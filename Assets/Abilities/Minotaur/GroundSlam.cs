using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;

public class GroundSlam : Ability, IInitializable
{
    [Header("Leap Settings")]
    [SerializeField] private float leapForwardSpeed = 20f;
    [SerializeField] private float leapUpwardVelocity = 12f;
    [SerializeField] private float gravityScale = 30f;

    [Header("Impact Mechanics")]
    [SerializeField] private float slamRadius = 6f;
    [SerializeField] private int slamDamage = 45;
    [SerializeField] private float knockupHeight = 1.5f; // How high they are popped up
    [SerializeField] private float knockupDuration = 0.5f; // Suspension time
    [SerializeField] private float inwardPullDistance = 2f; // Distance pulled toward center

    private PlayerController _playerController;
    private CharacterController _characterController;

    public void Initialize(GameObject playerRoot)
    {
        _playerController = playerRoot.GetComponent<PlayerController>();
        _characterController = playerRoot.GetComponent<CharacterController>();
    }

    protected override void Activate()
    {
        if (_playerController == null || _characterController == null)
        {
            isExecuting = false;
            return;
        }

        StartCoroutine(SlamRoutine());
    }

    private IEnumerator SlamRoutine()
    {
        _playerController.IsDashing = true;

        Vector3 forwardDir = transform.root.forward;
        forwardDir.y = 0f;
        forwardDir.Normalize();

        Vector3 airVelocity = (forwardDir * leapForwardSpeed) + (Vector3.up * leapUpwardVelocity);
        yield return new WaitForSeconds(0.05f); 

        while (true)
        {
            airVelocity.y -= gravityScale * Time.deltaTime;
            CollisionFlags flags = _characterController.Move(airVelocity * Time.deltaTime);
            
            if ((flags & CollisionFlags.Below) != 0)
                break;

            yield return null;
        }

        ExecuteImpactEffects();

        _playerController.SetVelocity(Vector3.zero);
        _playerController.IsDashing = false;

        StartCooldown();
    }

    private void ExecuteImpactEffects()
    {
        Vector3 impactPosition = _characterController.transform.position;

        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, impactPosition, Quaternion.identity);
        }

        Collider[] hitColliders = Physics.OverlapSphere(impactPosition, slamRadius);
        
        foreach (Collider target in hitColliders)
        {
            var myTeam = transform.root.GetComponent<TeamMember>();
                
            if (myTeam != null && !myTeam.IsEnemy(target.transform.root.gameObject))
            {
                continue;
            }

            IDamageable damageable = target.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(slamDamage);

                // Handle NavMeshAgent crowd control natively
                NavMeshAgent agent = target.GetComponentInParent<NavMeshAgent>();
                EnemyAI enemyAI = target.GetComponentInParent<EnemyAI>();

                if (agent != null && agent.enabled)
                {
                    // Calculate pull direction toward the center point
                    Vector3 toCenter = impactPosition - target.transform.position;
                    toCenter.y = 0f;
                    Vector3 pullDirection = toCenter.normalized;

                    // Stun the AI mind so it stops pathfinding tracking loops
                    if (enemyAI != null)
                    {
                        enemyAI.Stun(knockupDuration + 0.5f); // Stun stays slightly longer than airtime
                    }

                    // Suspend them into the air safely via NavMesh Agent separation overriding
                    StartCoroutine(AnimateAgentKnockup(agent, pullDirection));
                }
            }
        }
    }

    private IEnumerator AnimateAgentKnockup(NavMeshAgent agent, Vector3 pullDirection)
    {
        // Stop pathfinding tracking entirely
        agent.isStopped = true;

        float elapsed = 0f;
        Vector3 pullStep = pullDirection * (inwardPullDistance / knockupDuration);

        while (elapsed < knockupDuration)
        {
            float normalizedTime = elapsed / knockupDuration;
            // Parabolic curve arc height for visual tracking lift
            float currentHeight = Mathf.Sin(normalizedTime * Mathf.PI) * knockupHeight;

            // Move the agent horizontally along the NavMesh layout floor plane
            agent.Move(pullStep * Time.deltaTime);

            // Directly displace transform rendering vertically to simulate air suspension visually
            Transform modelTransform = agent.transform;
            Vector3 targetPos = modelTransform.position;
            
            // Sample standard NavMesh height position, then stack upward height offset rendering
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                modelTransform.position = new Vector3(targetPos.x, hit.position.y + currentHeight, targetPos.z);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Snap cleanly back onto layout geometry floor safely before handing controls back
        if (NavMesh.SamplePosition(agent.transform.position, out NavMeshHit finalFloor, 4f, NavMesh.AllAreas))
        {
            agent.transform.position = finalFloor.position;
        }

        agent.isStopped = false;
    }
}