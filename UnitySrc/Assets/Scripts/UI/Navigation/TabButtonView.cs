using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Navigation
{
    public class TabButtonView : MonoBehaviour
    {
        private const float HighlightAlpha = 0.15f;

        [SerializeField] private Button _button;
        [SerializeField] private Image _background;
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Color _selectedColor;
        [SerializeField] private Color _normalColor;

        private bool _isSelected;

        public event Action<TabButtonView> Clicked;

        private void Awake()
        {
            if (_button != null)
                _button.onClick.AddListener(OnClick);
            else
                Debug.LogError($"[TabButtonView] '{name}' has no Button assigned.", this);
        }

        public void SetLabel(string text)
        {
            if (_label != null)
                _label.text = text;
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;

            var tint = selected ? _selectedColor : _normalColor;
            if (_icon != null)
                _icon.color = tint;
            if (_label != null)
                _label.color = tint;

            if (_background != null)
            {
                var highlight = _selectedColor;
                highlight.a = selected ? HighlightAlpha : 0f;
                _background.color = highlight;
            }
        }

        private void OnClick()
        {
            transform.DOKill();
            transform.localScale = Vector3.one;
            transform.DOPunchScale(Vector3.one * -0.06f, 0.1f, 1, 0.5f);

            if (!_isSelected)
                Clicked?.Invoke(this);
        }
    }
}
