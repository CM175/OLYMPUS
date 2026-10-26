using System;
using UnityEngine;

public class DamageProxy : MonoBehaviour, IDamageable
{
    private IDamageable _parentDamageable;

    public bool IsInvincible
    {
        get => _parentDamageable != null && _parentDamageable.IsInvincible;
        set { if (_parentDamageable != null) _parentDamageable.IsInvincible = value; }
    }

    public bool IsDead
    {
        get => _parentDamageable != null && _parentDamageable.IsDead;
        set { if (_parentDamageable != null) _parentDamageable.IsDead = value; }
    }

    public int CurrentHealth => _parentDamageable != null ? _parentDamageable.CurrentHealth : 0;
    public int MaxHealth => _parentDamageable != null ? _parentDamageable.MaxHealth : 1;

    public event Action<int, GameObject> OnDamageReceived
    {
        add { if (_parentDamageable != null) _parentDamageable.OnDamageReceived += value; }
        remove { if (_parentDamageable != null) _parentDamageable.OnDamageReceived -= value; }
    }

    public event Action<GameObject> OnDied
    {
        add { if (_parentDamageable != null) _parentDamageable.OnDied += value; }
        remove { if (_parentDamageable != null) _parentDamageable.OnDied -= value; }
    }

    private void Awake()
    {
        _parentDamageable = transform.root.GetComponent<IDamageable>();
    }

    public void TakeDamage(int damage) => TakeDamage(damage, null);

    public void TakeDamage(int damage, GameObject attacker)
    {
        _parentDamageable?.TakeDamage(damage, attacker);
    }

    public void ApplyHeal(int heal) => _parentDamageable?.ApplyHeal(heal);
    public void HealOverTime(int totalHeal, float duration) => _parentDamageable?.HealOverTime(totalHeal, duration);
    public void DamageOverTime(int totalDamage, float duration) => _parentDamageable?.DamageOverTime(totalDamage, duration);
    public void DamageOverTime(int totalDamage, float duration, GameObject attacker) => _parentDamageable?.DamageOverTime(totalDamage, duration, attacker);
}