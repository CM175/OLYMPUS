using UnityEngine;

public class DamageProxy : MonoBehaviour, IDamageable
{
    private IDamageable _parentDamageable;
    public bool IsInvincible
    {
        get
        {
            if (_parentDamageable != null)
            {
                return _parentDamageable.IsInvincible;
            }
            return false;
        }
        set
        {
            if (_parentDamageable != null)
            {
                _parentDamageable.IsInvincible = value;
            }
        }
    }

    public bool IsDead
    {
        get
        {
            if (_parentDamageable != null)
            {
                return _parentDamageable.IsDead;
            }
            return false;
        }
        set
        {
            if (_parentDamageable != null)
            {
                _parentDamageable.IsDead = value;
            }
        }
    }

    private void Start()
    {
        _parentDamageable = transform.root.GetComponent<IDamageable>();
    }

    public void TakeDamage(int damage)
    {
        _parentDamageable?.TakeDamage(damage);
    }

    public void ApplyHeal(int heal)
    {
        _parentDamageable?.ApplyHeal(heal);
    }

    public void HealOverTime(int totalHeal, float duration)
    {
        _parentDamageable?.HealOverTime(totalHeal, duration);
    }

    public void DamageOverTime(int totalDamage, float duration)
    {
        _parentDamageable?.DamageOverTime(totalDamage, duration);
    }
}