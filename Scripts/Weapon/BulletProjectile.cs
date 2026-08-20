using UnityEngine;

public class BulletProjectile : MonoBehaviour
{
    private BulletPool _pool;
    private Vector3 _direction;
    private Vector3 _startPosition;
    private float _speed;
    private float _maxDistance;

    public void Launch(BulletPool pool, Vector3 position, Quaternion rotation, Vector3 direction, float speed, float maxDistance)
    {
        _pool = pool;
        _direction = direction.normalized;
        _speed = speed;
        _maxDistance = maxDistance;
        _startPosition = position;

        transform.SetParent(null);
        transform.SetPositionAndRotation(position, rotation);
    }

    void Update()
    {
        transform.position += _direction * (_speed * Time.deltaTime);

        if (Vector3.Distance(_startPosition, transform.position) >= _maxDistance)
            _pool.Release(this);
    }
}
