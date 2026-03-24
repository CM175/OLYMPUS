using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed   = 6f;
    public float sprintSpeed = 10f;
    public float crouchSpeed = 3f;
    public float gravity     = -9.81f;
    public float jumpHeight  = 1.8f;

    [Header("Crouch")]
    public float standingHeight      = 2f;
    public float crouchHeight        = 1f;
    public float crouchTransitionSpeed = 8f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float     groundDistance = 0.4f;
    public LayerMask groundMask;

    /// <summary>
    /// Runtime speed multiplier — set by AchillesFury, BattleCry, etc.
    /// 1 = normal speed. Resets to 1 when the buff expires.
    /// </summary>
    public float SpeedMultiplier { get; set; } = 1f;

    // ── Private state ────────────────────────────────────────────────────────

    private CharacterController  _controller;
    private InputSystem_Actions  _input;
    private Vector2  _moveInput;
    private Vector3  _velocity;
    private bool     _isGrounded;
    private bool     _isSprinting;
    private bool     _isCrouching;
    private bool     _jumpQueued;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _input      = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        _input.Player.Enable();

        _input.Player.Move.performed   += OnMove;
        _input.Player.Move.canceled    += OnMove;

        _input.Player.Sprint.performed += ctx => _isSprinting = true;
        _input.Player.Sprint.canceled  += ctx => _isSprinting = false;

        _input.Player.Jump.performed   += OnJump;
        _input.Player.Crouch.performed += OnCrouch;
        _input.Player.Crouch.canceled  += OnCrouchReleased;
    }

    private void OnDisable()
    {
        _input.Player.Move.performed   -= OnMove;
        _input.Player.Move.canceled    -= OnMove;

        _input.Player.Sprint.performed -= ctx => _isSprinting = true;
        _input.Player.Sprint.canceled  -= ctx => _isSprinting = false;

        _input.Player.Jump.performed   -= OnJump;
        _input.Player.Crouch.performed -= OnCrouch;
        _input.Player.Crouch.canceled  -= OnCrouchReleased;

        _input.Player.Disable();
    }

    // ── Input callbacks ──────────────────────────────────────────────────────

    private void OnMove(InputAction.CallbackContext ctx)
        => _moveInput = ctx.ReadValue<Vector2>();

    private void OnJump(InputAction.CallbackContext ctx)
    {
        if (_isGrounded) _jumpQueued = true;
    }

    private void OnCrouch(InputAction.CallbackContext ctx)        => _isCrouching = true;
    private void OnCrouchReleased(InputAction.CallbackContext ctx) => _isCrouching = false;

    // ── Update ───────────────────────────────────────────────────────────────

    private void Update()
    {
        HandleGroundCheck();
        HandleCrouchHeight();
        HandleMovement();
        HandleGravityAndJump();
    }

    private void HandleGroundCheck()
    {
        _isGrounded = Physics.CheckSphere(
            groundCheck.position, groundDistance, groundMask);

        if (_isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;
    }

    private void HandleMovement()
    {
        float baseSpeed = _isCrouching ? crouchSpeed
                        : _isSprinting ? sprintSpeed
                        : walkSpeed;

        // SpeedMultiplier is applied on top of the base speed
        float speed = baseSpeed * SpeedMultiplier;

        Vector3 move = transform.right   * _moveInput.x
                     + transform.forward * _moveInput.y;

        _controller.Move(move * speed * Time.deltaTime);
    }

    private void HandleGravityAndJump()
    {
        if (_jumpQueued && _isGrounded)
        {
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _jumpQueued = false;
        }

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }

    private void HandleCrouchHeight()
    {
        float targetHeight = _isCrouching ? crouchHeight : standingHeight;
        _controller.height = Mathf.Lerp(
            _controller.height, targetHeight,
            Time.deltaTime * crouchTransitionSpeed);

        Vector3 center = _controller.center;
        center.y = _controller.height / 2f;
        _controller.center = center;
    }
}