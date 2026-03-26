using UnityEngine;

public abstract class PassiveAbility : Ability
{
    protected override bool CanUse()
    {
        return true;
    }

}