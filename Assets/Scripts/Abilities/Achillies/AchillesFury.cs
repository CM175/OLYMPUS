using System.Collections;
using UnityEngine;

public class AchillesFury : Ultimate
{
    [Header("Fury Stats")]
    public float duration = 6f;
    public float speedMultiplier = 1.6f;
    public float damageMultiplier = 2f;

    protected override void Activate()
    {
        base.Activate();
        StartCoroutine(FuryRoutine());
    }

    private IEnumerator FuryRoutine()
    {
        // Cache components â€” they live on the Player root, not the hero child
        IDamageable health         = transform.root.GetComponent<IDamageable>();
        PlayerStats stats           = transform.root.GetComponent<PlayerStats>();
        IMovementController controller = transform.root.GetComponent<IMovementController>();

        // Apply buffs
        if (health     != null) health.IsInvincible          = true;
        if (stats      != null) stats.DamageMultiplier        = damageMultiplier;
        if (controller != null) controller.SpeedMultiplier    = speedMultiplier;

        AbilityVfx.Burst(transform.root.position + Vector3.up, AbilityVfx.Crimson, 2f, 60);
        AbilityVfx.Aura(transform.root, AbilityVfx.Crimson, duration);

        //Debug.Log($"[AchillesFury] Active â€” {duration}s of invincibility, {damageMultiplier}x damage, {speedMultiplier}x speed.");

        yield return new WaitForSeconds(duration);

        // Remove buffs
        if (health     != null) health.IsInvincible          = false;
        if (stats      != null) stats.DamageMultiplier        = 1f;
        if (controller != null) controller.SpeedMultiplier    = 1f;

        //Debug.Log("[AchillesFury] Expired.");
        StartCooldown();
    }
}
