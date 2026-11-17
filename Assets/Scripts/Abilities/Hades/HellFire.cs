using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HellFire : Ability, IInitializable
{
    [Header("Hellfire Settings")]
    [SerializeField] private float radius = 6f;
    [SerializeField] private int initialDamage = 40;
    [SerializeField] private float duration = 4.0f;
    [SerializeField] private float tickRate = 0.5f;
    [SerializeField] private int damagePerTick = 10;

    [Header("Crowd Control")]
    [SerializeField] private float slowMultiplier = 0.5f; // 50% movement speed
    [SerializeField] private float slowDuration = 1.0f;     // Refreshing duration while inside

    private Transform _rootTransform;

    public void Initialize(GameObject playerRoot)
    {
        _rootTransform = playerRoot.transform;
    }

    protected override void Activate()
    {
        if (_rootTransform == null) _rootTransform = transform.root;

        Vector3 centerPos = _rootTransform.position;

        // Instantiate visual warning/lingering fire zone if assigned
        if (vfxPrefab != null)
        {
            GameObject fx = Instantiate(vfxPrefab, centerPos, Quaternion.identity);
            fx.transform.localScale = Vector3.one * (radius * 2);
            Destroy(fx, duration + 0.5f);
        }
        else
        {
            AbilityVfx.Ring(centerPos + Vector3.up * 0.1f, AbilityVfx.Violet, radius, 0.6f);
            AbilityVfx.Burst(centerPos + Vector3.up * 0.5f, AbilityVfx.Violet, 2f, 45);
        }

        StartCoroutine(HellfireRoutine(centerPos));
        StartCooldown();
    }

    private IEnumerator HellfireRoutine(Vector3 center)
    {
        float elapsed = 0f;
        GameObject attackerRoot = _rootTransform.gameObject;
        TeamMember myTeam = attackerRoot.GetComponent<TeamMember>();

        // 1. Initial burst hit on activation
        ApplyInstantHit(center, attackerRoot, myTeam);

        // 2. Burning ground loop over duration
        while (elapsed < duration)
        {
            Collider[] targets = AbilityPhysics.OverlapSphere(center, radius);
            HashSet<GameObject> hitThisTick = new HashSet<GameObject>();

            foreach (var col in targets)
            {
                Transform victimRoot = col.transform.root;
                if (victimRoot.gameObject == attackerRoot) continue;
                if (!hitThisTick.Add(victimRoot.gameObject)) continue;

                if (myTeam != null && !myTeam.IsEnemy(victimRoot.gameObject)) continue;

                // Apply burn damage tick & slow field control
                IDamageable target = victimRoot.GetComponent<IDamageable>() ?? col.GetComponent<IDamageable>();
                IMovementController movement = victimRoot.GetComponent<IMovementController>();

                if (target != null && !target.IsDead)
                {
                    Deal(target, damagePerTick);
                }

                if (movement != null)
                {
                    movement.ApplySlow(slowMultiplier, slowDuration, decaying: false);
                }
            }

            yield return new WaitForSeconds(tickRate);
            elapsed += tickRate;
        }
    }

    private void ApplyInstantHit(Vector3 center, GameObject attackerRoot, TeamMember myTeam)
    {
        Collider[] targets = AbilityPhysics.OverlapSphere(center, radius);
        HashSet<GameObject> hitVictims = new HashSet<GameObject>();

        foreach (var col in targets)
        {
            Transform victimRoot = col.transform.root;
            if (victimRoot.gameObject == attackerRoot || !hitVictims.Add(victimRoot.gameObject)) continue;
            if (myTeam != null && !myTeam.IsEnemy(victimRoot.gameObject)) continue;

            IDamageable target = victimRoot.GetComponent<IDamageable>() ?? col.GetComponent<IDamageable>();
            if (target != null && !target.IsDead)
            {
                Deal(target, initialDamage);
            }
        }
    }
}