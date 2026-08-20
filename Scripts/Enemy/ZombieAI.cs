using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
public class ZombieAI : MonoBehaviour
{
    public enum TargetMode { Player, Generator }
    public static IReadOnlyList<ZombieAI> ActiveZombies => _activeZombies;
    private static readonly List<ZombieAI> _activeZombies = new();
    public float detectionRange = 60f;
    public float attackRange = 1.65f;
    [Min(0f)] public float objectiveAttackRangePadding = 1f;
    public float attackDamage = 10f;
    public float attackCooldown = 1.05f;
    [Min(0f)] public float hitStunDuration = 0.42f;
    public float repathInterval = 0.16f;
    [Header("Route Variation")]
    public float routeVariationRadius = 5.5f;
    public float waypointLookAhead = 10f;
    public float waypointRefreshInterval = 1.35f;
    public float directApproachDistance = 6f;
    public Transform visualRoot;
    public AudioClip[] idleSounds;
    public AudioClip[] attackSounds;
    public int maxSimultaneousIdleVoices = 6;
    public int maxSimultaneousCombatVoices = 10;

    private NavMeshAgent _agent;
    private EnemyHealth _health;
    private PlayerHealth _playerTarget;
    private DefendObjective _objectiveTarget;
    private Collider _objectiveCollider;
    private Transform _activeTarget;
    private IDamageable _activeDamageable;
    private AudioSource _audioSource;
    private Animator _animator;
    private float _nextAttackTime;
    private float _nextRepathTime;
    private float _nextIdleSoundTime;
    private Vector3 _visualBasePosition;
    private Quaternion _visualBaseRotation;
    private Vector3 _visualBaseScale;
    private Vector3 _configuredVisualPosition;
    private Quaternion _configuredVisualRotation;
    private float _attackPulse;
    private bool _prefersObjective;
    private TargetMode _targetMode = TargetMode.Player;
    private string _locomotionState = "Crawl";
    private string _currentAnimation;
    private float _animationLockUntil;
    private float _hitStunUntil;
    private bool _animatorPausedForHit;
    private Vector3 _routeWaypoint;
    private Transform _routeTarget;
    private float _nextWaypointTime;
    private float _routeSide = 1f;
    private float _routeMagnitude = 1f;
    private float _routePhase;
    private bool _hasRouteWaypoint;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _health = GetComponent<EnemyHealth>();
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource != null)
        {
            _audioSource.spatialBlend = 1f;
            _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            _audioSource.minDistance = 2.5f;
            _audioSource.maxDistance = 32f;
            _audioSource.dopplerLevel = 0f;
        }
        _animator = GetComponentInChildren<Animator>(true);
        if (visualRoot == null && transform.childCount > 0)
            visualRoot = transform.GetChild(0);
        if (visualRoot != null)
        {
            _visualBasePosition = visualRoot.localPosition;
            _visualBaseRotation = visualRoot.localRotation;
            _visualBaseScale = visualRoot.localScale;
            _configuredVisualPosition = _visualBasePosition;
            _configuredVisualRotation = _visualBaseRotation;
        }

        _agent.stoppingDistance = attackRange * 0.82f;
        _agent.autoBraking = true;
        _agent.updateRotation = true;
    }

    void OnEnable()
    {
        if (!_activeZombies.Contains(this)) _activeZombies.Add(this);
        _animationLockUntil = 0f;
        _hitStunUntil = 0f;
        _animatorPausedForHit = false;
        _currentAnimation = string.Empty;
        _hasRouteWaypoint = false;
        _routeTarget = null;
        _nextWaypointTime = 0f;
        if (_animator != null)
        {
            _animator.speed = 1f;
            _animator.Rebind();
            _animator.Update(0f);
        }
    }

    public void ConfigureDifficulty(int wave, int spawnIndex, bool boss = false, float speedMultiplier = 1f, float damageMultiplier = 1f)
    {
        bool runner = wave >= 2 && spawnIndex % 3 == 0;
        bool upright = wave >= 3 && spawnIndex % 2 == 1;
        bool brute = !boss && wave >= 4 && spawnIndex % 5 == 4;

        _agent.speed = (boss ? 3.25f : brute ? 3.6f : upright ? 5.35f : runner ? 4.85f : 3.45f + wave * 0.18f) * speedMultiplier;
        _agent.acceleration = runner || upright ? 20f : 15f;
        attackDamage = (boss ? 6f : brute ? 5f : 1.5f + wave * 0.75f) * damageMultiplier;
        attackCooldown = boss ? 1.15f : runner ? 0.78f : brute ? 1.3f : 1.0f;
        _locomotionState = boss || runner || upright ? "Run" : wave <= 1 ? "Crawl" : "Walk";

        float routeHash = Hash01(wave * 73856093 ^ (spawnIndex + 1) * 19349663);
        _routeSide = routeHash < 0.5f ? -1f : 1f;
        _routeMagnitude = Mathf.Lerp(0.58f, 1.12f, Hash01((spawnIndex + 7) * 83492791));
        _routePhase = Hash01(wave * 31 + spawnIndex * 17) * Mathf.PI * 2f;
        _agent.avoidancePriority = Mathf.Clamp(22 + Mathf.RoundToInt(routeHash * 58f), 0, 99);
        _hasRouteWaypoint = false;

        _configuredVisualPosition = _visualBasePosition;
        _configuredVisualRotation = _visualBaseRotation;
        if (visualRoot != null)
        {
            visualRoot.localPosition = _configuredVisualPosition;
            visualRoot.localRotation = _configuredVisualRotation;
            visualRoot.localScale = _visualBaseScale * (boss ? 1.7f : brute ? 1.25f : runner ? 0.93f : 1f);
        }
    }

    public void ConfigureTarget(TargetMode mode)
    {
        _targetMode = mode;
        _prefersObjective = mode == TargetMode.Generator;
        ResolveTargets();
    }

    void Start()
    {
        ResolveTargets();
        _nextIdleSoundTime = Time.time + Random.Range(3f, 7f);
    }

    void Update()
    {
        if (_health.IsDead)
            return;

        ResolveTargets();
        SelectActiveTarget();
        if (_activeTarget == null || _activeDamageable == null)
            return;

        if (Time.time < _hitStunUntil)
        {
            if (_agent.isOnNavMesh) _agent.isStopped = true;
            return;
        }
        if (_animatorPausedForHit)
        {
            _animatorPausedForHit = false;
            if (_animator != null) _animator.speed = 1f;
            _currentAnimation = string.Empty;
        }

        bool targetingObjective = _activeTarget == _objectiveTarget?.transform;
        _agent.stoppingDistance = targetingObjective ? 0.2f : attackRange * 0.82f;
        float distance = Vector3.Distance(transform.position, _activeTarget.position);
        if (distance > detectionRange)
        {
            if (_agent.isOnNavMesh) _agent.isStopped = true;
            AnimateVisual(false);
            return;
        }

        float attackDistance = targetingObjective && _objectiveCollider != null
            ? Vector3.Distance(transform.position, _objectiveCollider.ClosestPoint(transform.position))
            : distance;
        float effectiveAttackRange = targetingObjective ? attackRange + objectiveAttackRangePadding : attackRange;
        if (attackDistance <= effectiveAttackRange)
        {
            if (_agent.isOnNavMesh) _agent.isStopped = true;
            FaceTarget();
            TryAttack();
        }
        else
        {
            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                if (Time.time >= _nextRepathTime)
                {
                    _agent.SetDestination(GetRouteDestination(distance));
                    _nextRepathTime = Time.time + repathInterval;
                }
            }
        }

        if (Time.time >= _nextIdleSoundTime)
        {
            PlayRandom(idleSounds, 0.42f, maxSimultaneousIdleVoices);
            _nextIdleSoundTime = Time.time + Random.Range(5f, 9f);
        }

        AnimateVisual(_agent.enabled && _agent.velocity.sqrMagnitude > 0.1f);
    }

    void TryAttack()
    {
        if (Time.time < _nextAttackTime)
            return;
        _nextAttackTime = Time.time + attackCooldown;
        _attackPulse = 1f;
        if (_locomotionState == "Crawl")
            PlayState("Crawl", 0.04f);
        else
            PlayOneShot("Attack", 0.72f);
        _activeDamageable.TakeDamage(attackDamage);
        PlayRandom(attackSounds, 0.75f, maxSimultaneousCombatVoices);
    }

    Vector3 GetRouteDestination(float targetDistance)
    {
        if (_routeTarget != _activeTarget)
        {
            _routeTarget = _activeTarget;
            _hasRouteWaypoint = false;
        }

        if (targetDistance <= directApproachDistance)
            return GetDirectTargetDestination();

        bool reachedWaypoint = _hasRouteWaypoint && (transform.position - _routeWaypoint).sqrMagnitude < 3.2f * 3.2f;
        if (!_hasRouteWaypoint || reachedWaypoint || Time.time >= _nextWaypointTime)
        {
            Vector3 towardTarget = _activeTarget.position - transform.position;
            towardTarget.y = 0f;
            if (towardTarget.sqrMagnitude < 0.01f) return GetDirectTargetDestination();
            towardTarget.Normalize();

            Vector3 lateral = Vector3.Cross(Vector3.up, towardTarget);
            float sway = Mathf.Sin(Time.time * 0.82f + _routePhase);
            float side = Mathf.Abs(sway) < 0.22f ? _routeSide : Mathf.Sign(sway);
            float lateralDistance = routeVariationRadius * _routeMagnitude * Mathf.Lerp(0.62f, 1f, Mathf.Abs(sway));
            float forwardDistance = Mathf.Min(waypointLookAhead, targetDistance * 0.55f);
            Vector3 candidate = transform.position + towardTarget * forwardDistance + lateral * (side * lateralDistance);

            _routeWaypoint = NavMesh.SamplePosition(candidate, out NavMeshHit hit, 4.5f, NavMesh.AllAreas)
                ? hit.position
                : GetDirectTargetDestination();
            _hasRouteWaypoint = true;
            _nextWaypointTime = Time.time + waypointRefreshInterval * Mathf.Lerp(0.82f, 1.22f, _routeMagnitude - 0.5f);
        }

        return _routeWaypoint;
    }

    Vector3 GetDirectTargetDestination()
    {
        if (_activeTarget == _objectiveTarget?.transform && _objectiveCollider != null)
        {
            Vector3 surfacePoint = _objectiveCollider.ClosestPoint(transform.position);
            if (NavMesh.SamplePosition(surfacePoint, out NavMeshHit hit, attackRange + _agent.radius, NavMesh.AllAreas))
                return hit.position;
            return surfacePoint;
        }

        return _activeTarget.position;
    }

    static float Hash01(int value)
    {
        uint x = unchecked((uint)value);
        x ^= x >> 16;
        x *= 0x7feb352d;
        x ^= x >> 15;
        x *= 0x846ca68b;
        x ^= x >> 16;
        return (x & 0x00ffffff) / 16777215f;
    }

    void FaceTarget()
    {
        Vector3 direction = _activeTarget.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 12f * Time.deltaTime);
    }

    void ResolveTargets()
    {
        if (_playerTarget == null || _playerTarget.IsDead)
            _playerTarget = FindFirstObjectByType<PlayerHealth>();
        if (_objectiveTarget == null || _objectiveTarget.IsDestroyed)
        {
            _objectiveTarget = FindFirstObjectByType<DefendObjective>();
            _objectiveCollider = _objectiveTarget != null ? _objectiveTarget.GetComponent<Collider>() : null;
        }
    }

    void SelectActiveTarget()
    {
        bool playerAvailable = _playerTarget != null && !_playerTarget.IsDead;
        bool objectiveAvailable = _objectiveTarget != null && !_objectiveTarget.IsDestroyed;

        if (!playerAvailable && !objectiveAvailable)
        {
            _activeTarget = null;
            _activeDamageable = null;
            return;
        }

        if (!objectiveAvailable)
        {
            _activeTarget = _playerTarget.transform;
            _activeDamageable = _playerTarget;
            return;
        }

        if (!playerAvailable)
        {
            _activeTarget = _objectiveTarget.transform;
            _activeDamageable = _objectiveTarget;
            return;
        }

        float playerDistance = Vector3.Distance(transform.position, _playerTarget.transform.position);
        bool playerIsImmediateThreat = playerDistance <= 7f || _targetMode == TargetMode.Player || !_prefersObjective;
        _activeTarget = playerIsImmediateThreat ? _playerTarget.transform : _objectiveTarget.transform;
        _activeDamageable = playerIsImmediateThreat ? (IDamageable)_playerTarget : _objectiveTarget;
    }

    void AnimateVisual(bool moving)
    {
        if (visualRoot == null)
            return;

        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            if (Time.time < _animationLockUntil) return;
            PlayLoop(_locomotionState == "Crawl" ? "Crawl" : moving ? _locomotionState : "Idle");
            return;
        }

        float speed = _agent.enabled ? _agent.velocity.magnitude : 0f;
        float phase = Time.time * Mathf.Lerp(4f, 8f, Mathf.InverseLerp(0f, 4.5f, speed));
        float bob = moving ? Mathf.Abs(Mathf.Sin(phase)) * 0.045f : Mathf.Sin(phase * 0.35f) * 0.012f;
        float sway = moving ? Mathf.Sin(phase * 0.5f) * 3.5f : Mathf.Sin(phase * 0.25f) * 1.2f;

        _attackPulse = Mathf.MoveTowards(_attackPulse, 0f, Time.deltaTime * 3.6f);
        float lunge = Mathf.Sin(_attackPulse * Mathf.PI) * 0.22f;
        visualRoot.localPosition = _configuredVisualPosition + Vector3.up * bob + Vector3.forward * lunge;
        visualRoot.localRotation = _configuredVisualRotation * Quaternion.Euler(0f, 0f, sway - lunge * 22f);
    }

    public void PlayHitAnimation()
    {
        float duration = Mathf.Max(0.1f, hitStunDuration);
        _hitStunUntil = Mathf.Max(_hitStunUntil, Time.time + duration);
        if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;

        if (_locomotionState == "Crawl")
        {
            _animationLockUntil = Mathf.Max(_animationLockUntil, _hitStunUntil);
            if (_animator != null)
            {
                _animator.speed = 0f;
                _animatorPausedForHit = true;
            }
            return;
        }

        PlayOneShot("Hit", duration);
    }

    public bool PlayDeathAnimation()
    {
        if (_animator != null) _animator.speed = 1f;
        _animatorPausedForHit = false;
        _animationLockUntil = float.PositiveInfinity;
        return PlayState("Death", 0.05f);
    }

    void PlayLoop(string state)
    {
        if (_currentAnimation == state) return;
        PlayState(state, 0.12f);
    }

    void PlayOneShot(string state, float duration)
    {
        if (_animator == null || _animator.runtimeAnimatorController == null) return;
        _animationLockUntil = Mathf.Max(_animationLockUntil, Time.time + duration);
        PlayState(state, 0.06f);
    }

    bool PlayState(string state, float fade)
    {
        if (_animator == null) return false;
        int hash = Animator.StringToHash("Base Layer." + state);
        if (!_animator.HasState(0, hash)) return false;
        _currentAnimation = state;
        _animator.CrossFade(hash, fade);
        return true;
    }

    void PlayRandom(AudioClip[] clips, float volume, int maxVoices = 10)
    {
        if (_audioSource == null || clips == null || clips.Length == 0)
            return;
        int playing = 0;
        foreach (ZombieAI zombie in _activeZombies)
            if (zombie != null && zombie._audioSource != null && zombie._audioSource.isPlaying) playing++;
        float listenerDistance = Camera.main != null ? Vector3.Distance(Camera.main.transform.position, transform.position) : 0f;
        if (playing >= maxVoices && listenerDistance > 7f) return;
        _audioSource.pitch = Random.Range(0.96f, 1.04f);
        _audioSource.PlayOneShot(clips[Random.Range(0, clips.Length)], volume);
    }

    void OnDisable()
    {
        _activeZombies.Remove(this);
        if (visualRoot == null)
            return;
        visualRoot.localPosition = _configuredVisualPosition;
        visualRoot.localRotation = _configuredVisualRotation;
        visualRoot.localScale = _visualBaseScale;
        _currentAnimation = string.Empty;
    }
}
