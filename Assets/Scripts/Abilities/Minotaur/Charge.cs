using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Charge : Ability, IInitializable
{
    [Header("Charge Speeds")]
    [SerializeField] private float chargeSpeed = 5f;
    [SerializeField] private float chargeDuration = 2.0f;
    [SerializeField] private float hitSweepRadius = 2.0f;

    [Header("Damage Profile")]
    [SerializeField] private int impactDamage = 30; 
    [SerializeField] private int pinWallCrushDamage = 75; 
    [SerializeField] private float horizontalImpulse = 20f;
    [SerializeField] private float verticalImpulse = 5f;
    [SerializeField] private float turnRate = 45f; // Max turning speed in degrees per second
    [SerializeField] private float wallCrushStunDuration = 0.5f; // Duration of stun when crushed against a hard wall

    private IMovementController _movementController;
    private Transform _rootTransform;
    
    private IMovementController _pinnedMovement = null;
    private IDamageable pinnedDamageable = null;
    private Transform _pinnedTransform = null;

    private HashSet<Transform> _hitSecondaryTargets = new HashSet<Transform>();

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

        StartCoroutine(ChargeRoutine());
    }

    private bool HasPinnedTarget => _pinnedMovement != null;

    private IEnumerator ChargeRoutine()
    {
        
        //Debug.Log($"{transform.root.name} is Executing charge ");
        _pinnedMovement = null;
        _pinnedTransform = null;
        _hitSecondaryTargets.Clear(); 

        int characterLayer = LayerMask.NameToLayer("Character");
        if (characterLayer != -1)
        {
            Physics.IgnoreLayerCollision(characterLayer, characterLayer, true);
        }

        _movementController.IsDashing = true;
        float speed = chargeSpeed;

        float elapsedTime = 0f;
        Vector3 chargeDirection = _movementController.GetAimDirection();
        chargeDirection.y = 0f;
        chargeDirection = chargeDirection.sqrMagnitude > 0.001f ? chargeDirection.normalized : _rootTransform.forward;

        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, _rootTransform.position, Quaternion.LookRotation(chargeDirection));
        }

        while (elapsedTime < chargeDuration && isExecuting)
        {
            if (HasPinnedTarget && pinnedDamageable.IsDead)
            {
                
                speed = chargeSpeed;
                pinnedDamageable = null;
                _pinnedMovement = null;
                _pinnedTransform = null;
            }

            Vector3 targetDirection = _movementController.GetAimDirection();
            targetDirection.y = 0f;

            if (targetDirection.sqrMagnitude > 0.001f)
            {
                targetDirection.Normalize();

                // 3. Slerp/Rotate toward the target direction by a maximum degree angle per second
                float step = turnRate * Mathf.Deg2Rad * Time.deltaTime;
                chargeDirection = Vector3.RotateTowards(chargeDirection, targetDirection, step, 0.0f);
            }

            // 4. Move forward using the clamped charge direction
            Vector3 movementThisFrame = chargeDirection * speed * Time.deltaTime;
            _movementController.Move(movementThisFrame);

            // Optional: Align the player model to face the actual charge direction
            _rootTransform.rotation = Quaternion.LookRotation(chargeDirection);

            int wallMask = LayerMask.GetMask("Ground");

            if (Physics.Raycast(_rootTransform.position, chargeDirection, out RaycastHit ray, 1.2f, wallMask))
            {
                if (HasPinnedTarget) ExecuteWallCrush();
                yield return new WaitForSeconds(0.5f);
                break;
            }

            Vector3 sweepCenter = _rootTransform.position + Vector3.up + (chargeDirection * 0.5f);
            Collider[] hits = Physics.OverlapSphere(sweepCenter, hitSweepRadius);

            foreach (var hit in hits)
            {
                Transform targetRoot = hit.transform.root;
                
                var myTeam = _rootTransform.GetComponent<TeamMember>();
                
                if (myTeam != null && !myTeam.IsEnemy(targetRoot.gameObject) || targetRoot == _rootTransform)
                {
                    continue;
                }

                IDamageable damageable = targetRoot.GetComponent<IDamageable>() ?? targetRoot.GetComponentInChildren<IDamageable>();
                if (damageable == null) continue;

                IMovementController targetMovement = targetRoot.GetComponent<IMovementController>();
                if (targetMovement == null) continue;
                
                if (!HasPinnedTarget)
                {
                    _pinnedMovement = targetMovement;
                    _pinnedTransform = targetRoot;

                    _pinnedMovement.HaltVelocity();
                    pinnedDamageable = _pinnedTransform.GetComponent<IDamageable>() ?? _pinnedTransform.GetComponentInChildren<IDamageable>();
                    Deal(pinnedDamageable, impactDamage);
                    // OPTIMIZED: Stun the target for the remaining duration of the charge ride
                    float remainingChargeTime = chargeDuration - elapsedTime;
                    _pinnedMovement.Stun(remainingChargeTime + 0.5f); 

                    speed *= 0.85f; 
                }
                else if (targetRoot != _pinnedTransform && !_hitSecondaryTargets.Contains(targetRoot))
                {
                    _hitSecondaryTargets.Add(targetRoot);
                    Deal(damageable, (int)(impactDamage * 0.5f));
                    
                    // Secondary side-swiped targets get a brief generic 1-second hit stun
                    targetMovement.Stun(1.0f);
                    ApplySideBounce(targetMovement, targetRoot);
                }
            }

            if (_pinnedTransform != null && _pinnedMovement != null)
            {
                Vector3 desiredPinnedPosition = _rootTransform.position + (chargeDirection * 1.5f);
                desiredPinnedPosition.y = _pinnedTransform.position.y;

                Vector3 frameDisplacement = desiredPinnedPosition - _pinnedTransform.position;
                _pinnedMovement.Move(frameDisplacement * (Time.deltaTime * 25f));
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        if (characterLayer != -1)
        {
            Physics.IgnoreLayerCollision(characterLayer, characterLayer, false);
        }

        // Open Ground release: target takes normal impact damage and comes out of stun naturally
        if (_pinnedTransform != null)
        {
            

            if (_pinnedMovement != null)
            {
                _pinnedMovement.HaltVelocity();
                
            }
        }
        
        yield return new WaitForSeconds(0.5f);
        _movementController.HaltVelocity();
        _movementController.IsDashing = false;

        StartCooldown();
    }

    private void ExecuteWallCrush()
    {
        if (_pinnedTransform != null)
        {
            IDamageable target = _pinnedTransform.GetComponent<IDamageable>() ?? _pinnedTransform.GetComponentInChildren<IDamageable>();
            Deal(target, pinWallCrushDamage);

            if (_pinnedMovement != null)
            {
                _pinnedMovement.HaltVelocity();
                
                
                _pinnedMovement.Stun(wallCrushStunDuration);
            }

            _pinnedMovement = null;
            _pinnedTransform = null;
        }
    }

    private void ApplySideBounce(IMovementController targetMovement, Transform targetTransform)
    {
        Vector3 forward = _rootTransform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 toEnemy = targetTransform.position - _rootTransform.position;
        toEnemy.y = 0f;

        Vector3 sideDir = Vector3.Cross(forward, Vector3.up).normalized;

        if (toEnemy.sqrMagnitude > 0.001f && Vector3.Dot(toEnemy.normalized, sideDir) < 0)
        {
            sideDir = -sideDir;
        }

        // Single impulse: 14 horizontal, 5 vertical launch
        Vector3 impulse = (sideDir * horizontalImpulse) + (Vector3.up * verticalImpulse);
        targetMovement.HaltVelocity();
        targetMovement.ApplyImpulseForce(impulse);
    }
}