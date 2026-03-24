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

    // ── Override Use() to gate on charge ────────────────────────────────────

    public override void Use()
    {
        if (!IsReady()) return;
        isExecuting = true;
        playerStats.ConsumeCharge(cost);
        Activate();
    }
}