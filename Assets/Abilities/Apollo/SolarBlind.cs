using UnityEngine;

public class SolarBlind : Ability
{
    [Header("Blind Settings")]
    public float flashRadius = 5f;
    public int minorBurnDamage = 15;
    public float utilityDuration = 2f;
    public float range = 15f;

    protected override void Activate()
    {
        if (playerCamera == null)
        {
            isExecuting = false;
            return;
        }

        Vector3 strikePoint = playerCamera.transform.position + playerCamera.transform.forward * range;
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out RaycastHit hit, range))
        {
            strikePoint = hit.point;
        }

        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, strikePoint, Quaternion.identity);
        }

        Collider[] victims = Physics.OverlapSphere(strikePoint, flashRadius);
        foreach (var victim in victims)
        {
            Transform victimRoot = victim.transform.root;
            var myTeam = transform.root.GetComponent<TeamMember>();
            
            if (myTeam != null && myTeam.IsEnemy(victim.gameObject))
            {
                victimRoot.GetComponent<IDamageable>()?.TakeDamage(minorBurnDamage);

                // OPTIMIZED: Replaced EnemyAI lookup with a unified generic Stun call via interface
                IMovementController movement = victimRoot.GetComponent<IMovementController>();
                if (movement != null)
                {
                    movement.Stun(utilityDuration);
                }
            }
        }

        StartCooldown();
    }
}