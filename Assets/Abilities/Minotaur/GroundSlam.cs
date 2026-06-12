using UnityEngine;
using System.Collections;
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

    private IMovementController _movementController;
    private Transform _rootTransform;

    public void Initialize(GameObject playerRoot)
    {
        _rootTransform = playerRoot.transform;
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
        Debug.Log($"{_rootTransform.root.name} is executing Ground Slam!");
        _movementController.IsDashing = true;

        // Safe directional fallback if playerCamera is null (works perfectly for AI bots)
        Vector3 forwardDir = (playerCamera != null) ? playerCamera.transform.forward : _rootTransform.forward;
        forwardDir.y = 0f;
        forwardDir.Normalize();

        Vector3 airVelocity = (forwardDir * leapForwardSpeed) + (Vector3.up * leapUpwardVelocity);
        yield return new WaitForSeconds(0.05f);

        // Track the height relative to where the jump started to determine landing cleanly
        float groundY = _rootTransform.position.y;
        if (NavMesh.SamplePosition(_rootTransform.position, out NavMeshHit groundHit, 2f, NavMesh.AllAreas))
        {
            groundY = groundHit.position.y;
        }

        while (true)
        {
            airVelocity.y -= gravityScale * Time.deltaTime;
            Vector3 frameMovement = airVelocity * Time.deltaTime;

            // Displace layout coordinates completely via the abstraction interface
            _movementController.TeleportOrMove(frameMovement);

            // Ground check abstracted from explicit component types
            bool grounded = airVelocity.y < 0f && _rootTransform.position.y <= (groundY + 0.1f);

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
            Transform targetRoot = target.transform.root;
            var myTeam = _rootTransform.GetComponent<TeamMember>();
                
            if (myTeam != null && !myTeam.IsEnemy(targetRoot.gameObject) || targetRoot == _rootTransform)
            {
                continue;
            }

            IDamageable damageable = targetRoot.GetComponent<IDamageable>() ?? targetRoot.GetComponentInChildren<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(slamDamage);

                // Dynamically route crowd control strictly using the target's controller layer
                IMovementController targetMovement = targetRoot.GetComponent<IMovementController>();
                if (targetMovement != null)
                {
                    Vector3 toCenter = impactPosition - targetRoot.position;
                    toCenter.y = 0f;
                    Vector3 pullDirection = toCenter.normalized;

                    StartCoroutine(AnimateTargetKnockup(targetMovement, targetRoot, pullDirection));
                }
            }
        }
    }

    private IEnumerator AnimateTargetKnockup(IMovementController targetMovement, Transform targetTransform, Vector3 pullDirection)
    {
        targetMovement.IsDashing = true;
        targetMovement.HaltVelocity();

        float elapsed = 0f;
        Vector3 pullStep = pullDirection * (inwardPullDistance / knockupDuration);

        float originalY = targetTransform.position.y;

        while (elapsed < knockupDuration)
        {
            float normalizedTime = elapsed / knockupDuration;
            
            // Calculate height via a smooth sine wave arc step
            float desiredHeight = Mathf.Sin(normalizedTime * Mathf.PI) * knockupHeight;

            // Combine horizontal pulling and target height correction into a decoupled vector displacement
            Vector3 targetPosition = targetTransform.position + (pullStep * Time.deltaTime);
            targetPosition.y = originalY + desiredHeight;

            Vector3 displacementThisFrame = targetPosition - targetTransform.position;
            targetMovement.TeleportOrMove(displacementThisFrame);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Snap cleanly back to ground level definitions
        if (NavMesh.SamplePosition(targetTransform.position, out NavMeshHit finalFloor, 4f, NavMesh.AllAreas))
        {
            Vector3 finalSnap = finalFloor.position - targetTransform.position;
            targetMovement.TeleportOrMove(finalSnap);
        }

        targetMovement.HaltVelocity();
        targetMovement.IsDashing = false;
    }
}