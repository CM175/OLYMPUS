using UnityEngine;
using System.Collections;

public class LabyrinthDomain : Ultimate, IInitializable
{
    [Header("Arena Settings")]
    [SerializeField] private GameObject pillarPrefab;
    [SerializeField] private int pillarCount = 10;
    [SerializeField] private float arenaRadius = 7.5f;
    [SerializeField] private float arenaDuration = 6.0f;
    [SerializeField] private int slamDamage = 50;
    [SerializeField] private float stunDuration = 0.75f;
    [SerializeField] private float pullForce = 15f;

    private Transform _rootTransform;

    public void Initialize(GameObject playerRoot)
    {
        _rootTransform = playerRoot.transform;
    }

    protected override void Activate()
    {
        base.Activate(); // Consumes Ultimate charge via Ultimate base class
        StartCoroutine(ErectLabyrinth());
    }

    private IEnumerator ErectLabyrinth()
    {
        Vector3 center = _rootTransform.position;
        Collider[] victims = Physics.OverlapSphere(center, arenaRadius);
        var myTeam = _rootTransform.GetComponent<TeamMember>();

        foreach (var col in victims)
        {
            Transform targetRoot = col.transform.root;
            if (targetRoot == _rootTransform) continue;

            if (myTeam != null && !myTeam.IsEnemy(targetRoot.gameObject))
                continue;

            IDamageable dmg = targetRoot.GetComponent<IDamageable>() ?? targetRoot.GetComponentInChildren<IDamageable>();
            Deal(dmg, slamDamage);

            IMovementController targetMovement = targetRoot.GetComponent<IMovementController>();
    
            Vector3 toCenter = center - targetRoot.position;
            toCenter.y = 0f;
            Vector3 pullDirection = toCenter.normalized;

            Vector3 pullImpulse = pullDirection * pullForce;
            targetMovement.HaltVelocity();
            targetMovement.Stun(stunDuration); 
            targetMovement.ApplyImpulseForce(pullImpulse);
        }

        GameObject[] spawnedPillars = new GameObject[pillarCount];
        float angleStep = 360f / pillarCount;

        for (int i = 0; i < pillarCount; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * arenaRadius;
            Vector3 spawnPos = center + offset;
            Quaternion rotation = Quaternion.LookRotation(center - spawnPos);
            spawnedPillars[i] = Instantiate(pillarPrefab, spawnPos, rotation);
            Destroy(spawnedPillars[i], arenaDuration+ 1.5f);
        }

        StartCooldown();
        yield return new WaitForSeconds(arenaDuration);

        for (int i = 0; i < spawnedPillars.Length; i++)
        {
            if (spawnedPillars[i] != null)
                Destroy(spawnedPillars[i]);
        }
    }
}