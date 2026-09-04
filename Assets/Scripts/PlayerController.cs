using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour, IMovementController
{
    [Header("Movement")]
    public float walkSpeed = 6f;
    public float crouchSpeed = 3f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.8f;
    public float horizontalDrag = 8f;

    [Header("Crouch")]
    public float standingHeight = 2f;
    public float crouchHeight = 1f;
    public float crouchTransitionSpeed = 8f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    public float SpeedMultiplier { get; set; } = 1f;

    private CharacterController _controller;
    private InputSystem_Actions _input;

    private Vector2 _moveInput;
    private bool _isCrouching;
    private bool _jumpQueued;

    private bool _stunned = false;
    public bool IsStunned => _stunned;

    public bool IsDashing { get; set; } = false;

    public bool IsGrounded { get; set; }
    
    public bool IsUnderExternalForce { get; set; } = false;
    private Coroutine _slowCoroutine;
    private float _preSlowMultiplier = 1f;

    // =========================
    // NEW PHYSICS MODEL
    // =========================
    private Vector3 _internalVelocity; // input + gravity
    private Vector3 _externalVelocity; // abilities (knockback, slam, etc.)
    private float _gravityMultiplier = 1f;

    public void ApplyImpulseForce(Vector3 force)
    {
        _externalVelocity += force;
    }

    public void HaltVelocity()
    {
        _internalVelocity = Vector3.zero;
        _externalVelocity = Vector3.zero;
    }

    public void Move(Vector3 displacement)
    {
        if (_controller.enabled)
            _controller.Move(displacement);
    }

    public void Teleport(Vector3 position)
    {
        if (_controller == null) return;

        // 1. Clear all accumulated frame velocities so you don't keep flying
        HaltVelocity();

        // 2. CharacterController must be disabled to safely set world transform coordinates
        _controller.enabled = false;
        transform.position = position;
        _controller.enabled = true;
    }

    public Vector3 GetAimDirection()
    {
        Camera cam = Camera.main;
        return cam != null ? cam.transform.forward : transform.forward;
    }

    public void Stun(float duration)
    {
        StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        HaltVelocity();
        _stunned = true;
        AbilityManager abilityManager = GetComponent<AbilityManager>();
        if (abilityManager != null)
        {
            abilityManager.InterruptAllAbilities();
            abilityManager.enabled = false;
        }
        yield return new WaitForSeconds(duration);
        if (abilityManager != null)
            abilityManager.enabled = true;
        _stunned = false;
    }

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _input = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        _input.Player.Enable();

        _input.Player.Move.performed += OnMove;
        _input.Player.Move.canceled += OnMove;

        _input.Player.Jump.performed += OnJump;
        _input.Player.Crouch.performed += OnCrouch;
        _input.Player.Crouch.canceled += OnCrouchReleased;
    }

    private void OnDisable()
    {
        _input.Player.Disable();
    }

    private void OnMove(InputAction.CallbackContext ctx)
    {
        _moveInput = ctx.ReadValue<Vector2>();
    }

    private void OnJump(InputAction.CallbackContext ctx)
    {
        if (IsGrounded)
            _jumpQueued = true;
    }

    private void OnCrouch(InputAction.CallbackContext ctx)
    {
        _isCrouching = true;
    }

    private void OnCrouchReleased(InputAction.CallbackContext ctx)
    {
        _isCrouching = false;
    }

    private void Update()
    {
        HandleGroundCheck();
        HandleCrouchHeight();
        ExecuteMovement();
    }

    private void ExecuteMovement()
    {
        Vector3 inputMove = Vector3.zero;

        if (!IsStunned && !IsDashing)
        {
            float baseSpeed = _isCrouching ? crouchSpeed : walkSpeed;
            float speed = baseSpeed * SpeedMultiplier;

            Vector3 moveDir = transform.right * _moveInput.x + transform.forward * _moveInput.y;
            inputMove = moveDir * speed;
        }
        else
        {
            inputMove = Vector3.zero;
        }

        // Gravity → internal only
        if (!IsGrounded)
        {
            _internalVelocity.y += gravity * _gravityMultiplier * Time.deltaTime;
            _externalVelocity.y += gravity * _gravityMultiplier * Time.deltaTime;
        }

        // Jump
        if (_jumpQueued && IsGrounded && !IsStunned)
        {
            _internalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _jumpQueued = false;
        }

        // External drag
        Vector3 extHorizontal = new Vector3(_externalVelocity.x, 0f, _externalVelocity.z);
        extHorizontal = Vector3.Lerp(extHorizontal, Vector3.zero, horizontalDrag * Time.deltaTime);
        _externalVelocity = new Vector3(extHorizontal.x, _externalVelocity.y, extHorizontal.z);

        Vector3 finalMove =
            (inputMove * Time.deltaTime) +
            (_internalVelocity * Time.deltaTime) +
            (_externalVelocity * Time.deltaTime);

        if (_controller.enabled)
            _controller.Move(finalMove);
    }

    private void HandleGroundCheck()
    {
        bool wasGrounded = IsGrounded;
        IsGrounded = Physics.CheckSphere(GetGroundCheckPosition(), groundDistance, groundMask);

        if (IsGrounded && !wasGrounded)
        {
            _internalVelocity.y = 0f;
            _externalVelocity.y = 0f;
        }
    }

    private void HandleCrouchHeight()
    {
        float targetHeight = _isCrouching ? crouchHeight : standingHeight;
        targetHeight = Mathf.Max(_controller.radius * 2f, targetHeight);

        _controller.height = Mathf.Lerp(
            _controller.height,
            targetHeight,
            Time.deltaTime * crouchTransitionSpeed);

        Vector3 center = _controller.center;
        center.y = _controller.height / 2f;
        _controller.center = center;
    }

    private Vector3 GetGroundCheckPosition()
    {
        if (groundCheck != null)
            return groundCheck.position;

        return transform.position + _controller.center - Vector3.up * (_controller.height / 2f);
    }

    
    public void SetGravityMultiplier(float value)
    {
        _gravityMultiplier = value;
    }
    public void HaltVerticalVelocity()
    {
        _internalVelocity.y = 0f;
        _externalVelocity.y = 0f;
    }

    public void ApplySlow(float slowMultiplier, float duration, bool decaying = false)
    {
        if (_slowCoroutine != null)
        {
            StopCoroutine(_slowCoroutine);
            SpeedMultiplier = _preSlowMultiplier;
        }

        _slowCoroutine = StartCoroutine(SlowRoutine(slowMultiplier, duration, decaying));
    }

    private IEnumerator SlowRoutine(float slowMultiplier, float duration, bool decaying)
    {
        _preSlowMultiplier = SpeedMultiplier;
        float targetSpeed = _preSlowMultiplier * slowMultiplier;
        SpeedMultiplier = targetSpeed;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (decaying)
            {
                SpeedMultiplier = Mathf.Lerp(targetSpeed, _preSlowMultiplier, elapsed / duration);
            }
            yield return null;
        }

        SpeedMultiplier = _preSlowMultiplier;
        _slowCoroutine = null;
    }
}