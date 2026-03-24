using UnityEngine;

/// <summary>
/// Passive: reduces all incoming damage by a flat percentage.
/// Initialized by HeroManager after the hero is parented to the Player.
/// </summary>
public class FrontalArmor : PassiveAbility
{
    [Header("Stats")]
    [Range(0f, 0.9f)]
    [Tooltip("0.25 = 25% damage reduction on all hits.")]
    public float damageReduction = 0.25f;

    // Called once by HeroManager — safe to call after parenting
    public void Initialize(PlayerHealth playerHealth)
    {
        if (playerHealth == null) return;
        playerHealth.DamageReduction = damageReduction;
        Debug.Log($"[FrontalArmor] Damage reduction set to {damageReduction * 100f}%");
    }

    // PassiveAbility requires Activate() — nothing to do at runtime;
    // the reduction is baked into PlayerHealth for the duration of the match.
    protected override void Activate() { }
}
