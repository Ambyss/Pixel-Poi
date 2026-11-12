using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ImageSlotCell : MonoBehaviour
{
    public Button button;
    public Image photoImage;
    public Image plusIcon;
    public TextMeshProUGUI indexLabel;
    public Button removeButton;

    private int _slotIndex;
    private Texture2D _runtimeTexture;
    private Action<int> _onPick;
    private Action<int> _onRemove;
    private Action<int> _onOpen;

    public int SlotIndex => _slotIndex;
    public bool HasImage { get; private set; }

    public void Init(int slotIndex, Action<int> onPick, Action<int> onRemove, Action<int> onOpen)
    {
        _slotIndex = slotIndex;
        _onPick = onPick;
        _onRemove = onRemove;
        _onOpen = onOpen;

        indexLabel.text = (slotIndex + 1).ToString();

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClick);

        removeButton.onClick.RemoveAllListeners();
        removeButton.onClick.AddListener(OnRemoveClick);

        SetEmpty();
    }

    public string CurrentPath { get; private set; }

    public void SetEmpty()
    {
        HasImage = false;
        CurrentPath = null;
        DisposeTexture();

        photoImage.enabled = false;
        photoImage.sprite = null;
        plusIcon.enabled = true;
        removeButton.gameObject.SetActive(false);
    }

    public void SetImageFromFile(string fullPath)
    {
        CurrentPath = fullPath;
        var tex = GalleryStorage.LoadTexture(fullPath);
        if (tex == null)
        {
            SetEmpty();
            return;
        }
        SetImageFromTexture(tex);
    }

    public void SetImageFromTexture(Texture2D tex)
    {
        DisposeTexture();
        _runtimeTexture = tex;

        var sprite = Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            100f);

        photoImage.sprite = sprite;
        photoImage.enabled = true;
        plusIcon.enabled = false;
        removeButton.gameObject.SetActive(true);
        HasImage = true;
    }

    private void OnClick()
    {
        if (HasImage)
            _onOpen?.Invoke(_slotIndex);
        else
            _onPick?.Invoke(_slotIndex);
    }

    private void OnRemoveClick()
    {
        _onRemove?.Invoke(_slotIndex);
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
