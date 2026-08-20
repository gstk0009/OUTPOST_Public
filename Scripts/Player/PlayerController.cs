using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Feel")]
    public float moveSpeed = 5.2f;
    public float sprintSpeed = 7.2f;
    public float sneakSpeed = 2.25f;
    public float crouchSpeed = 2.8f;
    public float groundAcceleration = 38f;
    public float groundDeceleration = 46f;
    public float turnAccelerationMultiplier = 1.55f;
    [Range(0.5f, 1f)] public float strafeSpeedMultiplier = 0.94f;
    [Range(0.5f, 1f)] public float backwardSpeedMultiplier = 0.86f;
    [Range(0f, 1f)] public float airControl = 0.38f;

    [Header("Jump & Gravity")]
    public float jumpHeight = 1.15f;
    public float gravity = -25f;
    public float groundedStickForce = 4f;
    public float coyoteTime = 0.1f;
    public float jumpBufferTime = 0.12f;

    [Header("Crouch")]
    public float standHeight = 2f;
    public float crouchHeight = 1.15f;
    public float crouchTransitionSpeed = 12f;
    public LayerMask ceilingMask = ~0;
    public Transform viewTransform;

    [Header("State - Read Only")]
    public bool isGrounded;
    public bool isSprinting;
    public bool isSneaking;
    public bool isCrouching;
    public float PlanarSpeed => _planarVelocity.magnitude;
    public bool IsMoving => PlanarSpeed > 0.15f;
    public bool WasJustLanded { get; private set; }

    private CharacterController _controller;
    private Vector2 _moveInput;
    private Vector3 _planarVelocity;
    private float _verticalVelocity;
    private float _lastGroundedTime = float.NegativeInfinity;
    private float _lastJumpPressedTime = float.NegativeInfinity;
    private float _landTimer;
    private bool _wasGrounded;
    private Vector3 _standCenter;
    private float _viewStandY;
    private float _viewVelocity;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _controller.minMoveDistance = 0f;
        _controller.stepOffset = 0.36f;
        _controller.slopeLimit = 48f;
        _controller.skinWidth = 0.045f;
        _standCenter = _controller.center;
        standHeight = _controller.height;

        if (viewTransform == null && Camera.main != null && Camera.main.transform.IsChildOf(transform))
            viewTransform = Camera.main.transform;
        if (viewTransform != null)
            _viewStandY = viewTransform.localPosition.y;
    }

    void Update()
    {
        ReadInput();
        UpdateGroundState();
        UpdateCrouch();
        UpdateMovement();
        UpdateLandingState();
    }

    void ReadInput()
    {
        _moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (_moveInput.sqrMagnitude > 1f)
            _moveInput.Normalize();

        isSneaking = Input.GetKey(KeyCode.LeftAlt);
        isSprinting = Input.GetKey(KeyCode.LeftShift) && _moveInput.y > 0.1f && !isSneaking;

        bool wantsCrouch = Input.GetKey(KeyCode.LeftControl);
        if (!wantsCrouch && isCrouching && !CanStand())
            wantsCrouch = true;
        isCrouching = wantsCrouch;
        if (isCrouching)
            isSprinting = false;

        if (Input.GetButtonDown("Jump"))
            _lastJumpPressedTime = Time.time;
    }

    void UpdateGroundState()
    {
        isGrounded = _controller.isGrounded;
        if (isGrounded)
        {
            _lastGroundedTime = Time.time;
            if (_verticalVelocity < 0f)
                _verticalVelocity = -groundedStickForce;
        }

        bool bufferedJump = Time.time - _lastJumpPressedTime <= jumpBufferTime;
        bool canUseCoyote = Time.time - _lastGroundedTime <= coyoteTime;
        if (bufferedJump && canUseCoyote && !isCrouching)
        {
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _lastJumpPressedTime = float.NegativeInfinity;
            _lastGroundedTime = float.NegativeInfinity;
            isGrounded = false;
        }
    }

    void UpdateMovement()
    {
        float targetSpeed = isCrouching ? crouchSpeed : isSneaking ? sneakSpeed : isSprinting ? sprintSpeed : moveSpeed;
        if (_moveInput.y < -0.1f) targetSpeed *= backwardSpeedMultiplier;
        else if (Mathf.Abs(_moveInput.x) > Mathf.Abs(_moveInput.y)) targetSpeed *= strafeSpeedMultiplier;
        Vector3 wishDirection = transform.right * _moveInput.x + transform.forward * _moveInput.y;
        Vector3 targetVelocity = wishDirection * (targetSpeed * Mathf.Clamp01(_moveInput.magnitude));

        float acceleration = targetVelocity.sqrMagnitude > 0.01f ? groundAcceleration : groundDeceleration;
        if (targetVelocity.sqrMagnitude > 0.01f && _planarVelocity.sqrMagnitude > 0.01f &&
            Vector3.Dot(targetVelocity.normalized, _planarVelocity.normalized) < 0.55f)
            acceleration *= turnAccelerationMultiplier;
        if (!isGrounded)
            acceleration *= airControl;

        _planarVelocity = Vector3.MoveTowards(_planarVelocity, targetVelocity, acceleration * Time.deltaTime);
        _verticalVelocity += gravity * Time.deltaTime;

        CollisionFlags flags = _controller.Move((_planarVelocity + Vector3.up * _verticalVelocity) * Time.deltaTime);
        if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
            _verticalVelocity = 0f;
    }

    void UpdateCrouch()
    {
        float targetHeight = isCrouching ? crouchHeight : standHeight;
        float newHeight = Mathf.MoveTowards(_controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);
        float bottom = _standCenter.y - standHeight * 0.5f;

        _controller.height = newHeight;
        Vector3 center = _controller.center;
        center.y = bottom + newHeight * 0.5f;
        _controller.center = center;

        if (viewTransform != null)
        {
            float crouchOffset = (standHeight - crouchHeight) * 0.82f;
            float targetViewY = _viewStandY - (isCrouching ? crouchOffset : 0f);
            Vector3 local = viewTransform.localPosition;
            local.y = Mathf.SmoothDamp(local.y, targetViewY, ref _viewVelocity, 0.07f);
            viewTransform.localPosition = local;
        }
    }

    bool CanStand()
    {
        float radius = Mathf.Max(0.05f, _controller.radius - 0.03f);
        Vector3 worldCenter = transform.TransformPoint(_standCenter);
        float half = Mathf.Max(radius, standHeight * 0.5f - radius);
        Vector3 bottom = worldCenter + Vector3.down * half;
        Vector3 top = worldCenter + Vector3.up * half;
        Collider[] overlaps = Physics.OverlapCapsule(bottom, top, radius, ceilingMask, QueryTriggerInteraction.Ignore);
        foreach (Collider overlap in overlaps)
        {
            if (overlap != _controller && !overlap.transform.IsChildOf(transform))
                return false;
        }
        return true;
    }

    void UpdateLandingState()
    {
        if (!_wasGrounded && isGrounded)
            _landTimer = 0.14f;

        _wasGrounded = isGrounded;
        if (_landTimer > 0f)
            _landTimer -= Time.deltaTime;
        WasJustLanded = _landTimer > 0f;
    }

    public void OnLanded()
    {
        _landTimer = 0.14f;
    }
}
