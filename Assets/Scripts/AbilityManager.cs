using UnityEngine;
using UnityEngine.InputSystem;

public class AbilityManager : MonoBehaviour
{
    
    public Ability primaryAbility;
    public Ability secondaryAbility;
    public Ability mobilityAbility;
    public Ultimate ultimateAbility;
    private InputSystem_Actions _input;

    // Store references so we can unsubscribe correctly
    private System.Action<InputAction.CallbackContext> _onAttack;
    private System.Action<InputAction.CallbackContext> _onSecondary;
    private System.Action<InputAction.CallbackContext> _onMobility;
    private System.Action<InputAction.CallbackContext> _onUltimate;

    private void Awake()
    {
        _input = new InputSystem_Actions();
        _onAttack    = _ => primaryAbility?.Use();
        _onSecondary = _ => secondaryAbility?.Use();
        _onMobility  = _ => mobilityAbility?.Use();
        _onUltimate  = _ => ultimateAbility?.Use();
    }

    private void OnEnable()
    {
        _input.Player.Enable();
        _input.Player.Attack.performed    += _onAttack;
        _input.Player.Secondary.performed += _onSecondary;
        _input.Player.Mobility.performed  += _onMobility;
        _input.Player.Ultimate.performed  += _onUltimate;
    }

    private void OnDisable()
    {
        _input.Player.Attack.performed    -= _onAttack;
        _input.Player.Secondary.performed -= _onSecondary;
        _input.Player.Mobility.performed  -= _onMobility;
        _input.Player.Ultimate.performed  -= _onUltimate;
        _input.Player.Disable();
    }
}