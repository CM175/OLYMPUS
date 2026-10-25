using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AIMovementController))]
[RequireComponent(typeof(AbilityManager))]
[RequireComponent(typeof(TeamMember))]
public class RLEnemyCombatAgent : Agent
{
    private enum PositioningAction { Hold = 0, Advance = 1, Retreat = 2, Strafe = 3 }

    [Header("Macro / Navigation")]
    [SerializeField] private float detectionRange = 25f;
    [SerializeField] private LayerMask searchMask;
    [SerializeField] private LayerMask lineOfSightBlockMask; 
    [SerializeField] private Transform defaultObjective;
    [SerializeField] private Transform trainingSpawnPoint;
    
    private Vector3 _initialSpawnPosition;
    
    private Quaternion _initialSpawnRotation;

    [Header("Aim Settings")]
    [SerializeField] private float maxAimLeadAngle = 15f;
    [SerializeField] private float aimTurnSpeed = 15f;

    [Header("Reward Shaping")]
    [SerializeField] private float damageDealtRewardScale = 0.05f;
    [SerializeField] private float damageTakenPenaltyScale = 0.03f;
    [SerializeField] private float killReward = 1.0f;
    [SerializeField] private float deathPenalty = -1.0f;
    [SerializeField] private float abilityHitBonus = 0.02f;
    [SerializeField] private float existentialPenaltyPerStep = -0.0005f;

    private NavMeshAgent _agent;
    private AIMovementController _movement;
    private AbilityManager _abilityManager;
    private TeamMember _teamMember;
    private IDamageable _selfDamageable;

    private Transform _currentTarget;
    private IDamageable _targetDamageable;
    private Vector3 _lastTargetPosition;
    private Vector3 _targetVelocity;
    private bool _hasLineOfSight;

    private Quaternion _desiredAimRotation;
    private bool _hasAimTarget;
    private PositioningAction _positioningAction = PositioningAction.Hold;
    private bool _isInitialized = false;
    private bool _hasSpawnCached = false;
    private void Awake()
    {
        CacheSpawnPosition();
    }

    public void SetSpawnPoint(Transform spawn)
    {
        trainingSpawnPoint = spawn;
        _initialSpawnPosition = spawn.position;
        _initialSpawnRotation = spawn.rotation;
        _hasSpawnCached = true;
    }

    private void CacheSpawnPosition()
    {
        if (_hasSpawnCached) return;

        if (trainingSpawnPoint != null)
        {
            _initialSpawnPosition = trainingSpawnPoint.position;
            _initialSpawnRotation = trainingSpawnPoint.rotation;
        }
        else
        {
            _initialSpawnPosition = transform.position;
            _initialSpawnRotation = transform.rotation;
        }
        _hasSpawnCached = true;
    }
    public override void Initialize()
    {
        _agent = GetComponent<NavMeshAgent>();
        _movement = GetComponent<AIMovementController>();
        _abilityManager = GetComponent<AbilityManager>();
        _teamMember = GetComponent<TeamMember>();
        _selfDamageable = GetComponent<IDamageable>();

        if (defaultObjective == null)
            defaultObjective = GameObject.FindWithTag("Objective")?.transform;

        if (_agent != null)
            _agent.updateRotation = false;

        if (_selfDamageable != null)
        {
            _selfDamageable.OnDamageReceived += HandleSelfDamageTaken;
            _selfDamageable.OnDied += HandleSelfDeath;
        }

        // Automatically set dummy mode on health if training
        if (TryGetComponent<EnemyHealth>(out var eh))
        {
            eh.isTrainingDummy = true;
        }

        _initialSpawnPosition = transform.position;
        _initialSpawnRotation = transform.rotation;
        _isInitialized = true;
    }

