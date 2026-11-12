using UnityEngine;

namespace BLE
{
    public enum AckResult
    {
        None,
        Ok,
        Fail
    }

    public class AckReceiver
    {
        private const byte ACK_OK = 0xA0;
        private const byte ACK_FAIL = 0xA1;

        private int _waitingKey = -1;
        private AckResult _lastResult = AckResult.None;

        public void BeginWait(int key)
        {
            _waitingKey = key;
            _lastResult = AckResult.None;
        }

        public AckResult Poll(int key)
        {
            if (_waitingKey != key)
                return AckResult.None;
            return _lastResult;
        }

        public void OnNotify(byte[] data)
        {
            Debug.Log($"1 ON NOTIFY");
            Debug.Log($"2 ON NOTIFY {data.Length} bytes");
            if (data == null || data.Length < 3)
                return;

            byte status = data[0];
            int seq = data[1] | (data[2] << 8);

            if (seq != _waitingKey)
                return;

            if (status == ACK_OK)
                _lastResult = AckResult.Ok;
            else if (status == ACK_FAIL)
                _lastResult = AckResult.Fail;
        }
    }
}
