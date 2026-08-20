using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public abstract class WeaponBase : MonoBehaviour
{
    private static AudioClip _fallbackShotClip;

    [Header("Base Stats")]
    public string weaponName;
    public int slotIndex; // 1=primary, 2=pistol, 3=knife, 4=grenade
    public float damage = 50f;
    public int maxAmmo = 30;
    public int currentAmmo;
    public int reserveAmmo = 90;
    public float fireRate = 0.1f;
    public float reloadTime = 2f;

    [Header("Accuracy")]
    public float baseSpread = 0.5f;
    public float moveSpreadMult = 2.0f;
    public float jumpSpreadMult = 3.5f;
    public float crouchSpreadMult = 0.8f;

    [Header("Fire Mode")]
    public bool isAutomatic = false;
    public bool isSemiAutoWithHold = false;

    [Header("FX References")]
    public ParticleSystem muzzleFlash;
    public GameObject muzzleFlashPrefab;
    public GameObject impactEffectPrefab;
    public GameObject bloodImpactEffectPrefab;
    public Transform bulletSpawnPoint;
    public bool autoCreateMuzzlePoint = true;
    public bool spawnVisualBullet = false;
    public float visualBulletSpeed = 90f;
    public float visualBulletDistance = 80f;
    public float muzzleFlashLifetime = 0.12f;
    public Vector3 muzzleFlashScale = new Vector3(0.08f, 0.08f, 0.08f);
    public float hitEffectLifetime = 2f;
    [Range(0f, 1f)] public float tracerChance = 0.4f;
    public float tracerDuration = 0.055f;
    public float tracerWidth = 0.018f;
    public Color tracerColor = new Color(1f, 0.58f, 0.12f, 1f);

    [Header("Components")]
    public Animator animator;
    public GunAudio gunAudio;
    public RecoilSystem recoilSystem;

    [Header("Input")]
    public bool handleInputDirectly = true;

    protected float nextFireTime;
    protected bool isReloading;
    protected PlayerController player;
    protected CrosshairUI crosshair;
    private AudioSource _weaponAudioSource;
    private float _nextEmptyClickTime;
    public Vector3 LastShotDirection { get; private set; }
    public Vector3 LastHitPoint { get; private set; }
    public bool LastShotHit { get; private set; }

    protected virtual void Awake()
    {
        currentAmmo = maxAmmo;
        player = GetComponentInParent<PlayerController>();
        crosshair = FindFirstObjectByType<CrosshairUI>(FindObjectsInactive.Include);
        if (animator == null) TryGetComponent(out animator);
        TryGetComponent(out _weaponAudioSource);
        EnsureRuntimeFxSetup();
    }

    void Update()
    {
        if (handleInputDirectly)
            HandleFireInput();
    }

    public void HandleFireInput()
    {
        if (isAutomatic || isSemiAutoWithHold)
        {
            if (Input.GetMouseButton(0))
                Fire();
        }
        else
        {
            if (Input.GetMouseButtonDown(0))
                Fire();
        }

        if (Input.GetMouseButtonDown(1))
            AltFire();

        if (Input.GetKeyDown(KeyCode.R))
            TryReload();
    }

    public void TryReload()
    {
        if (!isReloading && currentAmmo < maxAmmo && reserveAmmo > 0)
            ReloadAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    protected void HandleEmptyTrigger()
    {
        if (Time.time < _nextEmptyClickTime) return;
        _nextEmptyClickTime = Time.time + 0.24f;
        gunAudio?.PlayEmptyClick();
        if (reserveAmmo > 0) TryReload();
    }

    public int AddReserveAmmo(int amount)
    {
        if (amount <= 0) return 0;
        reserveAmmo += amount;
        return amount;
    }

    public abstract void Fire();
    public abstract void AltFire();

    public void EnsureRuntimeFxSetup()
    {
        ResolveDefaultFxPrefabs();

        if (bulletSpawnPoint == null)
            bulletSpawnPoint = FindChildRecursive(transform, "MuzzlePoint");

        if (bulletSpawnPoint == null && autoCreateMuzzlePoint)
            bulletSpawnPoint = CreateMuzzlePoint();
    }

    protected virtual async UniTask ReloadAsync(CancellationToken token)
    {
        isReloading = true;
        if (animator) animator.SetTrigger("Reload");
        gunAudio?.PlayReloadSound();

        bool cancelled = await UniTask.Delay((int)(reloadTime * 1000f), cancellationToken: token).SuppressCancellationThrow();
        if (cancelled) { isReloading = false; return; }

        int needed = maxAmmo - currentAmmo;
        int take = Mathf.Min(needed, reserveAmmo);
        currentAmmo += take;
        reserveAmmo -= take;
        isReloading = false;
    }

    protected float GetMovementMultiplier()
    {
        if (player == null) return 1f;

        if (!player.isGrounded || player.WasJustLanded)
            return jumpSpreadMult;
        if (player.IsMoving)
            return player.isCrouching ? crouchSpreadMult : moveSpreadMult;
        if (player.isCrouching)
            return crouchSpreadMult;

        return 1f;
    }

    protected Vector3 GetSpreadDirection(Camera cam, float spreadDegrees)
    {
        Ray centerRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector2 random = Random.insideUnitCircle;
        float radius = Mathf.Tan(Mathf.Max(0f, spreadDegrees) * Mathf.Deg2Rad);
        return (centerRay.direction + cam.transform.right * random.x * radius + cam.transform.up * random.y * radius).normalized;
    }

    protected void ShootRaycast(float spread)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[WeaponBase] Cannot fire because no camera is tagged MainCamera.");
            return;
        }

        Ray centerRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 direction = GetSpreadDirection(cam, spread);
        LastShotDirection = direction;
        LastShotHit = Physics.Raycast(centerRay.origin, direction, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore);
        LastHitPoint = LastShotHit ? hit.point : centerRay.origin + direction * 200f;

        Transform muzzle = GetMuzzleTransform();
        Vector3 muzzleDirection = (LastHitPoint - muzzle.position).normalized;
        PlayMuzzleFx(muzzleDirection);
        SpawnVisualBullet(muzzleDirection);
        if (Random.value <= tracerChance)
            SpawnTracer(muzzle.position, LastHitPoint);
        crosshair?.PulseShot(this is SniperRifle ? 1f : this is Pistol ? 0.55f : 0.38f);

        if (gunAudio != null && gunAudio.fireSound != null)
            gunAudio.PlayFireSound();
        else
            PlayFallbackShotSound();

        Debug.DrawRay(centerRay.origin, direction * 60f, Color.red, 0.2f);

        if (!LastShotHit)
            return;

        IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            float finalDamage = damage;
            bool headshot = IsHeadshot(hit.collider);
            if (headshot)
                finalDamage *= 2.5f;
            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null) SpawnBloodEffect(hit);
            else SpawnHitEffect(impactEffectPrefab, hit);
            damageable.TakeDamage(finalDamage);
            bool killed = enemy != null && enemy.IsDead;
            if (enemy != null) enemy.FlashHit(headshot);
            crosshair?.ShowHitMarker(headshot, killed);
        }
        else
        {
            SpawnHitEffect(impactEffectPrefab, hit);
        }
    }

    protected void ReportMeleeHit(RaycastHit hit, IDamageable damageable, float appliedDamage)
    {
        bool headshot = IsHeadshot(hit.collider);
        EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
        damageable.TakeDamage(appliedDamage * (headshot ? 1.5f : 1f));
        SpawnBloodEffect(hit);
        if (enemy != null) enemy.FlashHit(headshot);
        crosshair?.ShowHitMarker(headshot, enemy != null && enemy.IsDead);
    }

    static bool IsHeadshot(Collider hitCollider)
    {
        if (hitCollider == null) return false;
        return hitCollider.name.ToLowerInvariant().Contains("head") || hitCollider.tag == "Head";
    }

    void PlayMuzzleFx(Vector3 direction)
    {
        Transform muzzle = GetMuzzleTransform();
        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);

        if (muzzleFlash != null)
        {
            muzzleFlash.transform.SetPositionAndRotation(muzzle.position, rotation);
            muzzleFlash.Play(true);
            return;
        }

        CreateProceduralMuzzleFlash(muzzle.position, rotation);
    }

    void SpawnHitEffect(GameObject prefab, RaycastHit hit)
    {
        CreateProceduralImpact(hit, ResolveSurfaceColor(hit.collider));
    }

    static Color ResolveSurfaceColor(Collider hitCollider)
    {
        string label = hitCollider != null ? hitCollider.name.ToLowerInvariant() : string.Empty;
        Renderer renderer = hitCollider != null ? hitCollider.GetComponentInParent<Renderer>() : null;
        if (renderer != null && renderer.sharedMaterial != null)
            label += " " + renderer.sharedMaterial.name.ToLowerInvariant();
        if (label.Contains("container"))
            return new Color(1f, 0.46f, 0.035f, 1f);
        if (label.Contains("metal") || label.Contains("gate") || label.Contains("fence"))
            return new Color(1f, 0.68f, 0.10f, 1f);
        if (label.Contains("dirt") || label.Contains("ground") || label.Contains("sand"))
            return new Color(1f, 0.43f, 0.06f, 1f);
        return new Color(1f, 0.52f, 0.08f, 1f);
    }

    void SpawnBloodEffect(RaycastHit hit)
    {
        CreateProceduralImpact(hit, new Color(0.65f, 0f, 0f, 1f));
    }

    void SpawnVisualBullet(Vector3 direction)
    {
        if (!spawnVisualBullet)
            return;

        Transform muzzle = GetMuzzleTransform();
        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
        BulletPool.Instance.Spawn(muzzle.position, rotation, direction, visualBulletSpeed, visualBulletDistance);
    }

    Transform GetMuzzleTransform()
    {
        if (bulletSpawnPoint == null)
            EnsureRuntimeFxSetup();

        if (bulletSpawnPoint != null)
            return bulletSpawnPoint;

        Camera cam = Camera.main;
        return cam != null ? cam.transform : transform;
    }

    Transform CreateMuzzlePoint()
    {
        Camera cam = Camera.main;
        Vector3 forward = cam != null ? cam.transform.forward : transform.forward;
        Renderer muzzleRenderer = FindPreferredMuzzleRenderer();
        Transform parent = muzzleRenderer != null ? muzzleRenderer.transform : transform;
        Vector3 worldPosition = EstimateMuzzleWorldPosition(muzzleRenderer, forward);
        Quaternion worldRotation = Quaternion.LookRotation(forward, Vector3.up);

        GameObject muzzle = new GameObject("MuzzlePoint");
        muzzle.transform.SetParent(parent, false);
        muzzle.transform.SetPositionAndRotation(worldPosition, worldRotation);
        return muzzle.transform;
    }

    Renderer FindPreferredMuzzleRenderer()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        string[] preferredNames = GetMuzzleParentNames();
        for (int i = 0; i < renderers.Length; i++)
        {
            string rendererName = renderers[i].name.ToLowerInvariant();
            if (rendererName.Contains("hand") || rendererName.Contains("arm")) continue;
            for (int j = 0; j < preferredNames.Length; j++)
                if (rendererName.Contains(preferredNames[j].ToLowerInvariant())) return renderers[i];
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            string rendererName = renderers[i].name.ToLowerInvariant();
            if (!rendererName.Contains("hand") && !rendererName.Contains("arm")) return renderers[i];
        }
        return null;
    }

    Transform FindPreferredMuzzleParent()
    {
        string[] names = GetMuzzleParentNames();
        for (int i = 0; i < names.Length; i++)
        {
            Transform found = FindChildRecursive(transform, names[i]);
            if (found != null)
                return found;
        }

        return null;
    }

    protected virtual string[] GetMuzzleParentNames()
    {
        return new[] { "MuzzlePoint" };
    }

    Vector3 EstimateMuzzleWorldPosition(Renderer target, Vector3 forward)
    {
        if (target == null) return transform.position + forward * 0.65f;

        // Find the actual forward-most mesh vertex. Renderer.bounds.center was visibly
        // wrong for angled first-person weapons because it lands near the receiver/hand.
        if (target is SkinnedMeshRenderer skinned)
        {
            Mesh baked = new Mesh();
            skinned.BakeMesh(baked);
            Vector3[] vertices = baked.vertices;
            Vector3 best = target.bounds.center;
            float bestProjection = float.NegativeInfinity;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 world = skinned.transform.TransformPoint(vertices[i]);
                float projection = Vector3.Dot(world, forward);
                if (projection <= bestProjection) continue;
                bestProjection = projection;
                best = world;
            }
            DestroyImmediate(baked);
            return best + forward * 0.025f;
        }

        if (target.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
        {
            Vector3[] vertices = filter.sharedMesh.vertices;
            Vector3 best = target.bounds.center;
            float bestProjection = float.NegativeInfinity;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 world = filter.transform.TransformPoint(vertices[i]);
                float projection = Vector3.Dot(world, forward);
                if (projection <= bestProjection) continue;
                bestProjection = projection;
                best = world;
            }
            return best + forward * 0.025f;
        }

        Bounds bounds = target.bounds;
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        float farthest = float.NegativeInfinity;
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
            farthest = Mathf.Max(farthest, Vector3.Dot(corner, forward));
        }
        return center + forward * (farthest - Vector3.Dot(center, forward) + 0.025f);
    }

    static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root.name == childName)
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindChildRecursive(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }

    void ResolveDefaultFxPrefabs()
    {
        // Generic fire effects are intentionally not auto-assigned as muzzle flashes.
    }

    void CreateProceduralMuzzleFlash(Vector3 position, Quaternion rotation)
    {
        if (CombatFxPool.Instance == null) return;
        GameObject flash = CombatFxPool.Instance.Rent(CombatFxPool.FxType.Muzzle);
        flash.transform.SetPositionAndRotation(position, rotation);
        flash.transform.localScale = Vector3.one;
        float effectScale = Mathf.Max(0.16f, muzzleFlashScale.x);

        flash.TryGetComponent(out ParticleSystem particles);
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.duration = muzzleFlashLifetime;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 0.045f;
        main.startSpeed = 4.5f * effectScale;
        main.startSize = 1.25f * effectScale;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.92f, 0.35f, 1f),
            new Color(1f, 0.35f, 0.05f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 10)
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 9f;
        shape.radius = 0.018f * effectScale;

        ParticleSystemRenderer renderer = flash.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = CombatFxPool.Instance.GetMaterial(new Color(1f, 0.65f, 0.08f, 1f));

        flash.TryGetComponent(out Light flashLight);
        flashLight.type = LightType.Point;
        flashLight.color = new Color(1f, 0.48f, 0.08f);
        flashLight.intensity = 4f + effectScale * 12f;
        flashLight.range = 2.8f + effectScale * 4f;
        flashLight.shadows = LightShadows.None;

        particles.Play(true);
        CombatFxPool.Instance.ReleaseAfter(flash, CombatFxPool.FxType.Muzzle, muzzleFlashLifetime + 0.1f);
    }

    void SpawnTracer(Vector3 start, Vector3 end)
    {
        if (CombatFxPool.Instance == null) return;
        GameObject tracer = CombatFxPool.Instance.Rent(CombatFxPool.FxType.Tracer);
        tracer.TryGetComponent(out LineRenderer line);
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = tracerWidth;
        line.endWidth = tracerWidth * 0.18f;
        line.numCapVertices = 2;
        line.startColor = tracerColor;
        Color faded = tracerColor;
        faded.a = 0.18f;
        line.endColor = faded;
        line.sharedMaterial = CombatFxPool.Instance.GetMaterial(tracerColor);
        CombatFxPool.Instance.ReleaseAfter(tracer, CombatFxPool.FxType.Tracer, tracerDuration);
    }

    void CreateProceduralImpact(RaycastHit hit, Color color)
    {
        if (CombatFxPool.Instance == null) return;
        GameObject impact = CombatFxPool.Instance.Rent(CombatFxPool.FxType.Impact);
        impact.transform.SetPositionAndRotation(
            hit.point + hit.normal * 0.02f,
            Quaternion.LookRotation(hit.normal, Vector3.up));

        impact.TryGetComponent(out ParticleSystem particles);
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.25f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.42f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.11f);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 22)
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.08f;

        ParticleSystemRenderer renderer = impact.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = CombatFxPool.Instance.GetMaterial(color);

        Light impactLight = impact.GetComponent<Light>();
        if (impactLight == null) impactLight = impact.AddComponent<Light>();
        impactLight.type = LightType.Point;
        impactLight.color = color;
        impactLight.intensity = 2.2f;
        impactLight.range = 1.35f;
        impactLight.shadows = LightShadows.None;

        particles.Play(true);
        CombatFxPool.Instance.ReleaseAfter(impact, CombatFxPool.FxType.Impact, hitEffectLifetime);
    }

    void PlayFallbackShotSound()
    {
        if (_weaponAudioSource == null) return;
        _weaponAudioSource.spatialBlend = 0f;
        _weaponAudioSource.PlayOneShot(GetFallbackShotClip(), 0.35f);
    }

    static AudioClip GetFallbackShotClip()
    {
        if (_fallbackShotClip != null)
            return _fallbackShotClip;

        const int sampleRate = 22050;
        const int length = sampleRate / 12;
        float[] samples = new float[length];
        uint seed = 123456789;

        for (int i = 0; i < samples.Length; i++)
        {
            seed = seed * 1103515245 + 12345;
            float noise = ((seed / 65536u) % 32768u) / 16384f - 1f;
            float envelope = 1f - (float)i / samples.Length;
            samples[i] = noise * envelope * envelope * 0.8f;
        }

        _fallbackShotClip = AudioClip.Create("Fallback Gunshot", length, 1, sampleRate, false);
        _fallbackShotClip.SetData(samples, 0);
        return _fallbackShotClip;
    }
}
