using System;
using BLE;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Zenject;

public class PhotoDetailWindow : MonoBehaviour
{
    
    public Image backdrop;
    public RectTransform panel;
    public Image photoImage;
    public TextMeshProUGUI numberLabel;
    public Button applyButton;
    public Button changeButton;
    public Button removeButton;
    public Button uploadButton;
    public Button closeButton;
    public WarningPopup warningPopup;

    [Inject] private ImgSender imgSender; 
        
    private int _slotIndex;
    private string _localPath;
    private Texture2D _runtimeTexture;
    private Action<int> _onChange;
    private Action<int> _onRemove;

    private void Awake()
    {
        applyButton.onClick.RemoveAllListeners();
        applyButton.onClick.AddListener(OnApply);
        
        changeButton.onClick.RemoveAllListeners();
        changeButton.onClick.AddListener(OnChange);

        removeButton.onClick.RemoveAllListeners();
        removeButton.onClick.AddListener(OnRemove);

        uploadButton.onClick.RemoveAllListeners();
        uploadButton.onClick.AddListener(OnUpload);

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(Hide);

        gameObject.SetActive(false);
    }

    private void Hide()
    {
        Hide(null);
    }

    public void Open(int slotIndex, string localPath, Action<int> onChange, Action<int> onRemove)
    {
        _slotIndex = slotIndex;
        _localPath = localPath;
        _onChange = onChange;
        _onRemove = onRemove;

        numberLabel.text = "#" + (slotIndex + 1);
        LoadPhoto(localPath);

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

    private void LoadPhoto(string path)
    {
        DisposeTexture();
        _runtimeTexture = GalleryStorage.LoadTexture(path);
        if (_runtimeTexture == null) return;

        var sprite = Sprite.Create(
            _runtimeTexture,
            new Rect(0, 0, _runtimeTexture.width, _runtimeTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        photoImage.sprite = sprite;
        photoImage.preserveAspect = true;
    }

    private void OnApply()
    {
        imgSender.OpenImage(_slotIndex);
    }
    
    private void OnChange()
    {
        PunchButton(changeButton.transform);
        var slot = _slotIndex;
        var cb = _onChange;
        Hide(() => cb?.Invoke(slot));
    }

    private void OnRemove()
    {
        PunchButton(removeButton.transform);
        var slot = _slotIndex;
        var cb = _onRemove;
        Hide(() => cb?.Invoke(slot));
    }

    private void OnUpload()
    {
        PunchButton(uploadButton.transform);

        if (!PixelPoiUploadBridge.IsConnected())
        {
            warningPopup.Show("Device not connected.\nConnect your PixelPoi first.");
            return;
        }

        SetButtonsInteractable(false);

        PixelPoiUploadBridge.Upload(
            _localPath,
            _slotIndex,
            onComplete: () =>
            {
                SetButtonsInteractable(true);
                warningPopup.Show("Upload complete.");
            },
            onFailed: reason =>
            {
                SetButtonsInteractable(true);
                warningPopup.Show("Upload failed.\n" + reason);
            },
            onProgress: p =>
            {
                if (numberLabel != null)
                    numberLabel.text = "#" + (_slotIndex + 1) + "  " + Mathf.RoundToInt(p * 100f) + "%";
            });
    }

    private void SetButtonsInteractable(bool value)
    {
        changeButton.interactable = value;
        removeButton.interactable = value;
        uploadButton.interactable = value;
        closeButton.interactable = value;
        if (value)
            numberLabel.text = "#" + (_slotIndex + 1);
    }

    private void PunchButton(Transform t)
    {
        t.DOKill();
        t.localScale = Vector3.one;
        t.DOPunchScale(Vector3.one * -0.05f, 0.1f, 1, 0.5f);
    }

    private void Hide(Action onComplete = null)
    {
        backdrop.DOKill();
        panel.DOKill();

        backdrop.DOFade(0f, 0.15f);
        panel.DOScale(0f, 0.2f).SetEase(Ease.InBack)
            .OnComplete(() =>
            {
                gameObject.SetActive(false);
                DisposeTexture();
                onComplete?.Invoke();
            });
    }

    /// <summary>Closes without animation (tab switch). A pending Change/Remove still completes.</summary>
    public void HideImmediate()
    {
        backdrop.DOKill(true);
        panel.DOKill(true);
        gameObject.SetActive(false);
        DisposeTexture();
    }

    private void DisposeTexture()
    {
        if (_runtimeTexture != null)
        {
            Destroy(_runtimeTexture);
            _runtimeTexture = null;
        }
    }

    private void OnDestroy()
    {
        DisposeTexture();
    }
}
