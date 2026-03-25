using UnityEngine;

public class BarrierDamage : MonoBehaviour
{
    public int damage = 10;
    [HideInInspector] public PlayerStats attackerStats;

    private void OnTriggerEnter(Collider other)
    {
        // Fix: tag check BEFORE logging — was firing for every collider
        if (!other.CompareTag("Enemy")) return;

        IDamageable target = other.GetComponent<IDamageable>();
        if (target == null) return;

        target.TakeDamage(damage);
        Debug.Log($"[BarrierDamage] Zapped {other.name} for {damage} damage.");
    }
}