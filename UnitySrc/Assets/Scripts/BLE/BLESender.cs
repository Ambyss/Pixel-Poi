using System;
using System.Collections.Generic;
using Android.BLE;
using Android.BLE.Commands;
using UnityEngine;
using Zenject;

namespace BLE
{
    public class BLESender
    {
        private string _uuid;
        // Must be lowercase: the Android plugin reports UUIDs via java.util.UUID.toString()
        // (always lowercase) and SubscribeToCharacteristic matches them case-sensitively,
        // so uppercase UUIDs here silently drop every TX notification (= no ACKs).
        private const string SERVICE_UUID = "5e400001-b5a3-f393-e0a9-e50e24dcca9e";
        private const string CHARACTERISTIC_UUID_RX = "5e400002-b5a3-f393-e0a9-e50e24dcca9e";
        private const string CHARACTERISTIC_UUID_TX = "5e400003-b5a3-f393-e0a9-e50e24dcca9e";

        // What we ask Android for. The firmware caps it at BLEDevice::setMTU(250), so the
        // negotiated value is 250 and an ATT write carries MTU - 3 = 247 bytes.
        private const int REQUESTED_MTU = 250;

        // BLE's default. Every connection starts here until someone raises it.
        private const int DEFAULT_ATT_MTU = 23;

        private const int ATT_WRITE_HEADER = 3;

        [Inject] private AckReceiver _ackReceiver;

        private SubscribeToCharacteristic _txSubscription;
        private int _attMtu = DEFAULT_ATT_MTU;

        /// <summary>
        /// How many bytes fit in one write to the RX characteristic. 20 on the default MTU,
        /// <see cref="REQUESTED_MTU"/> - 3 if the MTU was raised.
        /// </summary>
        public int MaxWriteLength => _attMtu - ATT_WRITE_HEADER;

        public bool IsConnected => !string.IsNullOrEmpty(_uuid);

        public string ActiveUuid => _uuid;

        public AckReceiver Ack => _ackReceiver;

        /// <summary>
        /// Takes over as the device to write to. Safe to call from the plugin's connected
        /// event: it sends "DeviceConnected" from discoveredService(), i.e. after service
        /// discovery, so the gatt and our characteristics already resolve by then.
        /// </summary>
        public void SetDevice(string uuid)
        {
            // Run even for the same address: a reconnect gets a fresh BluetoothGatt, so the
            // previous MTU and TX subscription say nothing about this link.
            Unsubscribe();
            _uuid = uuid;
            _attMtu = DEFAULT_ATT_MTU;

            SubscribeToTx();
            RequestMtu();
        }

        public void ClearDevice()
        {
            Unsubscribe();
            _uuid = null;
            _attMtu = DEFAULT_ATT_MTU;
        }

        /// <summary>
        /// Asks Android to raise the ATT MTU, which is what otherwise caps a write at 20
        /// bytes. The plugin has no requestMtu of its own, so its BluetoothGatt is read out
        /// of the private static mLeGattServers map by Java reflection.
        /// <para>
        /// Never through UnityAndroidBLE.getInstance(): every call to it after the first
        /// one replaces mLeDeviceListAdapter, mLeGattServers and mConnectedServers with
        /// empty ones, which breaks every lookup the plugin makes afterwards - writes,
        /// disconnects and all. The fields are static, so they are read off the class.
        /// </para>
        /// <para>
        /// The plugin has no onMtuChanged either, so there is no confirmation: when the call
        /// is accepted we assume the firmware's cap (BLEDevice::setMTU) was granted. If it
        /// was not, Android splits the larger writes into ATT long writes, which the
        /// firmware reassembles - slower, not broken. Any failure here is non-fatal and
        /// leaves the write size at the 20 bytes that always work.
        /// </para>
        /// </summary>
        private void RequestMtu()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (string.IsNullOrEmpty(_uuid))
                return;

            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var loader = activity.Call<AndroidJavaObject>("getClassLoader");

                // Class.forName through the app's loader, so no instance is needed.
                using var classClass = new AndroidJavaClass("java.lang.Class");
                using var bleClass = classClass.CallStatic<AndroidJavaObject>(
                    "forName", "com.velorexe.unityandroidble.UnityAndroidBLE", false, loader);

                using var field = bleClass.Call<AndroidJavaObject>("getDeclaredField", "mLeGattServers");
                field.Call("setAccessible", true);

                // Static field, hence a null target.
                using var servers = field.Call<AndroidJavaObject>("get", new object[] { null });
                if (servers == null)
                {
                    Debug.LogWarning($"[BLESender] No gatt map; write size stays {MaxWriteLength}");
                    return;
                }

                using var gatt = FindGatt(servers);
                if (gatt == null)
                {
                    Debug.LogWarning($"[BLESender] No BluetoothGatt for {_uuid}; " +
                                     $"write size stays {MaxWriteLength}");
                    return;
                }

                if (gatt.Call<bool>("requestMtu", REQUESTED_MTU))
                {
                    _attMtu = REQUESTED_MTU;
                    Debug.Log($"[BLESender] requestMtu({REQUESTED_MTU}) accepted on {_uuid}, " +
                              $"write size {MaxWriteLength} bytes");
                }
                else
                {
                    Debug.LogWarning($"[BLESender] requestMtu({REQUESTED_MTU}) refused on {_uuid}, " +
                                     $"write size stays {MaxWriteLength}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BLESender] Could not raise the MTU, write size stays " +
                                 $"{MaxWriteLength}: {e.Message}");
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// The BluetoothGatt for <see cref="_uuid"/> out of the plugin's
        /// Map&lt;BluetoothDevice, BluetoothGatt&gt;, or null if it holds none.
        /// </summary>
        private AndroidJavaObject FindGatt(AndroidJavaObject servers)
        {
            using var entries = servers.Call<AndroidJavaObject>("entrySet");
            using var iterator = entries.Call<AndroidJavaObject>("iterator");

            while (iterator.Call<bool>("hasNext"))
            {
                using var entry = iterator.Call<AndroidJavaObject>("next");
                using var device = entry.Call<AndroidJavaObject>("getKey");

                // The map holds every connection, so pick the one this sender writes to.
                string address = device?.Call<string>("getAddress");
                if (!string.Equals(address, _uuid, StringComparison.OrdinalIgnoreCase))
                    continue;

                return entry.Call<AndroidJavaObject>("getValue");
            }

            return null;
        }
#endif

        private void SubscribeToTx()
        {
            if (string.IsNullOrEmpty(_uuid))
                return;

            _txSubscription = new SubscribeToCharacteristic(
                _uuid,
                SERVICE_UUID,
                CHARACTERISTIC_UUID_TX,
                bytes => _ackReceiver.OnNotify(bytes),
                true);

            BleManager.Instance.QueueCommand(_txSubscription);
            Debug.Log($"Subscribed to TX characteristic for ACK on {_uuid}");
        }

        private void Unsubscribe()
        {
            if (_txSubscription == null)
                return;

            _txSubscription.Unsubscribe();
            _txSubscription = null;
        }

        public void Transmit(byte oneByte)
        {
            Transmit(new List<byte> { oneByte });
        }

        public void Transmit(List<byte> chunk)
        {
            if (!IsConnected)
            {
                Debug.Log("Skipping transmit: no connected device");
                return;
            }

            SendDataToCharacteristic(chunk);
        }

        private void SendDataToCharacteristic(List<byte> chunk)
        {
            var writeCharacteristic = new WriteToCharacteristic(_uuid, SERVICE_UUID, CHARACTERISTIC_UUID_RX,
                chunk.ToArray(), true);
            writeCharacteristic.Start();
        }
    }
}
