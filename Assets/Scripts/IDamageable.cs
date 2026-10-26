using System;
using UnityEngine;

/// <summary>
/// Implemented by anything that can receive damage.
/// Decouples attackers and RL agents from concrete health implementations.
/// </summary>
public interface IDamageable
{
    bool IsInvincible { get; set; }
    bool IsDead { get; set; }

    int CurrentHealth { get; }
    int MaxHealth { get; }

    // Event fired whenever damage is dealt: (int amount, GameObject attacker)
    event Action<int, GameObject> OnDamageReceived;
    // Event fired whenever entity dies: (GameObject killer)
    event Action<GameObject> OnDied;

    void TakeDamage(int damage);
    void TakeDamage(int damage, GameObject attacker);
    void ApplyHeal(int heal);
    void HealOverTime(int totalHeal, float duration);
    void DamageOverTime(int totalDamage, float duration);
    void DamageOverTime(int totalDamage, float duration, GameObject attacker);
}