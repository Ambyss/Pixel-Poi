using System;
using Android.BLE;
using Android.BLE.Commands;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class DeviceRowView : MonoBehaviour
{
    private string _deviceUuid = string.Empty;
    private string _deviceName = string.Empty;

    [SerializeField]
    private Text _deviceUuidText;
    [SerializeField]
    private Text _deviceNameText;

    [SerializeField]
    private Image _deviceButtonImage;
    [SerializeField]
    private Text _deviceButtonText;

    [SerializeField]
    private Color _onConnectedColor;

    [SerializeField] 
    private Button _buttonComponent;
    private Color _previousColor;

    private bool _isConnected = false;

    private ConnectToDevice _connectCommand;
    private ReadFromCharacteristic _readFromCharacteristic;

    public Action<string, string> OnDeviceConnected;
    public Action<string> OnDeviceDisconnected;

    public string DeviceUuid => _deviceUuid;

    public void Show(string uuid, string name)
    {
        _deviceButtonText.text = "Connect";

        _deviceUuid = uuid;
        _deviceName = name;

        _deviceUuidText.text = uuid;
        _deviceNameText.text = name;

        _buttonComponent.onClick.AddListener(ToggleConnect);

        gameObject.SetActive(true);
    }

    public void ToggleConnect()
    {
        if (!_isConnected)
            Connect();
        else
        {
            _connectCommand.Disconnect();
            _buttonComponent.interactable = false;
        }
    }

    public void Connect()
    {
        if (_isConnected)
            return;

        _connectCommand = new ConnectToDevice(_deviceUuid, OnConnected, OnDisconnected);
        BleManager.Instance.QueueCommand(_connectCommand);
        _buttonComponent.interactable = false;
    }

    public void SubscribeToExampleService()
    {
    }

    private void OnConnected(string deviceUuid)
    {
        _previousColor = _deviceButtonImage.color;
        _deviceButtonImage.color = _onConnectedColor;

        _isConnected = true;
        _buttonComponent.interactable = true;
        _deviceButtonText.text = "Disconnect";

        OnDeviceConnected?.Invoke(deviceUuid, _deviceName);
    }

    private void OnDisconnected(string deviceUuid)
    {
        _deviceButtonImage.color = _previousColor;
        _buttonComponent.interactable = true;
        _isConnected = false;
        _deviceButtonText.text = "Connect";

        OnDeviceDisconnected?.Invoke(deviceUuid);
    }
}
