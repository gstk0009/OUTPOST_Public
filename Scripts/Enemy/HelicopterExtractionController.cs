using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class HelicopterExtractionController : MonoBehaviour
{
    public Transform helicopter;
    public Transform approachPoint;
    public Transform landingPoint;
    public Transform doorApproachPoint;
    public Transform boardingPoint;
    public Transform departurePoint;
    public Camera cinematicCamera;
    public float approachDuration = 5.5f;
    public float doorApproachDuration = 1.15f;
    public float boardingDuration = 0.8f;
    public float departureDuration = 6.5f;
    public float departureHoldDuration = 1.2f;
    public float finalWaveStartRadius = 9f;
    public float rotorSpeed = 900f;
    public AudioSource engineSource;

    private Transform[] _rotors;
    private bool _running;

    void Awake()
    {
        if (helicopter != null)
            _rotors = System.Array.FindAll(helicopter.GetComponentsInChildren<Transform>(true),
                item => item.name.ToLowerInvariant().Contains("rotor") || item.name.ToLowerInvariant().Contains("blade"));
        else _rotors = System.Array.Empty<Transform>();
        if (cinematicCamera != null) cinematicCamera.gameObject.SetActive(false);
    }

    public bool IsPlayerAtLandingZone(PlayerHealth player)
    {
        if (player == null || landingPoint == null) return false;
        Vector3 offset = player.transform.position - landingPoint.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= finalWaveStartRadius * finalWaveStartRadius;
    }

    void Update()
    {
        if (!_running) return;
        foreach (Transform rotor in _rotors)
            if (rotor != null) rotor.Rotate(0f, rotorSpeed * Time.deltaTime, 0f, Space.Self);
    }

    public async UniTask<bool> PlayExtractionAsync(PlayerHealth player, CancellationToken token)
    {
        if (helicopter == null || landingPoint == null || departurePoint == null) return true;
        _running = true;
        if (approachPoint != null) helicopter.SetPositionAndRotation(approachPoint.position, approachPoint.rotation);
        helicopter.gameObject.SetActive(true);
        engineSource?.Play();
        if (!await MoveAsync(helicopter, helicopter.position, landingPoint.position, approachDuration, token)) return false;

        if (player != null)
        {
            PlayerController movement = player.GetComponent<PlayerController>();
            WeaponManager weapons = player.GetComponent<WeaponManager>();
            MouseLook look = player.GetComponentInChildren<MouseLook>();
            if (movement != null) movement.enabled = false;
            if (weapons != null) weapons.enabled = false;
            if (look != null) look.enabled = false;
            CrosshairUI crosshair = FindFirstObjectByType<CrosshairUI>();
            if (crosshair != null) crosshair.HideCrosshair(true);
            SniperScopeUI scope = FindFirstObjectByType<SniperScopeUI>();
            if (scope != null)
            {
                scope.HideScope();
                if (scope.crosshairObject != null) scope.crosshairObject.SetActive(false);
            }
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

            Vector3 doorPosition = doorApproachPoint != null ? doorApproachPoint.position : landingPoint.position - helicopter.right * 4f;
            FaceHorizontal(player.transform, doorPosition);
            if (!await MoveAsync(player.transform, player.transform.position, doorPosition, doorApproachDuration, token)) return false;

            Vector3 insidePosition = boardingPoint != null ? boardingPoint.position : landingPoint.position - helicopter.right * 2.2f;
            FaceHorizontal(player.transform, insidePosition);
            if (!await MoveAsync(player.transform, doorPosition, insidePosition, boardingDuration, token)) return false;
            ActivateCinematicCamera(player);
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        }

        bool departed = await DepartWithCameraAsync(token);
        if (departed && departureHoldDuration > 0f)
        {
            bool holdCancelled = await UniTask.Delay((int)(departureHoldDuration * 1000f), cancellationToken: token).SuppressCancellationThrow();
            if (holdCancelled) return false;
        }
        _running = false;
        engineSource?.Stop();
        return departed;
    }

    void ActivateCinematicCamera(PlayerHealth player)
    {
        Camera gameplayCamera = player != null ? player.GetComponentInChildren<Camera>(true) : Camera.main;
        if (cinematicCamera == null)
        {
            GameObject cameraObject = new("ExtractionCinematicCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.position = landingPoint.position + new Vector3(15f, 7f, -13f);
            cinematicCamera = cameraObject.GetComponent<Camera>();
            cinematicCamera.fieldOfView = 54f;
        }

        if (gameplayCamera != null && gameplayCamera != cinematicCamera)
        {
            AudioListener listener = gameplayCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
            gameplayCamera.enabled = false;
        }
        cinematicCamera.gameObject.SetActive(true);
        cinematicCamera.transform.LookAt(helicopter.position + Vector3.up * 2f);
    }

    async UniTask<bool> DepartWithCameraAsync(CancellationToken token)
    {
        Vector3 helicopterFrom = helicopter.position;
        Quaternion rotationFrom = helicopter.rotation;
        Quaternion rotationTo = departurePoint.rotation;
        Vector3 curveControl = helicopterFrom + new Vector3(24f, 15f, 25f);
        Vector3 cameraFrom = cinematicCamera != null ? cinematicCamera.transform.position : Vector3.zero;
        Vector3 cameraTo = cameraFrom + new Vector3(3f, 4f, 2f);
        float elapsed = 0f;
        while (elapsed < departureDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, departureDuration)));
            float inverse = 1f - t;
            helicopter.position = inverse * inverse * helicopterFrom +
                                  2f * inverse * t * curveControl +
                                  t * t * departurePoint.position;
            float bank = Mathf.Sin(t * Mathf.PI);
            helicopter.rotation = Quaternion.Slerp(rotationFrom, rotationTo, t) *
                                  Quaternion.Euler(-6f * bank, 0f, 11f * bank);
            if (cinematicCamera != null)
            {
                cinematicCamera.transform.position = Vector3.Lerp(cameraFrom, cameraTo, t);
                cinematicCamera.transform.LookAt(helicopter.position + Vector3.up * 1.7f);
            }
            await UniTask.Yield(PlayerLoopTiming.Update);
            if (token.IsCancellationRequested) return false;
        }
        helicopter.SetPositionAndRotation(departurePoint.position, departurePoint.rotation);
        return true;
    }

    static void FaceHorizontal(Transform target, Vector3 point)
    {
        Vector3 direction = point - target.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f) target.rotation = Quaternion.LookRotation(direction);
    }

    static async UniTask<bool> MoveAsync(Transform target, Vector3 from, Vector3 to, float duration, CancellationToken token)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration)));
            target.position = Vector3.Lerp(from, to, t);
            await UniTask.Yield(PlayerLoopTiming.Update);
            if (token.IsCancellationRequested) return false;
        }
        target.position = to;
        return true;
    }
}
