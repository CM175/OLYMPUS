using UnityEngine;
using UnityEngine.InputSystem;

public class AbilityManager : MonoBehaviour
{
    public bool isPlayer = false; 
    public Attack primaryAttack;
    
    public Ability primaryAbility;
    public Ability secondaryAbility;
    public Ability mobilityAbility;
    public Ultimate ultimateAbility;

    private Ability[] abilities => new[] { primaryAbility, secondaryAbility, mobilityAbility, ultimateAbility };
    private InputSystem_Actions _input;

    // Store references so we can unsubscribe correctly
    private System.Action<InputAction.CallbackContext> _onAttack;
    
    private System.Action<InputAction.CallbackContext> _onPrimary;
    private System.Action<InputAction.CallbackContext> _onSecondary;
    private System.Action<InputAction.CallbackContext> _onMobility;
    private System.Action<InputAction.CallbackContext> _onUltimate;

    private void Awake()
    {
        if (!isPlayer) return; 
        _input = new InputSystem_Actions();
        // Changed: Call StartCharging on press, but Use/Activate on release
        _onAttack = ctx =>
        {
            if (ctx.started)
                primaryAttack?.OnPress();

            if (ctx.canceled)
                primaryAttack?.OnRelease();
        };
        
        _onPrimary = _ => primaryAbility?.Use();
        _onSecondary = _ => secondaryAbility?.Use();
        _onMobility  = _ => mobilityAbility?.Use();
        _onUltimate  = _ => ultimateAbility?.Use();
    }

    private void OnEnable()
    {
        if (!isPlayer) return; 

        _input.Player.Enable();
        // Changed: Subscribe to started (press) and canceled (release) as well
        _input.Player.Attack.started   += _onAttack;
        _input.Player.Attack.performed += _onAttack;
        _input.Player.Attack.canceled  += _onAttack;

        
        _input.Player.Primary.performed += _onPrimary;
        _input.Player.Secondary.performed += _onSecondary;
        _input.Player.Mobility.performed  += _onMobility;
        _input.Player.Ultimate.performed  += _onUltimate;

        foreach (var ability in abilities)
        {
            if (!ability) continue;
            ability.lastUsedTime = Time.time - ability.cooldown; // Ensure all abilities are canceled when the manager is disabled
        }
    }

    private void OnDisable()
    {
        if (!isPlayer) return; 
        _input.Player.Attack.started   -= _onAttack;
        _input.Player.Attack.performed -= _onAttack;
        _input.Player.Attack.canceled  -= _onAttack;
        
        _input.Player.Primary.performed -= _onPrimary;
        _input.Player.Secondary.performed -= _onSecondary;
        _input.Player.Mobility.performed  -= _onMobility;
        _input.Player.Ultimate.performed  -= _onUltimate;
        _input.Player.Disable();

        foreach (var ability in abilities)
        {
            if (!ability) continue;
            ability.StartCooldown(); 
        }
    }

    public bool HasActiveExclusiveAbility()
    {
        // Check primary weapon attacks
        if (primaryAttack != null && primaryAttack.isExecuting && primaryAttack.exclusiveState) return true;

        // Loop through standard utility/mobility ability arrays safely
        if (abilities != null)
        {
            foreach (var ability in abilities)
            {
                if (ability != null && ability.isExecuting && ability.exclusiveState)
                {
                    return true;
                }
            }
        }

        // Check ultimate active window states
        if (ultimateAbility != null && ultimateAbility.isExecuting && ultimateAbility.exclusiveState) return true;

        return false;
    }
}