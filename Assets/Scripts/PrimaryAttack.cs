using UnityEngine;

public abstract class Attack : Ability
{
    [Header("Primary Attack")]
    public int damage = 10;
    public float range = 100f;

    public bool isHolding;

    private void Update()
    {
        // Automatic fire while holding
        if (isHolding)
        {
            TryFire();
        }
    }

    public override void OnPress()
    {
        isHolding = true;
    }

    public override void OnRelease()
    {
        isHolding = false;
    }

    private void TryFire()
    {
        if (!CanUse()) return;

        Activate();
        StartCooldown();
    }

    protected override void Activate(){}


}