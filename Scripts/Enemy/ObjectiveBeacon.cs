using UnityEngine;

public class ObjectiveBeacon : MonoBehaviour
{
    public Transform rotatingRing;
    public Light beaconLight;
    public float rotationSpeed = 42f;
    public float pulseSpeed = 2.4f;
    public float minIntensity = 4f;
    public float maxIntensity = 9f;

    void Update()
    {
        if (rotatingRing != null)
            rotatingRing.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
        if (beaconLight != null)
            beaconLight.intensity = Mathf.Lerp(minIntensity, maxIntensity,
                (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f);
    }
}
