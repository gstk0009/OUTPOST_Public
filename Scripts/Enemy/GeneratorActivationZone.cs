using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class GeneratorActivationZone : MonoBehaviour
{
    public GameCycleDirector director;

    void Awake()
    {
        BoxCollider trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;
        if (director == null)
            director = FindFirstObjectByType<GameCycleDirector>();
    }

    void OnTriggerEnter(Collider other) => TryActivate(other);
    void OnTriggerStay(Collider other) => TryActivate(other);

    void TryActivate(Collider other)
    {
        if (director == null || !director.CanActivateGenerator)
            return;
        if (other.GetComponentInParent<PlayerController>() == null)
            return;
        director.ActivateGenerator();
    }
}
