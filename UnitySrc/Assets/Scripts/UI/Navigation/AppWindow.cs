using DG.Tweening;
using UnityEngine;

namespace UI.Navigation
{
    /// <summary>
    /// Base class for tab windows. Shown with an OutBack scale-in, hidden instantly —
    /// never cross-animated, so two windows are never visible at once.
    /// </summary>
    public abstract class AppWindow : MonoBehaviour
    {
        [SerializeField] private float _showDuration = 0.2f;
        [SerializeField] private TabBarNavigation _navigation;

        public bool IsOpen { get; private set; }

        public void Show()
        {
            // Complete any leftover tween first, so nothing from a previous rapid
            // tab switch can touch this window after it is reopened.
            transform.DOKill(true);
            gameObject.SetActive(true);
            transform.localScale = Vector3.zero;
            transform.DOScale(1f, _showDuration).SetEase(Ease.OutBack);
            IsOpen = true;
            OnShown();
        }

        public void HideInstant()
        {
            transform.DOKill();
            transform.localScale = Vector3.zero;
            gameObject.SetActive(false);
            IsOpen = false;
            OnHidden();
        }

        protected void ReturnToDefaultTab()
        {
            if (_navigation != null)
                _navigation.SelectDefault();
        }

        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }
    }
}
