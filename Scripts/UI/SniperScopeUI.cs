using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SniperScopeUI : MonoBehaviour
{
    public GameObject scopeOverlay;
    public TextMeshProUGUI zoomLabel;
    public GameObject crosshairObject;
    public bool useGeneratedScopeOverlay = true;
    public Color scopeColor = Color.black;

    private bool _isReady;

    void Awake()
    {
        EnsureReady();
        HideScope();
    }

    public void ShowScope(int zoomLevel)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        EnsureReady();

        if (scopeOverlay) scopeOverlay.SetActive(true);
        if (crosshairObject) crosshairObject.SetActive(false);
        if (zoomLabel) zoomLabel.text = zoomLevel == 1 ? "x2" : "x4";
    }

    public void HideScope()
    {
        EnsureReady();

        if (scopeOverlay) scopeOverlay.SetActive(false);
        if (crosshairObject) crosshairObject.SetActive(true);
        if (zoomLabel) zoomLabel.text = "";
    }

    void EnsureReady()
    {
        if (_isReady)
            return;

        if (useGeneratedScopeOverlay)
            CreateGeneratedScopeOverlay();

        _isReady = true;
    }

    void CreateGeneratedScopeOverlay()
    {
        GameObject legacyOverlay = scopeOverlay;
        Transform existing = transform.Find("GeneratedScopeOverlay");
        GameObject overlay = existing != null ? existing.gameObject : new GameObject("GeneratedScopeOverlay");
        overlay.transform.SetParent(transform, false);
        overlay.layer = gameObject.layer;

        RectTransform rect = overlay.GetComponent<RectTransform>();
        if (rect == null)
            rect = overlay.AddComponent<RectTransform>();

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        SniperScopeGraphic graphic = overlay.GetComponent<SniperScopeGraphic>();
        if (graphic == null)
            graphic = overlay.AddComponent<SniperScopeGraphic>();

        graphic.color = Color.white;
        graphic.raycastTarget = false;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            overlay.transform.SetAsLastSibling();
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = true;
        }

        scopeOverlay = overlay;

        if (legacyOverlay != null && legacyOverlay != overlay)
            legacyOverlay.SetActive(false);
    }
}