    public override void OnEpisodeBegin()
    {
        Debug.Log($"[RLEnemyCombatAgent] OnEpisodeBegin() called for {gameObject.name}");
        CacheSpawnPosition();

        UnsubscribeFromTarget();
        _currentTarget = null;
        _targetDamageable = null;
        _targetVelocity = Vector3.zero;
        _hasLineOfSight = false;
        _hasAimTarget = false;

        if (_abilityManager != null)
            _abilityManager.InterruptAllAbilities();

        // 1. Revive health
        if (TryGetComponent<EnemyHealth>(out var eh))
        {
            eh.ResetHealth();
        }

        // 2. Resolve Spawn Target
        Vector3 targetSpawnPos = trainingSpawnPoint != null ? trainingSpawnPoint.position : _initialSpawnPosition;
        Quaternion targetSpawnRot = trainingSpawnPoint != null ? trainingSpawnPoint.rotation : _initialSpawnRotation;

        // 3. Forcibly disable NavMeshAgent before transform displacement
        if (_agent != null)
        {
            _agent.enabled = false;
        }

        // 4. Reset velocities and physics states
        if (_movement != null)
        {
            _movement.HaltVelocity();
            _movement.IsDashing = false;
            _movement.IsUnderExternalForce = false;
        }

        // 5. Relocate Transform
        transform.position = targetSpawnPos;
        transform.rotation = targetSpawnRot;

        // 6. Snap cleanly to NavMesh surface and re-enable
        if (NavMesh.SamplePosition(targetSpawnPos, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
        }

        if (_agent != null)
        {
            _agent.enabled = true;
            _agent.Warp(transform.position);
            _agent.ResetPath();
            _agent.velocity = Vector3.zero;
        }
    }
    private void Update()
    {
        FindTarget();
        UpdateTargetVelocity();
        UpdateLineOfSight();
        ExecuteMacroNavigation();
        ApplyAimRotation();
    }

    private void ExecuteMacroNavigation()
    {
        if (_movement.IsUnderExternalForce || _movement.IsStunned)
            return;

        if (_currentTarget != null)
        {
            float combatRange = _abilityManager.primaryAttack != null ? _abilityManager.primaryAttack.range : 15f;
            Vector3 destination = ComputePositioningDestination(combatRange);
            _agent.stoppingDistance = 0.5f;
            if (_agent.isOnNavMesh)
                _agent.SetDestination(destination);
        }
        else if (defaultObjective != null)
        {
            if (!_agent.isOnNavMesh) return;
            _agent.stoppingDistance = 2f;
            _agent.SetDestination(defaultObjective.position);
        }
    }

    private Vector3 ComputePositioningDestination(float combatRange)
    {
        Vector3 targetPos = _currentTarget.position;
        Vector3 awayFromTarget = (transform.position - targetPos);
        awayFromTarget.y = 0f;
        float currentDistance = awayFromTarget.magnitude;
        Vector3 awayDir = awayFromTarget.sqrMagnitude > 0.001f ? awayFromTarget.normalized : transform.forward;

        Vector3 rawDestination = targetPos + awayDir * (combatRange * 0.7f);

        switch (_positioningAction)
        {
            case PositioningAction.Advance:
                rawDestination = targetPos + awayDir * (combatRange * 0.4f);
                break;
            case PositioningAction.Retreat:
                rawDestination = targetPos + awayDir * (combatRange * 1.2f);
                break;
            case PositioningAction.Strafe:
                float orbitDistance = Mathf.Clamp(currentDistance, combatRange * 0.5f, combatRange * 1.1f);
                Vector3 lateral = Vector3.Cross(Vector3.up, awayDir).normalized;
                float side = (GetInstanceID() % 2 == 0) ? 1f : -1f;
                Vector3 orbitDir = (awayDir + lateral * side * 0.6f).normalized;
                rawDestination = targetPos + orbitDir * orbitDistance;
                break;
            case PositioningAction.Hold:
            default:
                rawDestination = targetPos + awayDir * (combatRange * 0.7f);
                break;
        }

        // CLAMP TO VALID NAVMESH SURFACE: Never send the agent over an edge
        if (NavMesh.SamplePosition(rawDestination, out NavMeshHit hit, 4.0f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        // If candidate point is off-island, retreat to the central objective
        return defaultObjective != null ? defaultObjective.position : transform.position;
    }

    private void FindTarget()
    {
        if (_currentTarget != null && (!_currentTarget.gameObject.activeInHierarchy || Vector3.Distance(transform.position, _currentTarget.position) > detectionRange * 1.5f))
        {
            UnsubscribeFromTarget();
            _currentTarget = null;
            _targetDamageable = null;
        }

        if (_currentTarget != null) return;

        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRange, searchMask);
        float closestDist = float.MaxValue;
        Transform bestTarget = null;
        IDamageable bestDamageable = null;

        foreach (var col in colliders)
        {
            if (col.gameObject == gameObject) continue;
            GameObject rootObj = col.transform.root.gameObject;

            if (_teamMember.IsEnemy(rootObj))
            {
                IDamageable dmg = rootObj.GetComponent<IDamageable>();
                if (dmg == null || dmg.IsDead) continue;

                // LOS Check before picking target
                Vector3 origin = transform.position + Vector3.up * 1.5f;
                Vector3 targetPoint = rootObj.transform.position + Vector3.up * 1.0f;
                Vector3 dir = targetPoint - origin;
                
                if (Physics.Raycast(origin, dir.normalized, dir.magnitude, lineOfSightBlockMask))
                    continue; // Hidden behind obstacle

                float dist = Vector3.Distance(transform.position, rootObj.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bestTarget = rootObj.transform;
                    bestDamageable = dmg;
                }
            }
        }

        if (bestTarget != null)
        {
            _currentTarget = bestTarget;
            _targetDamageable = bestDamageable;
            _lastTargetPosition = bestTarget.position;

            _targetDamageable.OnDamageReceived += HandleTargetDamageTaken;
            _targetDamageable.OnDied += HandleTargetDeath;
        }
    }

    private void UpdateTargetVelocity()
    {
        if (_currentTarget == null) return;
        _targetVelocity = (_currentTarget.position - _lastTargetPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        _lastTargetPosition = _currentTarget.position;
    }

    private void UpdateLineOfSight()
    {
        if (_currentTarget == null)
        {
            _hasLineOfSight = false;
            return;
        }

        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 targetPoint = _currentTarget.position + Vector3.up * 1.0f;
        Vector3 dir = targetPoint - origin;

        _hasLineOfSight = !Physics.Raycast(origin, dir.normalized, dir.magnitude, lineOfSightBlockMask);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1. Self State (8 values)
        sensor.AddObservation(_selfDamageable != null ? (float)_selfDamageable.CurrentHealth / _selfDamageable.MaxHealth : 1f);
        sensor.AddObservation(_movement.IsStunned ? 1f : 0f);
        sensor.AddObservation(_movement.IsDashing ? 1f : 0f);
        sensor.AddObservation(GetCooldownNormalized(_abilityManager.primaryAttack));
        sensor.AddObservation(GetCooldownNormalized(_abilityManager.primaryAbility));
        sensor.AddObservation(GetCooldownNormalized(_abilityManager.secondaryAbility));
        sensor.AddObservation(GetCooldownNormalized(_abilityManager.mobilityAbility));
        sensor.AddObservation(GetCooldownNormalized(_abilityManager.ultimateAbility));

        // 2. Target Context (9 values)
        if (_currentTarget != null)
        {
            Vector3 toTarget = _currentTarget.position - transform.position;
            sensor.AddObservation(toTarget.normalized);
            sensor.AddObservation(Mathf.Clamp01(toTarget.magnitude / detectionRange));
            sensor.AddObservation(Vector3.ClampMagnitude(_targetVelocity / 10f, 1f)); // normalized velocity
            sensor.AddObservation(_targetDamageable != null ? (float)_targetDamageable.CurrentHealth / _targetDamageable.MaxHealth : 1f);
            sensor.AddObservation(_hasLineOfSight ? 1f : 0f);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(1f);
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        bool exclusiveLocked = _abilityManager.HasActiveExclusiveAbility();
        MaskAbilitySlot(actionMask, 1, _abilityManager.primaryAttack, exclusiveLocked);
        MaskAbilitySlot(actionMask, 2, _abilityManager.primaryAbility, exclusiveLocked);
        MaskAbilitySlot(actionMask, 3, _abilityManager.secondaryAbility, exclusiveLocked);
        MaskAbilitySlot(actionMask, 4, _abilityManager.mobilityAbility, exclusiveLocked);
        MaskAbilitySlot(actionMask, 5, _abilityManager.ultimateAbility, exclusiveLocked);
    }

    private void MaskAbilitySlot(IDiscreteActionMask actionMask, int actionIndex, Ability ability, bool exclusiveLocked)
    {
        bool unavailable = ability == null || ability.IsOnCooldown() || exclusiveLocked;
        if (unavailable)
            actionMask.SetActionEnabled(1, actionIndex, false);
    }

    private float GetCooldownNormalized(Ability ability)
    {
        if (ability == null || ability.cooldown <= 0f) return 0f;
        float remaining = (ability.lastUsedTime + ability.cooldown) - Time.time;
        return Mathf.Clamp01(remaining / ability.cooldown);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Step penalty applied reliably per decision tick
        AddReward(existentialPenaltyPerStep);

        if (_movement == null || _currentTarget == null)
        {
            _hasAimTarget = false;
            return;
        }

        var discreteActions = actions.DiscreteActions;
        if (discreteActions.Length < 3)
        {
            Debug.LogError($"RLEnemyCombatAgent requires 3 discrete branches, but received {discreteActions.Length}.");
            return;
        }

        if (_movement.IsStunned || _movement.IsUnderExternalForce)
        {
            _hasAimTarget = false;
            return;
        }

        // Branch 0: Aim
        int aimAction = discreteActions[0];
        float aimAngleOffset = (aimAction - 1) * maxAimLeadAngle;
        Vector3 directionToTarget = _currentTarget.position - transform.position;
        directionToTarget.y = 0f;

        if (directionToTarget.sqrMagnitude > 0.001f)
        {
            _desiredAimRotation = Quaternion.LookRotation(directionToTarget.normalized) * Quaternion.Euler(0, aimAngleOffset, 0);
            _hasAimTarget = true;
        }
        else
        {
            _hasAimTarget = false;
        }

        // Branch 1: Combat
        int combatAction = discreteActions[1];
        ExecuteCombatSlot(combatAction);

        // Branch 2: Positioning
        _positioningAction = (PositioningAction)discreteActions[2];
    }

    private void ApplyAimRotation()
    {
        if (!_hasAimTarget) return;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            _desiredAimRotation,
            Time.deltaTime * aimTurnSpeed
        );
    }

    private void ExecuteCombatSlot(int actionIndex)
    {
        Ability targetAbility = null;
        switch (actionIndex)
        {
            case 0:
                if (_abilityManager.primaryAttack != null && _abilityManager.primaryAttack.isHolding)
                    _abilityManager.primaryAttack.OnRelease();
                return;
            case 1: targetAbility = _abilityManager.primaryAttack; break;
            case 2: targetAbility = _abilityManager.primaryAbility; break;
            case 3: targetAbility = _abilityManager.secondaryAbility; break;
            case 4: targetAbility = _abilityManager.mobilityAbility; break;
            case 5: targetAbility = _abilityManager.ultimateAbility; break;
        }

        if (targetAbility == null) return;
        if (targetAbility.IsOnCooldown() || _abilityManager.HasActiveExclusiveAbility()) return;

        if (targetAbility is Attack weapon)
        {
            weapon.OnPress();
            weapon.Use();
        }
        else
        {
            targetAbility.Use();
        }
    }

    private void HandleSelfDamageTaken(int damage, GameObject attacker)
    {
        AddReward(-damage * damageTakenPenaltyScale);
    }

    private void HandleSelfDeath(GameObject killer)
    {
        AddReward(deathPenalty);
        EndEpisode();
    }

    private void HandleTargetDeath(GameObject killer)
    {
        // If the target is also an ML-Agent (self-play or sparring AI),
        // force it to end its episode so it also respawns!
        if (_currentTarget != null && _currentTarget.TryGetComponent<Agent>(out var targetAgent))
        {
            targetAgent.EndEpisode();
        }

        UnsubscribeFromTarget();
        _currentTarget = null;
        _targetDamageable = null;

        if (killer == gameObject)
            AddReward(killReward);

        EndEpisode();
    }

    private void HandleTargetDamageTaken(int damage, GameObject attacker)
    {
        if (attacker != gameObject) return;
        AddReward(damage * damageDealtRewardScale);
        AddReward(abilityHitBonus);
    }


    private void UnsubscribeFromTarget()
    {
        if (_targetDamageable == null) return;
        _targetDamageable.OnDamageReceived -= HandleTargetDamageTaken;
        _targetDamageable.OnDied -= HandleTargetDeath;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (!_isInitialized) return;
        var discreteActions = actionsOut.DiscreteActions;

        if (discreteActions.Length > 0)
            discreteActions[0] = 1;

        if (discreteActions.Length > 1)
            discreteActions[1] = Input.GetKey(KeyCode.Space) ? 1 : 0;

        if (discreteActions.Length > 2)
        {
            if (Input.GetKey(KeyCode.LeftShift))
                discreteActions[2] = (int)PositioningAction.Retreat;
            else if (Input.GetKey(KeyCode.LeftControl))
                discreteActions[2] = (int)PositioningAction.Advance;
            else if (Input.GetKey(KeyCode.LeftAlt))
                discreteActions[2] = (int)PositioningAction.Strafe;
            else
                discreteActions[2] = (int)PositioningAction.Hold;
        }
    }

    private void OnDestroy()
    {
        if (_selfDamageable != null)
        {
            _selfDamageable.OnDamageReceived -= HandleSelfDamageTaken;
            _selfDamageable.OnDied -= HandleSelfDeath;
        }
        UnsubscribeFromTarget();
    }
}