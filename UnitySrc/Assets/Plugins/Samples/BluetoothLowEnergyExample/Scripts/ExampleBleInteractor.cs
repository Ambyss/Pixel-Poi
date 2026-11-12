using System;
using UnityEngine;
using Android.BLE;
using Android.BLE.Commands;
using UnityEngine.Android;
using UnityEngine.UI;

public class ExampleBleInteractor : MonoBehaviour
{
    [SerializeField]
    private DeviceRowView _deviceButton;
    [SerializeField]
    private Transform _deviceList;

    [SerializeField] private Button _scanBtn;
    [SerializeField] private AppPermissions _appPermissions;

    [SerializeField]
    private int _scanTime = 10;

    private float _scanTimer = 0f;

    private bool _isScanning = false;

    private void Start()
    {
        _scanBtn.onClick.AddListener(_appPermissions.RequestPermissions);
    }

    public void ScanForDevices()
    {
        if (!_isScanning)
        {
            _isScanning = true;
            BleManager.Instance.QueueCommand(new DiscoverDevices(OnDeviceFound, _scanTime * 1000));
        }
    }

    private void Update()
    {
        if(_isScanning)
        {
            _scanTimer += Time.deltaTime;
            if(_scanTimer > _scanTime)
            {
                _scanTimer = 0f;
                _isScanning = false;
            }
        }
    }

    private void OnDeviceFound(string name, string device)
    {
        DeviceRowView button = Instantiate(_deviceButton, _deviceList);
        button.Show(name, device);
    }
}
