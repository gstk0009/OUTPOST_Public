using System;
using UnityEngine;

public class DefendObjective : MonoBehaviour, IDamageable
{
    public float maxHealth = 2500f;
    public float CurrentHealth { get; private set; }
    public bool IsDestroyed { get; private set; }
    public bool IsOperational { get; private set; }
    public float HealthRatio => maxHealth > 0f ? CurrentHealth / maxHealth : 0f;
    public event Action<float, float> HealthChanged;
    public event Action Destroyed;

    private Renderer[] _renderers;
    private Color[] _baseColors;
    private Light[] _operationalLights;

    void Awake()
    {
        CurrentHealth = maxHealth;
        _renderers = GetComponentsInChildren<Renderer>(true);
        _baseColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            _baseColors[i] = _renderers[i].sharedMaterial != null && _renderers[i].sharedMaterial.HasProperty("_BaseColor")
                ? _renderers[i].sharedMaterial.GetColor("_BaseColor")
                : Color.white;
        CreateOperationalLights();
    }

    void Update()
    {
        if (_operationalLights == null) return;
        float target = IsOperational && !IsDestroyed ? 1f : 0f;
        float pulse = 0.82f + Mathf.Sin(Time.time * 4.2f) * 0.18f;
        foreach (Light statusLight in _operationalLights)
        {
            if (statusLight == null) continue;
            statusLight.intensity = Mathf.MoveTowards(statusLight.intensity, target * 5.5f * pulse, Time.deltaTime * 12f);
            statusLight.enabled = statusLight.intensity > 0.02f;
        }
    }

    void CreateOperationalLights()
    {
        Transform root = transform.Find("OperationalStatusLights");
        if (root == null)
        {
            root = new GameObject("OperationalStatusLights").transform;
            root.SetParent(transform, false);
        }
        Vector3[] positions = { new(-2.2f, 1.7f, 0f), new(2.2f, 1.7f, 0f), new(0f, 1.25f, -2f) };
        _operationalLights = new Light[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            Transform child = root.Find("GreenStatusLight_" + (i + 1));
            if (child == null)
            {
                child = new GameObject("GreenStatusLight_" + (i + 1), typeof(Light)).transform;
                child.SetParent(root, false);
            }
            child.localPosition = positions[i];
            Light light = child.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.12f, 1f, 0.34f);
            light.range = 8.5f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            _operationalLights[i] = light;
        }
    }

    void Start()
    {
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void TakeDamage(float damage)
    {
        if (!IsOperational || IsDestroyed || damage <= 0f)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
        UpdateDamageLook();

        if (CurrentHealth <= 0f)
        {
            IsDestroyed = true;
            Destroyed?.Invoke();
        }
    }

    public void SetOperational(bool operational)
    {
        IsOperational = operational && !IsDestroyed;
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void Repair(float amount)
    {
        if (IsDestroyed || amount <= 0f)
            return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
        UpdateDamageLook();
    }

    void UpdateDamageLook()
    {
        float danger = 1f - HealthRatio;
        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer target = _renderers[i];
            if (target == null)
                continue;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            target.GetPropertyBlock(block);
            block.SetColor("_BaseColor", Color.Lerp(_baseColors[i], new Color(0.48f, 0.04f, 0.02f), danger * 0.72f));
            target.SetPropertyBlock(block);
        }
    }
}
