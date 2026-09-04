using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ReleaseTheKraken : Ultimate, IInitializable
{
    [Header("Kraken Zone")]
    public float radius = 9f;
    public int eruptionDamage = 70;
    public float suctionDuration = 1.2f;
    public float suctionForce = 9f;
    public float upwardEruptionForce = 12f;
    public float dazeDuration = 0.5f;

    private Transform _rootTransform;

    public void Initialize(GameObject playerRoot)
    {
        _rootTransform = playerRoot.transform;
    }

    protected override void Activate()
    {
        base.Activate();
        StartCoroutine(KrakenRoutine());
    }

    private IEnumerator KrakenRoutine()
    {
        Vector3 center = _rootTransform.position;
        GameObject vfxInstance = null;

        if (vfxPrefab != null)
        {
            vfxInstance = Instantiate(vfxPrefab, center, Quaternion.identity);
            vfxInstance.transform.localScale = Vector3.one * (radius * 2);
            Destroy(vfxInstance, suctionDuration + 1.5f);
        }

        TeamMember myTeam = _rootTransform.GetComponent<TeamMember>();
        float elapsed = 0f;

        // Stage 1: Vortex pull
        while (elapsed < suctionDuration)
        {
            Collider[] targets = Physics.OverlapSphere(center, radius);
            foreach (var col in targets)
            {
                Transform victimRoot = col.transform.root;
                if (victimRoot == _rootTransform) continue;
                if (myTeam != null && !myTeam.IsEnemy(victimRoot.gameObject)) continue;

                IMovementController move = victimRoot.GetComponent<IMovementController>();
                if (move != null)
                {
                    Vector3 pullDir = (center - victimRoot.position);
                    pullDir.y = 0f;
                    move.Move(pullDir.normalized * (suctionForce * Time.deltaTime));
                }
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Stage 2: Eruption & Brief Daze
        int finalDmg = playerStats != null 
            ? Mathf.RoundToInt(eruptionDamage * playerStats.DamageMultiplier) 
            : eruptionDamage;

        Collider[] eruptionTargets = Physics.OverlapSphere(center, radius);
        HashSet<Transform> processedTargets = new();

        foreach (var col in eruptionTargets)
        {
            Transform victimRoot = col.transform.root;
            if (victimRoot == _rootTransform || processedTargets.Contains(victimRoot)) continue;
            if (myTeam != null && !myTeam.IsEnemy(victimRoot.gameObject)) continue;

            processedTargets.Add(victimRoot);

            IDamageable dmg = victimRoot.GetComponent<IDamageable>() ?? victimRoot.GetComponentInChildren<IDamageable>();
            dmg?.TakeDamage(finalDmg);

            IMovementController move = victimRoot.GetComponent<IMovementController>();
            if (move != null)
            {
                move.HaltVelocity();
                move.Stun(dazeDuration);
                move.ApplyImpulseForce(Vector3.up * upwardEruptionForce);
            }
        }

        if (vfxInstance != null)
        {
            Destroy(vfxInstance, 1f);
        }

        StartCooldown();
    }
}