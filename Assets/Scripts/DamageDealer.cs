using UnityEngine;

/// <summary>
/// Single entry point for all outgoing damage. Always passes the attacker
/// explicitly and applies the attacker's PlayerStats.DamageMultiplier once,
/// so abilities never rely on ambient state or scale damage themselves.
/// </summary>
public static class DamageDealer
{
    public static int Scale(GameObject attacker, int baseDamage)
    {
        if (attacker != null && attacker.transform.root.TryGetComponent(out PlayerStats stats))
            return Mathf.RoundToInt(baseDamage * stats.DamageMultiplier);
        return baseDamage;
    }

    public static void Deal(GameObject attacker, IDamageable target, int baseDamage)
    {
        if (target == null) return;
        target.TakeDamage(Scale(attacker, baseDamage), attacker);
    }

    public static void DealOverTime(GameObject attacker, IDamageable target, int baseTotalDamage, float duration)
    {
        if (target == null) return;
        target.DamageOverTime(Scale(attacker, baseTotalDamage), duration, attacker);
    }
}
