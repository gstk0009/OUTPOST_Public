using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class CombatFxPool : MonoBehaviour
{
    public enum FxType { Muzzle, Tracer, Impact }
    public static CombatFxPool Instance { get; private set; }

    private readonly Dictionary<FxType, Queue<GameObject>> _available = new();
    private readonly Dictionary<Color32, Material> _materials = new();

    void Awake()
    {
        Instance = this;
        Prewarm(FxType.Muzzle, 12);
        Prewarm(FxType.Tracer, 24);
        Prewarm(FxType.Impact, 32);
    }

    void Prewarm(FxType type, int count)
    {
        if (!_available.ContainsKey(type)) _available[type] = new Queue<GameObject>();
        for (int i = 0; i < count; i++) _available[type].Enqueue(Create(type));
    }

    public GameObject Rent(FxType type)
    {
        if (!_available.TryGetValue(type, out Queue<GameObject> queue))
            _available[type] = queue = new Queue<GameObject>();
        GameObject item = queue.Count > 0 ? queue.Dequeue() : Create(type);
        item.SetActive(true);
        return item;
    }

    public void ReleaseAfter(GameObject item, FxType type, float seconds)
    {
        ReleaseAfterAsync(item, type, seconds).Forget();
    }

    async UniTaskVoid ReleaseAfterAsync(GameObject item, FxType type, float seconds)
    {
        bool cancelled = await UniTask.Delay((int)(Mathf.Max(0.01f, seconds) * 1000f),
            cancellationToken: this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
        if (cancelled || item == null) return;
        if (item.TryGetComponent(out ParticleSystem particles))
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        item.SetActive(false);
        item.transform.SetParent(transform, false);
        _available[type].Enqueue(item);
    }

    public Material GetMaterial(Color color)
    {
        Color32 key = color;
        if (_materials.TryGetValue(key, out Material material)) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        material = new Material(shader) { color = color };
        _materials[key] = material;
        return material;
    }

    GameObject Create(FxType type)
    {
        GameObject item = new GameObject("Pooled_" + type);
        item.transform.SetParent(transform, false);
        switch (type)
        {
            case FxType.Muzzle:
                item.AddComponent<ParticleSystem>();
                Light light = item.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                break;
            case FxType.Tracer:
                item.AddComponent<LineRenderer>();
                break;
            case FxType.Impact:
                item.AddComponent<ParticleSystem>();
                break;
        }
        item.SetActive(false);
        return item;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (Material material in _materials.Values)
            if (material != null) Destroy(material);
    }
}
