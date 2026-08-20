using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class BulletPool : MonoBehaviour
{
    private const string DefaultBulletPrefabPath =
        "Assets/GLASSOFCOINS - FPS low poly fps/bullets/bullet_prefabs/7.62x51_Bullet_prefab.prefab";

    private static BulletPool _instance;

    public GameObject bulletPrefab;
    public int initialSize = 32;

    private readonly Queue<BulletProjectile> _pool = new Queue<BulletProjectile>();

    public static BulletPool Instance
    {
        get
        {
            if (_instance == null)
            {
                BulletPool existing = FindFirstObjectByType<BulletPool>();
                if (existing != null)
                {
                    _instance = existing;
                }
                else
                {
                    GameObject go = new GameObject("BulletPool");
                    _instance = go.AddComponent<BulletPool>();
                }
            }

            return _instance;
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        ResolveDefaultPrefab();
        Prewarm();
    }

    public void Spawn(Vector3 position, Quaternion rotation, Vector3 direction, float speed, float maxDistance)
    {
        BulletProjectile bullet = GetBullet();
        bullet.gameObject.SetActive(true);
        bullet.Launch(this, position, rotation, direction, speed, maxDistance);
    }

    public void Release(BulletProjectile bullet)
    {
        bullet.gameObject.SetActive(false);
        bullet.transform.SetParent(transform);
        _pool.Enqueue(bullet);
    }

    void Prewarm()
    {
        for (int i = _pool.Count; i < initialSize; i++)
        {
            BulletProjectile bullet = CreateBullet();
            bullet.gameObject.SetActive(false);
            _pool.Enqueue(bullet);
        }
    }

    BulletProjectile GetBullet()
    {
        if (_pool.Count > 0)
            return _pool.Dequeue();

        return CreateBullet();
    }

    BulletProjectile CreateBullet()
    {
        GameObject go = bulletPrefab != null
            ? Instantiate(bulletPrefab, transform)
            : CreateFallbackBullet();

        BulletProjectile projectile = go.GetComponent<BulletProjectile>();
        if (projectile == null)
            projectile = go.AddComponent<BulletProjectile>();

        return projectile;
    }

    GameObject CreateFallbackBullet()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "FallbackBullet";
        go.transform.SetParent(transform);
        go.transform.localScale = new Vector3(0.035f, 0.035f, 0.18f);

        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        return go;
    }

    void ResolveDefaultPrefab()
    {
        if (bulletPrefab != null)
            return;

#if UNITY_EDITOR
        bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultBulletPrefabPath);
#endif
    }
}
