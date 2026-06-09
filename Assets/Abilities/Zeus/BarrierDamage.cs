using UnityEngine;
using System.Collections.Generic;

public class BarrierDamage : MonoBehaviour
{
    public int damage = 10;

    private Dictionary<Collider, float> _nextHitTime = new();

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Enemy")) return;
        if (_nextHitTime.TryGetValue(other, out float t) && Time.time < t) return;
        
        other.GetComponent<IDamageable>()?.TakeDamage(damage);
        _nextHitTime[other] = Time.time + 0.5f; // hit every 0.5s
    }
}