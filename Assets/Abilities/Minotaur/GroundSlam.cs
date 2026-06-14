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
        _movementController.HaltVelocity();

        // Safe directional fallback if playerCamera is null (works perfectly for AI bots)
        Vector3 forwardDir = (playerCamera != null) ? playerCamera.transform.forward : _rootTransform.forward;
        forwardDir.y = 0f;
        //forwardDir.Normalize();

        Vector3 airVelocity = (forwardDir * leapForwardSpeed) + (Vector3.up * leapUpwardVelocity);
        yield return new WaitForSeconds(0.05f);


        while (isExecuting)
        {
            //airVelocity.y -= gravityScale * Time.deltaTime;
            Vector3 frameMovement = airVelocity * Time.deltaTime;

            // Displace layout coordinates completely via the abstraction interface
            _movementController.Move(frameMovement);

            // Ground check abstracted from explicit component type

            if (_movementController.IsGrounded)
            {
                ExecuteImpactEffects();
                break;
            }

            yield return null;
        }
        

        _movementController.HaltVelocity();

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
                    // Inside GroundSlam.cs Impact Effects:
                    Vector3 toCenter = impactPosition - targetRoot.position;
                    toCenter.y = 0f;
                    Vector3 pullDirection = toCenter.normalized;

                    // 1. Calculate inward pull force + upward launch force
                    Vector3 liftImpulse = Vector3.up * Mathf.Sqrt( knockupHeight); // Using your custom gravity scale
                    Vector3 pullImpulse = pullDirection * (inwardPullDistance * 2f); 

                    // 2. Inject directly into the target controller layer
                    targetMovement.HaltVelocity();
                    targetMovement.Stun(knockupDuration); 
                    targetMovement.ApplyImpulseForce(liftImpulse + pullImpulse);
                }
            }
        }
    }

}