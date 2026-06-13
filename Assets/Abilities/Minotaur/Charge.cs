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

    private IMovementController _movementController;
    private Transform _rootTransform;
    
    private IMovementController _pinnedMovement = null;
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
        Vector3 chargeDirection = _rootTransform.forward;
        chargeDirection.y = 0f;
        chargeDirection.Normalize();

        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, _rootTransform.position, Quaternion.LookRotation(chargeDirection));
        }

        while (elapsedTime < chargeDuration && isExecuting)
        {
            Vector3 movementThisFrame = chargeDirection * speed * Time.deltaTime;
            movementThisFrame.y = -4f * Time.deltaTime; 

            _movementController.TeleportOrMove(movementThisFrame);

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

                    // OPTIMIZED: Stun the target for the remaining duration of the charge ride
                    float remainingChargeTime = chargeDuration - elapsedTime;
                    _pinnedMovement.Stun(remainingChargeTime + 0.5f); 

                    speed *= 0.85f; 
                }
                else if (targetRoot != _pinnedTransform && !_hitSecondaryTargets.Contains(targetRoot))
                {
                    _hitSecondaryTargets.Add(targetRoot);
                    damageable.TakeDamage((int)(impactDamage * 0.5f));
                    
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
                _pinnedMovement.TeleportOrMove(frameDisplacement * (Time.deltaTime * 25f));
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
            IDamageable playerDamage = _pinnedTransform.GetComponent<IDamageable>() ?? _pinnedTransform.GetComponentInChildren<IDamageable>();
            playerDamage?.TakeDamage(impactDamage);

            if (_pinnedMovement != null)
            {
                _pinnedMovement.HaltVelocity();
                
            }
        }

        _movementController.HaltVelocity();
        _movementController.IsDashing = false;

        StartCooldown();
    }

    private void ExecuteWallCrush()
    {
        if (_pinnedTransform != null)
        {
            IDamageable target = _pinnedTransform.GetComponent<IDamageable>() ?? _pinnedTransform.GetComponentInChildren<IDamageable>();
            target?.TakeDamage(pinWallCrushDamage);

            if (_pinnedMovement != null)
            {
                _pinnedMovement.HaltVelocity();
                
                
                // OPTIMIZED: Apply an extra 1.5-second impact stun when crushed against a hard wall
                _pinnedMovement.Stun(1.5f);
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
        toEnemy.Normalize();

        Vector3 sideDir = Vector3.Cross(forward, Vector3.up).normalized;

        if (Vector3.Dot(toEnemy, sideDir) < 0)
            sideDir = -sideDir;

        StartCoroutine(AnimateSideBounceRoutine(targetMovement, sideDir));
    }

    private IEnumerator AnimateSideBounceRoutine(IMovementController targetMovement, Vector3 direction)
    {
        targetMovement.IsDashing = true;
        targetMovement.HaltVelocity();

        float duration = 0.35f;
        float elapsed = 0f;

        direction.y = 0;
        direction.Normalize();

        while (elapsed < duration)
        {
            float tNorm = elapsed / duration;
            float horizontalSpeed = Mathf.Lerp(12f, 2f, tNorm); 
            float verticalSpeed = Mathf.Sin(tNorm * Mathf.PI) * 4f; 

            Vector3 stepMove = (direction * horizontalSpeed) + (Vector3.up * verticalSpeed);
            targetMovement.TeleportOrMove(stepMove * Time.deltaTime);

            elapsed += Time.deltaTime;
            yield return null;
        }

        targetMovement.HaltVelocity();
        targetMovement.IsDashing = false;
    }
}