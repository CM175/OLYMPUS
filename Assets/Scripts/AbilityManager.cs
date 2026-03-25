using UnityEngine;
using UnityEngine.InputSystem;

public class AbilityManager : MonoBehaviour
{
    public Ability primaryAbility;
    public Ability secondaryAbility;
    public Ability mobilityAbility;
    public Ultimate ultimateAbility;

    public void UsePrimary()   => primaryAbility?.Use();
    public void UseSecondary() => secondaryAbility?.Use();
    public void UseMobility()  => mobilityAbility?.Use();
    public void UseUltimate()  => ultimateAbility?.Use();
}

public class PlayerInput : MonoBehaviour
{
    [SerializeField] private AbilityManager _abilities;

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb.qKey.wasPressedThisFrame) _abilities.UseSecondary();
        if (kb.eKey.wasPressedThisFrame) _abilities.UseMobility();
        if (kb.fKey.wasPressedThisFrame) _abilities.UseUltimate();
    }
}