using System;
using System.Collections.Generic;
using Android.BLE;
using Android.BLE.Commands;
using BLE;
using DefaultNamespace.Misc;
using DG.Tweening;
using MEC;
using TMPro;
using UI.Navigation;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace DefaultNamespace.BLE
{
    public class BLEWindow : AppWindow
    {
        [SerializeField] private int _scanTime = 10;
        [SerializeField] private DeviceRowView _deviceButton;
        [SerializeField] private Transform _deviceList;
        [SerializeField] private TextMeshProUGUI _locationText;
        [SerializeField] private string _locEnableText;
        [SerializeField] private string _locDisableText;
        [SerializeField] private Color _locEnableColor;
        [SerializeField] private Color _locDisableColor;
        [SerializeField] private AppPermissions _appPermissions;
        [Tooltip("Optional. Switches back to the default tab when pressed.")]
        [SerializeField] private Button _hideBtn;

        [Tooltip("How long (seconds) to wait for the TARGET_UUID service while probing a discovered device.")]
        [SerializeField] private float _probeTimeout = 6f;

        [Inject] private BLESender _sender;
        [Inject] private BLEController _bleController;
        [Inject] private SavesController _saves;

        private LocationServiceStatus _currentStatus;
        private List<DeviceRowView> _deviceViews = new();
        private CoroutineHandle _scanLoop;

        // Addresses that already have a row or are queued/in-flight for probing.
        private readonly HashSet<string> _probedDevices = new();
        private readonly Queue<(string address, string name)> _probeQueue = new();
        private bool _isProbing;

        private void Awake()
        {
            // Subscribe in Awake, not Start: the first Show() activates this object and scans
            // right away, and AppPermissions may fire the granted event synchronously.
            _appPermissions.allPermissionsGrantedEvent.AddListener(ScanForDevices);
            if (_hideBtn != null)
                _hideBtn.onClick.AddListener(ReturnToDefaultTab);
        }

        void Start()
        {
            LocationStatusChanged(Input.location.isEnabledByUser);
            Observable.EveryUpdate()
                .Select(_ => Input.location.isEnabledByUser)
                .DistinctUntilChanged()
                .Subscribe(LocationStatusChanged)
                .AddTo(this);
        }

        protected override void OnShown()
        {
            Timing.KillCoroutines(_scanLoop);
            RemoveDeviceViews();
            Scan();
        }

        protected override void OnHidden()
        {
            // Each Show() starts a rescan loop; stop it so tab switching doesn't stack loops.
            Timing.KillCoroutines(_scanLoop);
        }

        private void RemoveDeviceViews()
        {
            for (int i = _deviceViews.Count - 1; i >= 0; i--)
            {
                DeviceRowView view = _deviceViews[i];

                if (view == null)
                {
                    _deviceViews.RemoveAt(i);
                    continue;
                }

                if (!_bleController.Devices.ContainsKey(view.DeviceUuid))
                {
                    // Drop it from the probed set too, otherwise the destroyed row
                    // can never come back: the next scan would skip the device.
                    _probedDevices.Remove(view.DeviceUuid);
                    Destroy(view.gameObject);
                    _deviceViews.RemoveAt(i);
                }
            }
        }

        private IEnumerator<float> _ScanContinueCheck()
        {
            yield return Timing.WaitForSeconds(_scanTime);
            if (IsOpen)
                Scan();
        }

        private void ScanForDevices()
        {
            BleManager.Instance.QueueCommand(new DiscoverDevices(CreateDeviceView, _scanTime * 1000));
        }

        private void LocationStatusChanged(bool isEnable)
        {
            if (_locationText == null) return;
            _locationText.text = isEnable ? _locEnableText : _locDisableText;
            _locationText.color = isEnable ? _locEnableColor : _locDisableColor;
        }

        private void Scan()
        {
            if (!Input.location.isEnabledByUser)
            {
                Debug.Log("[BLEWindow] Scan skipped: location is disabled");
                _locationText?.transform.DOPunchScale(Vector3.one, .5f);
                return;
            }
            Debug.Log("[BLEWindow] Scan started");
            _appPermissions.RequestPermissions();
            _scanLoop = Timing.RunCoroutine(_ScanContinueCheck());
        }

        // DiscoverDevices invokes this as (BleObject.Device, BleObject.Name),
        // i.e. the MAC address comes first and the advertised name second.
        private void CreateDeviceView(string address, string name)
        {
            if (string.IsNullOrEmpty(address))
                return;

            if (!_probedDevices.Add(address))
                return;

            //Debug.Log($"[BLEWindow] Discovered '{name}' ({address}), queued for probing");
            _probeQueue.Enqueue((address, name));
            if (!_isProbing)
                Timing.RunCoroutine(_ProbeQueue());
        }

        private IEnumerator<float> _ProbeQueue()
        {
            _isProbing = true;
            while (_probeQueue.Count > 0)
            {
                var next = _probeQueue.Dequeue();
                yield return Timing.WaitUntilDone(Timing.RunCoroutine(_ProbeDevice(next.address, next.name)));
            }
            _isProbing = false;
        }

        private IEnumerator<float> _ProbeDevice(string address, string name)
        {
            bool hasTarget = false;
            bool disconnected = false;

            var probe = new ConnectToDevice(address,
                _ => { },
                _ => disconnected = true,
                (_, service) =>
                {
                    if (string.Equals(service, BLEConstants.TARGET_UUID, StringComparison.OrdinalIgnoreCase))
                        hasTarget = true;
                },
                (_, __, ___) => { });
            BleManager.Instance.QueueCommand(probe);

            float elapsed = 0f;
            while (!hasTarget && !disconnected && elapsed < _probeTimeout)
            {
                yield return Timing.WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (!disconnected)
            {
                probe.Disconnect();
                float settle = 0f;
                while (!disconnected && settle < 2f)
                {
                    yield return Timing.WaitForSeconds(0.1f);
                    settle += 0.1f;
                }
            }

            if (!hasTarget)
            {
                // Let a later scan retry: the device may have been busy or out of range.
                _probedDevices.Remove(address);
                //Debug.Log($"[BLEWindow] '{name}' ({address}) has no target service, skipped");
                yield break;
            }

            DeviceRowView row = SpawnDeviceView(address, name);

            if (_saves.IsDeviceSaved(address))
                row.Connect();
        }

        private DeviceRowView SpawnDeviceView(string address, string name)
        {
            DeviceRowView button = Instantiate(_deviceButton, _deviceList);
            button.Show(address, name);
            button.OnDeviceConnected += OnDeviceConnected;
            button.OnDeviceDisconnected += OnDeviceDisconnected;
            _deviceViews.Add(button);
            return button;
        }

        private void OnDeviceConnected(string uuid, string name)
        {
            _bleController.RegisterDevice(uuid, name);
            _saves.SaveDevice(uuid, name);
        }

        private void OnDeviceDisconnected(string uuid)
        {
            _bleController.RemoveDevice(uuid);
        }
    }
}
