using UnityEngine;
using System.Collections;
using System.Collections.Generic;
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

    /// <summary>
    /// Runtime speed multiplier set by abilities and temporary buffs.
    /// 1 = normal speed. Resets to 1 when the buff expires.
    /// </summary>
    public float SpeedMultiplier { get; set; } = 1f;

    private CharacterController _controller;
    private InputSystem_Actions _input;
    private Vector2 _moveInput;
    private Vector3 _velocity;
    public bool IsGrounded { get; set; }
    private bool _isCrouching;
    private bool _jumpQueued;
    private bool _stunned = false;
    public bool IsStunned => _stunned;
    public bool IsDashing { get; set; } = false;

    public void ApplyImpulseForce(Vector3 force)
    {
        _velocity += force; 
    }

    public void Move(Vector3 displacement)
    {
        if (_controller.enabled)
        {
            _controller.Move(displacement);
        }
    }
    public void Teleport(Vector3 position)
    {
        if (_controller.enabled)
        {
            _controller.Move(position);
        }
    }

    public void HaltVelocity()
    {
        SetVelocity(Vector3.zero);
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

        IsDashing = false;
        _stunned = false;
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
        if (IsGrounded)
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

    // Inside PlayerController.cs

    private void HandleGroundCheck()
    {
        bool wasGrounded = IsGrounded; // Store the state from last frame
        IsGrounded = Physics.CheckSphere(GetGroundCheckPosition(), groundDistance, groundMask);

        if (IsGrounded)
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
        
    }

    private void Update()
    {
        HandleGroundCheck();
        HandleCrouchHeight();
        
        // Process movement inputs and physics into a single frame movement vector
        ExecuteCombinedMovement();
    }

    private void ExecuteCombinedMovement()
    {
        // 1. Calculate Horizontal Input Movement
        Vector3 horizontalInputMove = Vector3.zero;
        if (!IsStunned && !IsDashing) 
        {
            float baseSpeed = _isCrouching ? crouchSpeed : walkSpeed;
            float speed = baseSpeed * Mathf.Max(0f, SpeedMultiplier);
            Vector3 moveDirection = transform.right * _moveInput.x + transform.forward * _moveInput.y;
            horizontalInputMove = moveDirection * speed;
        }

        // 2. Apply Gravity to our Physics Velocity tracking vector
        if (!IsGrounded)
        {
            _velocity.y += gravity * Time.deltaTime;
        }

        // 3. Smoothly Decay Physics Impulse Forces (X & Z)
        Vector3 horizontalPhysicsVelocity = new Vector3(_velocity.x, 0f, _velocity.z);
        horizontalPhysicsVelocity = Vector3.Lerp(horizontalPhysicsVelocity, Vector3.zero, horizontalDrag * Time.deltaTime);
        _velocity = new Vector3(horizontalPhysicsVelocity.x, _velocity.y, horizontalPhysicsVelocity.z);

        // 4. Handle Jump Input
        if (_jumpQueued && IsGrounded && !IsStunned)
        {
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _jumpQueued = false;
        }

        // 5. Combine Input (m/s) and Physics Velocity (m/s) into final frame displacement
        Vector3 finalFrameMovement = (horizontalInputMove * Time.deltaTime) + (_velocity * Time.deltaTime);

        if (_controller.enabled)
        {
            _controller.Move(finalFrameMovement);
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
        {
            return groundCheck.position;
        }

        return transform.position + _controller.center - Vector3.up * (_controller.height / 2f);
    }

    public void SetVelocity(Vector3 newVelocity)
    {
        _velocity = newVelocity;
    }

    public void Stun(float duration)
    {
        StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        HaltVelocity(); // Stop any current movement
        _stunned = true; 
        AbilityManager abilityManager = GetComponent<AbilityManager>();
        abilityManager.enabled = false; // Disable abilities while stunned
        yield return new WaitForSeconds(duration);

        abilityManager.enabled = true; // Re-enable abilities after stun
        _stunned = false;
    }

    // Add these public methods inside the PlayerController class to satisfy the interface expansion

    public Vector3 GetAimDirection()
    {
        // Fall back to Camera.main if an explicit reference isn't serialized
        Camera mainCam = Camera.main;
        if (mainCam != null) 
        {
            return mainCam.transform.forward;
        }
        return transform.forward;
    }

}
