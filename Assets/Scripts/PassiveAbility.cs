using UnityEngine;

public abstract class PassiveAbility : Ability
{
    public override void Use()
    {
        Activate();
    }

}