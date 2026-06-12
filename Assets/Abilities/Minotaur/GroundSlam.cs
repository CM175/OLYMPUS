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

    // REFACTORED: Use decoupled abstraction here instead of PlayerController directly
    private IMovementController _movementController;
    private CharacterController _characterController; // May be null for AI
    private Transform _rootTransform;

    public void Initialize(GameObject playerRoot)
    {
        _rootTransform = playerRoot.transform;
        _characterController = playerRoot.GetComponent<CharacterController>();
        _movementController = playerRoot.GetComponent<IMovementController>();
    }

    protected override void Activate()
    {
        if (_movementController == null)
        {
            isExecuting = false;
            return;
        }

        StartCoroutine(SlamRoutine());
    }

    private IEnumerator SlamRoutine()
    {
        _movementController.IsDashing = true;

        // FIXED: Safe directional fallback if playerCamera is null (for AI bots)
        Vector3 forwardDir = (playerCamera != null) ? playerCamera.transform.forward : _rootTransform.forward;
        forwardDir.y = 0f;
        forwardDir.Normalize();

        Vector3 airVelocity = (forwardDir * leapForwardSpeed) + (Vector3.up * leapUpwardVelocity);
        yield return new WaitForSeconds(0.05f);

        while (true)
        {
            airVelocity.y -= gravityScale * Time.deltaTime;
            Vector3 frameMovement = airVelocity * Time.deltaTime;

            // REFACTORED: Displace layout coordinates via the abstraction interface
            _movementController.TeleportOrMove(frameMovement);

            // Check if we've landed. CharacterController has a built-in grounded flag;
            // AI agents (no CharacterController) use a NavMesh proximity check instead.
            bool grounded;
            if (_characterController != null)
            {
                grounded = _characterController.isGrounded;
            }
            else
            {
                grounded = airVelocity.y < 0f
                    && NavMesh.SamplePosition(_rootTransform.position, out NavMeshHit groundHit, 0.3f, NavMesh.AllAreas)
                    && Mathf.Abs(groundHit.position.y - _rootTransform.position.y) < 0.15f;
            }

            if (grounded)
                break;

            yield return null;
        }

        ExecuteImpactEffects();

        _movementController.HaltVelocity();
        _movementController.IsDashing = false;

        StartCooldown();
    }

    private void ExecuteImpactEffects()
    {
        Vector3 impactPosition = _rootTransform.position;

        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, impactPosition, Quaternion.identity);
        }

        Collider[] hitColliders = Physics.OverlapSphere(impactPosition, slamRadius);
        
        foreach (Collider target in hitColliders)
        {
            var myTeam = _rootTransform.GetComponent<TeamMember>();
                
            if (myTeam != null && !myTeam.IsEnemy(target.transform.root.gameObject))
            {
                continue;
            }

            IDamageable damageable = target.transform.root.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(slamDamage);

                // Handle NavMeshAgent crowd control natively
                NavMeshAgent agent = target.transform.root.GetComponent<NavMeshAgent>();
                EnemyAI enemyAI = target.transform.root.GetComponent<EnemyAI>();

                if (agent != null && agent.enabled)
                {
                    Vector3 toCenter = impactPosition - target.transform.position;
                    toCenter.y = 0f;
                    Vector3 pullDirection = toCenter.normalized;

                    if (enemyAI != null)
                    {
                        enemyAI.Stun(knockupDuration + 0.5f); 
                    }

                    StartCoroutine(AnimateAgentKnockup(agent, pullDirection));
                }
            }
        }
    }

    private IEnumerator AnimateAgentKnockup(NavMeshAgent agent, Vector3 pullDirection)
    {
        agent.isStopped = true;

        float elapsed = 0f;
        Vector3 pullStep = pullDirection * (inwardPullDistance / knockupDuration);

        while (elapsed < knockupDuration)
        {
            float normalizedTime = elapsed / knockupDuration;
            float currentHeight = Mathf.Sin(normalizedTime * Mathf.PI) * knockupHeight;

            agent.Move(pullStep * Time.deltaTime);

            Transform modelTransform = agent.transform;
            Vector3 targetPos = modelTransform.position;
            
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                modelTransform.position = new Vector3(targetPos.x, hit.position.y + currentHeight, targetPos.z);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (NavMesh.SamplePosition(agent.transform.position, out NavMeshHit finalFloor, 4f, NavMesh.AllAreas))
        {
            agent.transform.position = finalFloor.position;
        }

        agent.isStopped = false;
    }
}