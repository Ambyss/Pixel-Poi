using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    private RectTransform _rt;
    private Rect _lastSafeArea;
    private Vector2Int _lastScreen;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        Apply();
    }

    private void Update()
    {
        if (Screen.safeArea != _lastSafeArea ||
            Screen.width != _lastScreen.x ||
            Screen.height != _lastScreen.y)
        {
            Apply();
        }
    }

    private void Apply()
    {
        var safe = Screen.safeArea;
        _lastSafeArea = safe;
        _lastScreen = new Vector2Int(Screen.width, Screen.height);

        var min = safe.position;
        var max = safe.position + safe.size;
        min.x /= Screen.width;
        min.y /= Screen.height;
        max.x /= Screen.width;
        max.y /= Screen.height;

        _rt.anchorMin = min;
        _rt.anchorMax = max;
        _rt.offsetMin = Vector2.zero;
        _rt.offsetMax = Vector2.zero;
    }
}
