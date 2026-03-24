using UnityEngine;
using UnityEngine.InputSystem;

public class AbilityManager : MonoBehaviour
{
    public Ability primaryAbility;
    public Ability secondaryAbility;
    public Ability mobilityAbility;
    public Ultimate ultimateAbility;

    private InputSystem_Actions _input;

    private void Awake()
    {
        _input = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        _input.Player.Enable();

        // Primary attack maps to the existing Attack action (left mouse / gamepad west)
        _input.Player.Attack.performed += OnAttack;
    }

    private void OnDisable()
    {
        _input.Player.Attack.performed -= OnAttack;
        _input.Player.Disable();
    }

    private void OnAttack(InputAction.CallbackContext ctx)
    {
        if (primaryAbility != null)
            primaryAbility.Use();
    }

    private void Update()
    {
        // Q / E / F have no actions in InputSystem_Actions yet, so we read
        // Keyboard.current — still the new Input System, just polled rather
        // than event-driven. This keeps us fully off the legacy Input Manager.
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.qKey.wasPressedThisFrame && secondaryAbility != null)
            secondaryAbility.Use();

        if (kb.eKey.wasPressedThisFrame && mobilityAbility != null)
            mobilityAbility.Use();

        if (kb.fKey.wasPressedThisFrame && ultimateAbility != null)
            ultimateAbility.Use();
    }
}