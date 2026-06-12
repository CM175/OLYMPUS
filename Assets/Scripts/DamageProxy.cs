using UnityEngine;

public class DamageProxy : MonoBehaviour, IDamageable
{
    private IDamageable _parentDamageable;

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
}