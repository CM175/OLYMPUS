using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;

public class Charge : Ability, IInitializable
{
    [Header("Charge Speeds")]
    [SerializeField] private float chargeSpeed = 5f;
    [SerializeField] private float chargeDuration = 2.0f;
    [SerializeField] private float hitSweepRadius = 2.0f;

    [Header("Damage Profile")]
    [SerializeField] private int impactDamage = 30; 
    [SerializeField] private int pinWallCrushDamage = 75; 

    private PlayerController _playerController;
    private CharacterController _characterController;
    private NavMeshAgent _pinnedAgent = null;
    private EnemyAI _pinnedAI = null;

    // Registry to track who we have already side-swiped
    private HashSet<Transform> _hitSecondaryTargets = new HashSet<Transform>();

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

        StartCoroutine(ChargeRoutine());
    }

    private IEnumerator ChargeRoutine()
    {
        _pinnedAgent = null;
        _pinnedAI = null;
        _hitSecondaryTargets.Clear(); // Reset registry at the start of the skill

        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");

        if (playerLayer != -1 && enemyLayer != -1)
        {
            Physics.IgnoreLayerCollision(playerLayer, enemyLayer, true);
        }

        _playerController.IsDashing = true;
        float speed = chargeSpeed;

        float elapsedTime = 0f;
        Vector3 chargeDirection = transform.root.forward;
        chargeDirection.y = 0f;
        chargeDirection.Normalize();

        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, _characterController.transform.position, Quaternion.LookRotation(chargeDirection));
        }

        while (elapsedTime < chargeDuration)
        {
            Vector3 movementThisFrame = chargeDirection * speed * Time.deltaTime;
            movementThisFrame.y = -4f * Time.deltaTime; 

            CollisionFlags flags = _characterController.Move(movementThisFrame);

            if ((flags & CollisionFlags.Sides) != 0 )
            {
                if (_pinnedAgent != null) ExecuteWallCrush();
                yield return new WaitForSeconds(0.5f);
                break;
            }

            Vector3 sweepCenter = _characterController.transform.position + _characterController.center + (chargeDirection * 0.5f);
            Collider[] hits = Physics.OverlapSphere(sweepCenter, hitSweepRadius);

            foreach (var hit in hits)
            {
                if (hit.transform.root == transform.root) continue;

                // Stop multi-frame hitting: skip if this enemy has already been swiped
                if (_hitSecondaryTargets.Contains(hit.transform.root)) continue;

                IDamageable target = hit.GetComponent<IDamageable>();
                if (target != null)
                {
                    NavMeshAgent enemyAgent = hit.GetComponentInParent<NavMeshAgent>();
                    EnemyAI enemyAI = hit.GetComponentInParent<EnemyAI>();

                    if (enemyAgent != null && enemyAgent.enabled)
                    {
                        if (_pinnedAgent == null)
                        {
                            _pinnedAgent = enemyAgent;
                            _pinnedAI = enemyAI;
                            
                            _pinnedAgent.isStopped = true;
                            if (_pinnedAI != null) _pinnedAI.Stun(chargeDuration + 1f);

                            speed *= 0.85f; 
                        }
                        else if (enemyAgent != _pinnedAgent)
                        {
                            // Register them immediately so they don't take damage again next frame
                            _hitSecondaryTargets.Add(hit.transform.root);

                            target.TakeDamage((int)(impactDamage* 0.5f));
                            if (enemyAI != null) enemyAI.Stun(1f);
                            BounceEnemyAside(enemyAgent);
                        }
                    }
                }
            }

            if (_pinnedAgent != null && _pinnedAgent.gameObject != null)
            {
                // Calculate where they should look to be resting flat against your front silhouette
                Vector3 desiredPinnedPosition = _characterController.transform.position + (chargeDirection * 1.5f);
                desiredPinnedPosition.y = _pinnedAgent.transform.position.y;

                // Frame offset vector required to close the gap cleanly
                Vector3 frameDisplacement = desiredPinnedPosition - _pinnedAgent.transform.position;
                
                // Let the NavMesh logic track movement constraints step-by-step
                _pinnedAgent.Move(frameDisplacement * (Time.deltaTime * 25f));
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        if (playerLayer != -1 && enemyLayer != -1)
        {
            Physics.IgnoreLayerCollision(playerLayer, enemyLayer, false);
        }

        if (_pinnedAgent != null && _pinnedAgent.gameObject != null)
        {
            IDamageable targetDamage = _pinnedAgent.GetComponentInChildren<IDamageable>();
            targetDamage?.TakeDamage(impactDamage);
            BounceEnemyAside(_pinnedAgent, 0.1f);
            _pinnedAgent.isStopped = false;
        }

        _playerController.SetVelocity(Vector3.zero);
        _playerController.IsDashing = false;

        StartCooldown();
    }

    private void ExecuteWallCrush()
    {
        IDamageable target = _pinnedAgent.GetComponentInChildren<IDamageable>();
        target?.TakeDamage(pinWallCrushDamage);

        if (_pinnedAI != null)
        {
            _pinnedAI.Stun(1f);
            Debug.Log("stunned by wall");
        }
        
        _pinnedAgent = null;
        _pinnedAI = null;
    }

    private void BounceEnemyAside(NavMeshAgent enemyAgent, float pushMultiplier = 1.0f)
    {
        Vector3 sidePushDir = enemyAgent.transform.position - _characterController.transform.position;
        sidePushDir.y = 0f;
        sidePushDir.Normalize();

        Vector3 shoveVector = sidePushDir * (6f * pushMultiplier);
        enemyAgent.Move(shoveVector);
    }
}