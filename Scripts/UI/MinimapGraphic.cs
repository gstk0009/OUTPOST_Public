using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MinimapGraphic : MaskableGraphic
{
    public Rect worldBounds = new Rect(-75f, -33f, 150f, 180f);
    public Transform player;
    public DefendObjective objective;
    public GameCycleDirector director;
    public float refreshInterval = 0.12f;
    public bool drawBackground;

    private static readonly Vector3[] ThreatOrigins =
    {
        new(-8f, 0f, 4f), new(0f, 0f, 7f), new(8f, 0f, 4f),
        new(-17f, 0f, 22f), new(17f, 0f, 31f), new(0f, 0f, 76f),
        new(-17f, 0f, 91f), new(17f, 0f, 105f),
        new(-31f, 0f, 5f), new(31f, 0f, 18f), new(-31f, 0f, 58f),
        new(31f, 0f, 74f), new(-27f, 0f, 108f), new(27f, 0f, 118f), new(0f, 0f, 143f)
    };

    private readonly List<ZombieAI> _zombies = new();
    private float _nextRefresh;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
        if (player == null)
        {
            PlayerController controller = FindFirstObjectByType<PlayerController>();
            if (controller != null) player = controller.transform;
        }
        if (objective == null) objective = FindFirstObjectByType<DefendObjective>();
        if (director == null) director = FindFirstObjectByType<GameCycleDirector>();
    }

    void Update()
    {
        if (Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + refreshInterval;
            _zombies.Clear();
            _zombies.AddRange(ZombieAI.ActiveZombies);
        }
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (drawBackground)
            AddQuad(vh, r, new Color(0.055f, 0.065f, 0.04f, 0.90f));
        AddBorder(vh, r, 4f, new Color(0.88f, 0.64f, 0.22f, 0.98f));

        foreach (Vector3 origin in ThreatOrigins)
            AddBlip(vh, WorldToLocal(origin, r), 3f, new Color(1f, 0.48f, 0.08f, 0.92f));

        if (objective != null)
            AddBlip(vh, WorldToLocal(objective.transform.position, r), 8f, new Color(1f, 0.68f, 0.12f, 1f));
        DrawNavigationTarget(vh, r);
        foreach (ZombieAI zombie in _zombies)
            if (zombie != null && zombie.gameObject.activeInHierarchy)
                AddBlip(vh, WorldToLocal(zombie.transform.position, r), 3.5f, new Color(1f, 0.18f, 0.08f, 0.95f));
        if (player != null)
            AddBlip(vh, WorldToLocal(player.position, r), 6f, new Color(0.3f, 1f, 0.42f, 1f));
    }

    void DrawNavigationTarget(VertexHelper vh, Rect r)
    {
        if (director == null) return;
        Transform target = null;
        Color targetColor = new(0.18f, 1f, 0.48f, 1f);
        bool generatorRoute = director.Phase == GameCycleDirector.CyclePhase.GeneratorActivation ||
                              director.Phase == GameCycleDirector.CyclePhase.Preparing && director.CycleNumber is 2 or 3;
        if (generatorRoute && objective != null) target = objective.transform;
        else if ((director.Phase == GameCycleDirector.CyclePhase.HelipadTransit ||
                  director.Phase == GameCycleDirector.CyclePhase.Extraction ||
                  director.Phase == GameCycleDirector.CyclePhase.Preparing && director.CycleNumber == director.totalCycles) &&
                 director.extractionController != null)
        {
            target = director.extractionController.landingPoint;
            targetColor = new Color(0.18f, 0.72f, 1f, 1f);
        }
        if (target == null) return;

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6.5f);
        Vector2 center = WorldToLocal(target.position, r);
        Color halo = targetColor;
        halo.a = Mathf.Lerp(0.16f, 0.62f, pulse);
        AddBlip(vh, center, Mathf.Lerp(10f, 15f, pulse), halo);
        targetColor.a = Mathf.Lerp(0.68f, 1f, pulse);
        AddBlip(vh, center, 5.5f, targetColor);
    }

    Vector2 WorldToLocal(Vector3 world, Rect local)
    {
        float x = Mathf.InverseLerp(worldBounds.xMin, worldBounds.xMax, world.x);
        float y = Mathf.InverseLerp(worldBounds.yMin, worldBounds.yMax, world.z);
        return new Vector2(Mathf.Lerp(local.xMin + 10f, local.xMax - 10f, x), Mathf.Lerp(local.yMin + 10f, local.yMax - 10f, y));
    }

    static void AddBlip(VertexHelper vh, Vector2 center, float radius, Color color)
    {
        AddQuad(vh, new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), color);
    }

    static void AddBorder(VertexHelper vh, Rect rect, float width, Color color)
    {
        AddQuad(vh, new Rect(rect.xMin, rect.yMax - width, rect.width, width), color);
        AddQuad(vh, new Rect(rect.xMin, rect.yMin, rect.width, width), color);
        AddQuad(vh, new Rect(rect.xMin, rect.yMin, width, rect.height), color);
        AddQuad(vh, new Rect(rect.xMax - width, rect.yMin, width, rect.height), color);
    }

    static void AddQuad(VertexHelper vh, Rect rect, Color color)
    {
        int index = vh.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = new Vector3(rect.xMin, rect.yMin); vh.AddVert(vertex);
        vertex.position = new Vector3(rect.xMin, rect.yMax); vh.AddVert(vertex);
        vertex.position = new Vector3(rect.xMax, rect.yMax); vh.AddVert(vertex);
        vertex.position = new Vector3(rect.xMax, rect.yMin); vh.AddVert(vertex);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index + 2, index + 3, index);
    }
}
