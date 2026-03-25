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
        _input.Player.Attack.performed    += _ => UsePrimary();
        _input.Player.Secondary.performed += _ => UseSecondary();
        _input.Player.Mobility.performed  += _ => UseMobility();
        _input.Player.Ultimate.performed  += _ => UseUltimate();
    }

    private void OnDisable()
    {
        _input.Player.Attack.performed    -= _ => UsePrimary();
        _input.Player.Secondary.performed -= _ => UseSecondary();
        _input.Player.Mobility.performed  -= _ => UseMobility();
        _input.Player.Ultimate.performed  -= _ => UseUltimate();
        _input.Player.Disable();
    }

    public void UsePrimary()   => primaryAbility?.Use();
    public void UseSecondary() => secondaryAbility?.Use();
    public void UseMobility()  => mobilityAbility?.Use();
    public void UseUltimate()  => ultimateAbility?.Use();
}