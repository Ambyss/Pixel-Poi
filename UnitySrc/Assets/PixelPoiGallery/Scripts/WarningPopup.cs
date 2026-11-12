using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class WarningPopup : MonoBehaviour
{
    public Image backdrop;
    public RectTransform panel;
    public TextMeshProUGUI messageLabel;
    public Button okButton;

    private void Awake()
    {
        okButton.onClick.RemoveAllListeners();
        okButton.onClick.AddListener(Hide);
        gameObject.SetActive(false);
    }

    public void Show(string message)
    {
        messageLabel.text = message;
        gameObject.SetActive(true);

        backdrop.DOKill();
        panel.DOKill();

        var c = backdrop.color;
        c.a = 0f;
        backdrop.color = c;
        backdrop.DOFade(0.6f, 0.2f);

        panel.localScale = Vector3.zero;
        panel.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
    }

    public void HideImmediate()
    {
        backdrop.DOKill(true);
        panel.DOKill(true);
        gameObject.SetActive(false);
    }

    private void Hide()
    {
        backdrop.DOKill();
        panel.DOKill();

        backdrop.DOFade(0f, 0.15f);
        panel.DOScale(0f, 0.2f).SetEase(Ease.InBack)
            .OnComplete(() => gameObject.SetActive(false));
    }
}
