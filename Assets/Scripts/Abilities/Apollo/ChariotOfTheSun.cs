using System.Collections;
using UnityEngine;

public class ChariotOfTheSun : Ultimate
{
    [Header("Flight & Positioning")]
    public float liftHeight = 8f;        // Total upward lift distance
    public float liftSpeed = 12f;        // Speed of upward ascent
    public float hoverDuration = 5f;
    public float downwardDescendForce = -8f;

    [Header("Solar Pillar")]
    public float beamRadius = 6f;
    public int damagePerTick = 12;
    public int healPerTick = 20;
    public float tickRate = 0.25f;
    public float maxRange = 120f;

    [Header("Damage Mitigation")]
    [Range(0f, 1f)] public float aerialDamageReduction = 0.5f; // Replaces raw invincibility with 50% resistance

    protected override void Activate()
    {
        base.Activate();
        StartCoroutine(ChariotFlightRoutine());
    }

    private IEnumerator ChariotFlightRoutine()
    {
        Transform root = transform.root;
        IMovementController movement = root.GetComponent<IMovementController>();
        IDamageable health = root.GetComponent<IDamageable>();
        TeamMember myTeam = root.GetComponent<TeamMember>();

        // 1. Ascend to hover altitude
        if (movement != null)
        {
            movement.HaltVerticalVelocity();
            movement.SetGravityMultiplier(0f);
            movement.IsDashing = true; 
        }

        float verticalAscended = 0f;
        while (verticalAscended < liftHeight)
        {
            float step = liftSpeed * Time.deltaTime;
            if (verticalAscended + step > liftHeight)
                step = liftHeight - verticalAscended;

            if (movement != null)
                movement.Move(Vector3.up * step);

            verticalAscended += step;
            yield return null;
        }

        // 2. Lock velocity & maintain float
        if (movement != null)
            movement.HaltVerticalVelocity();

        float elapsed = 0f;
        float nextTick = 0f;

        while (elapsed < hoverDuration)
        {
            Vector3 rayOrigin;
            Vector3 rayDirection = movement != null ? movement.GetAimDirection() : transform.forward;

            if (playerCamera != null)
            {
                rayOrigin = playerCamera.transform.position;
            }
            else
            {
                rayOrigin = root.position;
            }

            Ray ray = new Ray(rayOrigin, rayDirection);
            if (Physics.Raycast(ray, out RaycastHit hit, maxRange))
            {
                if (Time.time >= nextTick)
                {
                    ProcessSolarPillar(hit.point, myTeam);
                    nextTick = Time.time + tickRate;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 3. Restore gravity and descend
        if (movement != null)
        {
            movement.SetGravityMultiplier(1f);
            movement.ApplyImpulseForce(Vector3.up * downwardDescendForce);
            movement.IsDashing = false;
        }

        StartCooldown();
    }

    private void ProcessSolarPillar(Vector3 centerPoint, TeamMember myTeam)
    {
        // Visual indicator at target point
        if (vfxPrefab != null)
        {
            GameObject fx = Instantiate(vfxPrefab, centerPoint, Quaternion.identity);
            fx.transform.localScale = Vector3.one * (beamRadius * 2);
            Destroy(fx, tickRate);
        }

        Collider[] targets = Physics.OverlapSphere(centerPoint, beamRadius);
        foreach (var col in targets)
        {
            Transform targetRoot = col.transform.root;

            IDamageable targetHealth = targetRoot.GetComponent<IDamageable>();
            if (targetHealth == null) continue;

            if (myTeam != null && myTeam.IsEnemy(targetRoot.gameObject))
            {
                Deal(targetHealth, damagePerTick);
            }
            else if (myTeam != null && !myTeam.IsEnemy(targetRoot.gameObject))
            {
                targetHealth.ApplyHeal(healPerTick);
            }
        }
    }
}