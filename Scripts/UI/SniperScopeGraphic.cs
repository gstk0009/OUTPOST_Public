using UnityEngine;
using UnityEngine.UI;

public class SniperScopeGraphic : Image
{
    [Range(0.28f, 0.49f)] public float scopeRadius = 0.43f;
    [Range(0.0005f, 0.01f)] public float lineThickness = 0.0022f;
    [Range(0.005f, 0.08f)] public float centerGap = 0.018f;
    [Range(256, 1024)] public int textureWidth = 768;
    [Range(0, 255)] public byte outsideShadeAlpha = 238;

    private Texture2D _runtimeTexture;
    private Sprite _runtimeSprite;
    private int _generatedWidth;
    private int _generatedHeight;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
        color = Color.white;
        GenerateScopeTexture();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (_runtimeTexture == null || ResolutionChanged())
            GenerateScopeTexture();
    }

    void GenerateScopeTexture()
    {
        ReleaseRuntimeAssets();

        float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
        _generatedWidth = Mathf.Clamp(textureWidth, 256, 1024);
        _generatedHeight = Mathf.Max(144, Mathf.RoundToInt(_generatedWidth / aspect));

        _runtimeTexture = new Texture2D(_generatedWidth, _generatedHeight, TextureFormat.RGBA32, false)
        {
            name = "Runtime Sniper Scope",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32[] pixels = new Color32[_generatedWidth * _generatedHeight];
        float cx = (_generatedWidth - 1) * 0.5f;
        float cy = (_generatedHeight - 1) * 0.5f;
        float minSize = Mathf.Min(_generatedWidth, _generatedHeight);
        float radius = minSize * scopeRadius;
        float line = Mathf.Max(2f, minSize * lineThickness);
        float gap = minSize * centerGap;
        Color32 black = new Color32(0, 0, 0, outsideShadeAlpha);
        Color32 reticle = new Color32(238, 242, 245, 255);
        Color32 clear = new Color32(0, 0, 0, 0);

        for (int y = 0; y < _generatedHeight; y++)
        {
            float dy = y - cy;
            for (int x = 0; x < _generatedWidth; x++)
            {
                float dx = x - cx;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                bool outside = distance >= radius;
                bool ring = distance >= radius - line;
                bool vertical = Mathf.Abs(dx) <= line * 0.5f && Mathf.Abs(dy) >= gap && Mathf.Abs(dy) <= radius;
                bool horizontal = Mathf.Abs(dy) <= line * 0.5f && Mathf.Abs(dx) >= gap && Mathf.Abs(dx) <= radius;
                bool centerDot = Mathf.Abs(dx) <= line && Mathf.Abs(dy) <= line;
                pixels[y * _generatedWidth + x] = outside || ring
                    ? black
                    : vertical || horizontal || centerDot
                        ? reticle
                        : clear;
            }
        }

        _runtimeTexture.SetPixels32(pixels);
        _runtimeTexture.Apply(false, true);
        _runtimeSprite = Sprite.Create(
            _runtimeTexture,
            new Rect(0f, 0f, _generatedWidth, _generatedHeight),
            new Vector2(0.5f, 0.5f),
            100f);
        _runtimeSprite.name = "Runtime Sniper Scope Sprite";
        sprite = _runtimeSprite;
        type = Type.Simple;
        preserveAspect = false;
        SetAllDirty();
    }

    bool ResolutionChanged()
    {
        if (Screen.height <= 0)
            return false;
        int expectedHeight = Mathf.Max(144, Mathf.RoundToInt(Mathf.Clamp(textureWidth, 256, 1024) / ((float)Screen.width / Screen.height)));
        return _generatedWidth != Mathf.Clamp(textureWidth, 256, 1024) || _generatedHeight != expectedHeight;
    }

    void ReleaseRuntimeAssets()
    {
        if (_runtimeSprite != null)
        {
            if (Application.isPlaying) Destroy(_runtimeSprite);
            else DestroyImmediate(_runtimeSprite);
        }
        if (_runtimeTexture != null)
        {
            if (Application.isPlaying) Destroy(_runtimeTexture);
            else DestroyImmediate(_runtimeTexture);
        }
        _runtimeSprite = null;
        _runtimeTexture = null;
    }

    protected override void OnDestroy()
    {
        ReleaseRuntimeAssets();
        base.OnDestroy();
    }
}
