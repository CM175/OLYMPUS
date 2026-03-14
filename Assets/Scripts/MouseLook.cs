using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLook : MonoBehaviour
{
    [Header("Sensitivity")]
    public float mouseSensitivity = 0.15f;  // new Input System delta is already scaled — keep this low
    public float gamepadSensitivity = 120f; // degrees per second for stick look

    [Header("Clamp")]
    public float minPitch = -90f;
    public float maxPitch =  90f;

    [Header("References")]
    public Transform playerBody; // The root player Transform (rotates on Y)

    private InputSystem_Actions _input;
    private Vector2 _lookInput;
    private float _xRotation;

    private void Awake()
    {
        _input = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        _input.Player.Enable();
        _input.Player.Look.performed += OnLook;
        _input.Player.Look.canceled  += OnLook;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    private void OnDisable()
    {
        _input.Player.Look.performed -= OnLook;
        _input.Player.Look.canceled  -= OnLook;
        _input.Player.Disable();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    private void OnLook(InputAction.CallbackContext ctx)
        => _lookInput = ctx.ReadValue<Vector2>();

    private void Update()
    {
        // Detect if the input is from a gamepad stick (needs Time.deltaTime scaling)
        // or mouse delta (already a frame delta — just needs sensitivity)
        float scaledX, scaledY;

        var control = _input.Player.Look.activeControl;
        bool isGamepad = control != null && control.device is Gamepad;

        if (isGamepad)
        {
            scaledX = _lookInput.x * gamepadSensitivity * Time.deltaTime;
            scaledY = _lookInput.y * gamepadSensitivity * Time.deltaTime;
        }
        else
        {
            scaledX = _lookInput.x * mouseSensitivity;
            scaledY = _lookInput.y * mouseSensitivity;
        }

        _xRotation -= scaledY;
        _xRotation  = Mathf.Clamp(_xRotation, minPitch, maxPitch);

        // Camera pitches up/down on this transform (the camera's parent)
        transform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);

        // Player body yaws left/right
        playerBody.Rotate(Vector3.up * scaledX);
    }
}