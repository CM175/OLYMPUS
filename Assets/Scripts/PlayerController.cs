using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 6f;
    public float crouchSpeed = 3f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.8f;

    [Header("Crouch")]
    public float standingHeight = 2f;
    public float crouchHeight = 1f;
    public float crouchTransitionSpeed = 8f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    /// <summary>
    /// Runtime speed multiplier set by abilities and temporary buffs.
    /// 1 = normal speed. Resets to 1 when the buff expires.
    /// </summary>
    public float SpeedMultiplier { get; set; } = 1f;

    private CharacterController _controller;
    private InputSystem_Actions _input;
    private Vector2 _moveInput;
    private Vector3 _velocity;
    private bool _isGrounded;
    private bool _isCrouching;
    private bool _jumpQueued;

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
        _input.Player.Move.performed -= OnMove;
        _input.Player.Move.canceled -= OnMove;

        _input.Player.Jump.performed -= OnJump;
        _input.Player.Crouch.performed -= OnCrouch;
        _input.Player.Crouch.canceled -= OnCrouchReleased;

        _input.Player.Disable();

        _moveInput = Vector2.zero;
        _jumpQueued = false;
    }

    private void OnMove(InputAction.CallbackContext ctx)
    {
        _moveInput = ctx.ReadValue<Vector2>();
    }

    private void OnJump(InputAction.CallbackContext ctx)
    {
        if (_isGrounded)
        {
            _jumpQueued = true;
        }
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
        HandleMovement();
        HandleGravityAndJump();
    }

    public void ApplyImpulse(Vector3 force)
    {
        // Adding to the current velocity creates a smooth transition
        // rather than an instant teleport.
        _velocity += force;
    }

    // Inside PlayerController.cs

    private void HandleGroundCheck()
    {
        bool wasGrounded = _isGrounded; // Store the state from last frame
        _isGrounded = Physics.CheckSphere(GetGroundCheckPosition(), groundDistance, groundMask);

        if (_isGrounded)
        {
            // If we just landed this frame...
            if (!wasGrounded)
            {
                OnLand();
            }

            if (_velocity.y < 0f)
            {
                _velocity.y = -2f; // Keep the player snapped to ground
            }
        }
    }

    private void OnLand()
    {
        // Zero out horizontal velocity (X and Z) so the leap momentum stops
        _velocity.x = 0f;
        _velocity.z = 0f;
        
        // Optional: Reset any leap-specific flags if you added them
        Debug.Log("Landed: Resetting horizontal momentum.");
    }

    private void HandleMovement()
    {
        float baseSpeed = _isCrouching ? crouchSpeed : walkSpeed;
        float speed = baseSpeed * Mathf.Max(0f, SpeedMultiplier);

        Vector3 move = transform.right * _moveInput.x
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
        {
            return groundCheck.position;
        }

        return transform.position + _controller.center - Vector3.up * (_controller.height / 2f);
    }
}
