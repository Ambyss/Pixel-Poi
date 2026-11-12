using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

namespace BLE
{
    public class BLEController
    {
        [Inject] private BLESender _sender;

        private readonly Dictionary<string, string> _devices = new();

        public Dictionary<string, string> Devices => _devices;

        public Action OnNewConnection;

        public void RegisterDevice(string uuid, string name)
        {
            _devices[uuid] = name;
            _sender.SetDevice(uuid);
            OnNewConnection?.Invoke();
        }

        public void RemoveDevice(string uuid)
        {
            if (!_devices.Remove(uuid))
                Debug.Log($"Device {uuid} not found on remove");

            if (_devices.Count == 0)
                _sender.ClearDevice();
            else if (uuid == _sender.ActiveUuid)
                _sender.SetDevice(_devices.Keys.First());
        }
    }
}
