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

            _characterController.Move(movementThisFrame);

            int wallMask = LayerMask.GetMask("Ground");

            if (Physics.Raycast(
                _characterController.transform.position,
                chargeDirection,
                out RaycastHit ray,
                1.2f,
                wallMask))
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

                            Vector3 forward = transform.root.forward;
                            forward.y = 0f;
                            forward.Normalize();

                            Vector3 toEnemy = enemyAgent.transform.position - _characterController.transform.position;
                            toEnemy.y = 0f;
                            toEnemy.Normalize();

                            Vector3 sideDir = Vector3.Cross(forward, Vector3.up).normalized;

                            if (Vector3.Dot(toEnemy, sideDir) < 0)
                                sideDir = -sideDir;
                            StartCoroutine(AnimateChargeBounce(enemyAgent, sideDir));
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

    private IEnumerator AnimateChargeBounce(NavMeshAgent agent, Vector3 direction)
    {
        agent.isStopped = true;

        float duration = 0.35f;
        float elapsed = 0f;

        direction += Random.insideUnitSphere * 0.1f;
        direction.y = 0;
        direction.Normalize();

        Vector3 horizontalStep = direction * 3f;
        float arcHeight = 0.5f;

        EnemyAI ai = agent.GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.Stun(duration + 0.2f);
        }

        Transform t = agent.transform;

        Vector3 startPos = t.position;

        while (elapsed < duration)
        {
            float tNorm = elapsed / duration;

            // smooth arc (like GroundSlam knockup)
            float heightOffset = Mathf.Sin(tNorm * Mathf.PI) * arcHeight;

            Vector3 nextPos = startPos + (horizontalStep * tNorm);
            nextPos.y += heightOffset;

            if (NavMesh.SamplePosition(nextPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                t.position = hit.position + Vector3.up * heightOffset;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // snap back to ground cleanly
        if (NavMesh.SamplePosition(t.position, out NavMeshHit finalHit, 4f, NavMesh.AllAreas))
        {
            t.position = finalHit.position;
        }

        agent.isStopped = false;
    }
}