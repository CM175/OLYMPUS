using UnityEngine;

public abstract class Ultimate : Ability
{
    [Header("Ultimate Settings")]
    public int cost = 100;

    // Not serialized — always set at runtime via Initialize()
    [System.NonSerialized] public PlayerStats playerStats;

    // ── Called by HeroManager after the hero is parented and in the scene ──

    public void Initialize(PlayerStats stats)
    {
        playerStats = stats;
        playerStats.maxUltimateCharge = cost;
    }

    // ── Readiness check ──────────────────────────────────────────────────────

    public bool IsReady()
    {
        if (playerStats == null) return false;
        return !IsOnCooldown() && playerStats.currentUltimateCharge >= cost;
    }

    // ── Override CanUse() to gate on charge ────────────────────────────────────

    protected override bool CanUse()
    {
        return base.CanUse()
           && playerStats != null
           && playerStats.currentUltimateCharge >= cost;
    }

    protected override void Activate() { playerStats.ConsumeCharge(cost); }
}