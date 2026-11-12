using System;
using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace.Misc
{
    public class SavesController
    {
        private const string BRIGHTNESS_KEY = "brightness";
        private const string DEVICES_KEY = "saved_devices";

        public int GetBrightness()
        {
            return PlayerPrefs.GetInt(BRIGHTNESS_KEY, 50);
        }

        public void SetBrightness(int value)
        {
            PlayerPrefs.SetInt(BRIGHTNESS_KEY, value);
        }

        public void SaveDevice(string address, string name)
        {
            if (string.IsNullOrEmpty(address))
                return;

            SavedDeviceList list = LoadDeviceList();
            SavedDevice existing = list.devices.Find(d => d.address == address);
            if (existing != null)
                existing.name = name;
            else
                list.devices.Add(new SavedDevice { address = address, name = name });

            PlayerPrefs.SetString(DEVICES_KEY, JsonUtility.ToJson(list));
            PlayerPrefs.Save();
        }

        public bool IsDeviceSaved(string address)
        {
            if (string.IsNullOrEmpty(address))
                return false;
            return LoadDeviceList().devices.Exists(d => d.address == address);
        }

        public void ForgetDevice(string address)
        {
            SavedDeviceList list = LoadDeviceList();
            if (list.devices.RemoveAll(d => d.address == address) == 0)
                return;

            PlayerPrefs.SetString(DEVICES_KEY, JsonUtility.ToJson(list));
            PlayerPrefs.Save();
        }

        public IReadOnlyList<SavedDevice> GetSavedDevices() => LoadDeviceList().devices;

        private SavedDeviceList LoadDeviceList()
        {
            string json = PlayerPrefs.GetString(DEVICES_KEY, string.Empty);
            if (string.IsNullOrEmpty(json))
                return new SavedDeviceList();
            return JsonUtility.FromJson<SavedDeviceList>(json) ?? new SavedDeviceList();
        }

        [Serializable]
        public class SavedDevice
        {
            public string address;
            public string name;
        }

        [Serializable]
        private class SavedDeviceList
        {
            public List<SavedDevice> devices = new();
        }
    }
}
