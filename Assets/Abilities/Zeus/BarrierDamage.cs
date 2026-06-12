using UnityEngine;
using System.Collections.Generic;

public class BarrierDamage : MonoBehaviour
{
    public int damage = 10;

    private Dictionary<Collider, float> _nextHitTime = new();

    private void OnTriggerStay(Collider col)
    {
        var myTeam = transform.root.GetComponent<TeamMember>();
                
        if (myTeam != null && !myTeam.IsEnemy(col.transform.root.gameObject))
        {
            return;
        }
        if (_nextHitTime.TryGetValue(col, out float t) && Time.time < t) return;
        
        col.GetComponent<IDamageable>()?.TakeDamage(damage);
        _nextHitTime[col] = Time.time + 0.5f; // hit every 0.5s
    }
}