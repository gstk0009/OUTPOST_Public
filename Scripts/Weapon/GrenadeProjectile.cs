using Cysharp.Threading.Tasks;
using UnityEngine;

public class GrenadeProjectile : MonoBehaviour
{
    public float explosionRadius = 5f;
    public float explosionDelay = 3f;
    public float damage = 100f;

    void OnEnable() => ExplodeAfterDelayAsync().Forget();

    async UniTaskVoid ExplodeAfterDelayAsync()
    {
        bool cancelled = await UniTask.Delay((int)(explosionDelay * 1000f), cancellationToken: this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
        if (cancelled) return;
        foreach (Collider hit in Physics.OverlapSphere(transform.position, explosionRadius))
        {
            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;
            float falloff = 1f - Mathf.Clamp01(Vector3.Distance(transform.position, hit.transform.position) / explosionRadius);
            damageable.TakeDamage(damage * falloff);
        }
        gameObject.SetActive(false);
    }
}
