using System.Collections.Generic;
using BLE;
using DefaultNamespace.Misc;
using MEC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace UI
{
    public class SpeedWindow : MonoBehaviour
    {
        [SerializeField] private Slider _slider;
        [SerializeField] private TextMeshProUGUI _value;

        [Inject] private BLESender _sender;
        [Inject] private SavesController _savesController;
        [Inject] private BLEController _bleController;

        private float valueCache;
        private CoroutineHandle timerHandle;
        
        private void Start()
        {
            _bleController.OnNewConnection += OnNewConnection;
            _slider.onValueChanged.AddListener(ValueChanged);
            _slider.SetValueWithoutNotify(_savesController.GetBrightness());
            UpdateLabel(_slider.value);
        }

        private void OnNewConnection()
        {
            ValueChanged(_slider.value);
        }

        private void ValueChanged(float arg0)
        {
            valueCache = arg0;
            if (timerHandle.IsRunning)
                Timing.KillCoroutines(timerHandle);
            timerHandle = Timing.RunCoroutine(_Timer());
        }

        private void ApplyValue()
        {
            var value = (int) valueCache;
            var data = new List<byte> {BLEConstants.COMMAND_SET_SPEED, (byte) value};
            Debug.Log($"Speed set to {value}%");
            _sender.Transmit(data);
            _savesController.SetBrightness(value);
            UpdateLabel(valueCache);
        }
        
        private void UpdateLabel(float arg0)
        {
            var intValue = arg0 / _slider.maxValue * 100;
            _value.text = $"{intValue}%";
        }

        private IEnumerator<float> _Timer()
        {
            yield return Timing.WaitForSeconds(.3f);
            ApplyValue();
        }
    }
}