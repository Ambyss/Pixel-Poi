using System;
using System.Collections.Generic;
using UnityEngine;

namespace UI.Navigation
{
    /// <summary>
    /// Bottom tab bar: exactly one <see cref="AppWindow"/> is open at a time.
    /// Wired by Tools/PixelPoi/Build Main Navigation.
    /// </summary>
    public class TabBarNavigation : MonoBehaviour
    {
        [Serializable]
        public class TabEntry
        {
            public string Id;
            public TabButtonView Button;
            public AppWindow Window;
        }

        [SerializeField] private List<TabEntry> _tabs = new();
        [SerializeField] private int _defaultIndex = 0;

        [Tooltip("Tab bar height as a fraction of the SafeArea height. Windows get the rest.")]
        [SerializeField, Range(0.05f, 0.3f)] private float _barHeight = 0.10f;
        [SerializeField] private RectTransform _windowsRoot;

        public int CurrentIndex { get; private set; } = -1;

        public event Action<int> TabChanged;

        private void Awake()
        {
            ApplyLayout();

            for (int i = 0; i < _tabs.Count; i++)
            {
                if (IsValid(i))
                    _tabs[i].Button.Clicked += OnTabClicked;
            }
        }

        private void Start()
        {
            foreach (var tab in _tabs)
            {
                if (tab != null && tab.Window != null)
                    tab.Window.HideInstant();
            }

            Select(_defaultIndex);
        }

        /// <summary>Splits the SafeArea: bar at the bottom, windows above it.</summary>
        public void ApplyLayout()
        {
            var bar = (RectTransform)transform;
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = new Vector2(1f, _barHeight);
            bar.offsetMin = Vector2.zero;
            bar.offsetMax = Vector2.zero;

            if (_windowsRoot != null)
            {
                _windowsRoot.anchorMin = new Vector2(0f, _barHeight);
                _windowsRoot.anchorMax = Vector2.one;
                _windowsRoot.offsetMin = Vector2.zero;
                _windowsRoot.offsetMax = Vector2.zero;
            }
        }

        public void Select(int index)
        {
            if (index == CurrentIndex || !IsValid(index))
                return;

            // CurrentIndex only ever points at a validated entry.
            if (CurrentIndex >= 0)
                _tabs[CurrentIndex].Window.HideInstant();

            CurrentIndex = index;
            _tabs[index].Window.Show();

            for (int i = 0; i < _tabs.Count; i++)
            {
                var button = _tabs[i]?.Button;
                if (button != null)
                    button.SetSelected(i == index);
            }

            TabChanged?.Invoke(index);
        }

        public void Select(string id)
        {
            int index = _tabs.FindIndex(tab => tab != null && tab.Id == id);
            if (index < 0)
            {
                Debug.LogError($"[TabBarNavigation] No tab with id '{id}'.", this);
                return;
            }

            Select(index);
        }

        public void SelectDefault()
        {
            Select(_defaultIndex);
        }

        private void OnTabClicked(TabButtonView button)
        {
            int index = _tabs.FindIndex(tab => tab != null && tab.Button == button);
            if (index >= 0)
                Select(index);
        }

        private bool IsValid(int index)
        {
            if (index < 0 || index >= _tabs.Count)
            {
                Debug.LogError($"[TabBarNavigation] Tab index {index} is out of range (0..{_tabs.Count - 1}).", this);
                return false;
            }

            var tab = _tabs[index];
            if (tab == null || tab.Button == null || tab.Window == null)
            {
                Debug.LogError($"[TabBarNavigation] Tab #{index} '{tab?.Id}' has no Button or Window assigned — skipped.", this);
                return false;
            }

            return true;
        }
    }
}
