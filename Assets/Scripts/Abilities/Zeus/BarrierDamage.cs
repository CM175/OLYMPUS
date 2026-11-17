using UnityEngine;
using System.Collections.Generic;

public class BarrierDamage : MonoBehaviour
{
    public int damage = 10;
    private Dictionary<Collider, float> _nextHitTime = new();

    private void OnTriggerStay(Collider col)
    {
        Transform attackerTransform = transform.root;
        Transform victimTransform = col.transform.root;
        if (victimTransform == attackerTransform) return;

        var myTeam = attackerTransform.GetComponent<TeamMember>();
        if (myTeam != null && !myTeam.IsEnemy(victimTransform.gameObject))
        {
            return;
        }

        if (_nextHitTime.TryGetValue(col, out float t) && Time.time < t) return;

        IDamageable target = victimTransform.GetComponent<IDamageable>() ?? col.GetComponent<IDamageable>();
        DamageDealer.Deal(attackerTransform.gameObject, target, damage);
        if (target != null) AbilityVfx.Impact(victimTransform.position + Vector3.up, AbilityVfx.Storm, 0.7f);

        _nextHitTime[col] = Time.time + 0.5f;
    }
}