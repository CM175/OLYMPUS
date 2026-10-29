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

    [Header("Reward Shaping (damage terms are fractions of max health, so a full kill is about +1)")]
    [SerializeField] private float damageDealtReward = 1.0f;
    [SerializeField] private float damageTakenPenalty = 0.5f;
    [SerializeField] private float killReward = 1.0f;
    [SerializeField] private float deathPenalty = -1.0f;
    [SerializeField] private float existentialPenaltyPerStep = -0.0005f;
    [SerializeField] private float rangeShaping = 0.1f;

    [Header("Episode / Performance")]
    [SerializeField] private int maxEpisodeSteps = 4000;      // ~80s of game time; time-outs bootstrap the value estimate
    [SerializeField] private float navRepathInterval = 0.2f;  // seconds between SetDestination calls
    [SerializeField] private float edgeObservationRange = 10f;

    private NavMeshAgent _agent;
    private AIMovementController _movement;
    private AbilityManager _abilityManager;
    private TeamMember _teamMember;
    private IDamageable _selfDamageable;

    public Transform _currentTarget;
    private IDamageable _targetDamageable;
    private Vector3 _lastTargetPosition;
    private Vector3 _targetVelocity;
    private bool _hasLineOfSight;

    private Quaternion _desiredAimRotation;
    private bool _hasAimTarget;
    private PositioningAction _positioningAction = PositioningAction.Hold;
    private bool _isInitialized = false;
    private bool _hasSpawnCached = false;

    private float _strafeSide = 1f;
    private float _nextNavTime;
    private int _selfHpKnown;
    private int _targetHpKnown;
    private int _lastEndFrame = -1;
    private bool _hasPrevPotential;
    private float _prevPotential;
    private TrainingArena _arena;
    private bool _primaryHeld;
    private float _primaryHeldSince;

    // per-episode diagnostics (reported to TensorBoard)
    private float _epDamageDealt, _epDamageTaken, _epKilled, _epDied;
    private int _epDecisions, _epWithTarget, _epWithLos, _epInRange, _epAttackUses, _epAbilityUses;
    private EnemyHealth _trainingDummy;

    public LayerMask SearchMask => searchMask;
    public float CombatRange => GetCombatRange();
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
        if (MaxStep <= 0) MaxStep = maxEpisodeSteps;
        if (_arena == null) SetArena(GetComponentInParent<TrainingArena>());
        _isInitialized = true;
    }

    public override void OnEpisodeBegin()
    {
        CacheSpawnPosition();

        // Restore the sparring dummy (dead after a kill, damaged after a time-out) so every episode starts equal.
        if (_trainingDummy != null) _trainingDummy.ResetHealth();

        UnsubscribeFromTarget();
        ReportEpisodeStats();
        _strafeSide = Random.value < 0.5f ? 1f : -1f;
        _hasPrevPotential = false;
        _currentTarget = null;
        _targetDamageable = null;
        _targetVelocity = Vector3.zero;
        _hasLineOfSight = false;
        _hasAimTarget = false;

        if (_abilityManager != null)
            _abilityManager.InterruptAllAbilities();
            if (_abilityManager.primaryAttack != null) _abilityManager.primaryAttack.isHolding = false;
            _primaryHeld = false;

        // 1. Revive health
        if (TryGetComponent<EnemyHealth>(out var eh))
        {
            eh.ResetHealth();
        }

        if (_selfDamageable != null) _selfHpKnown = _selfDamageable.CurrentHealth;

        // 2. Resolve Spawn Target
        Vector3 targetSpawnPos = trainingSpawnPoint != null ? trainingSpawnPoint.position : _initialSpawnPosition;
        Quaternion targetSpawnRot = trainingSpawnPoint != null ? trainingSpawnPoint.rotation : _initialSpawnRotation;
        if (_arena != null) _arena.ResetEpisode(out targetSpawnPos, out targetSpawnRot);

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
        UpdatePrimaryHold();
        FindTarget();
        UpdateTargetVelocity();
        UpdateLineOfSight();
        if (Time.time >= _nextNavTime)
        {
            _nextNavTime = Time.time + navRepathInterval;
            ExecuteMacroNavigation();
        }
        ApplyAimRotation();
    }

    private void ExecuteMacroNavigation()
    {
        if (_movement.IsUnderExternalForce || _movement.IsStunned)
            return;

        if (_currentTarget != null)
        {
            float combatRange = GetCombatRange();
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
                float side = _strafeSide;
                Vector3 orbitDir = (awayDir + lateral * side * 0.6f).normalized;
                rawDestination = targetPos + orbitDir * orbitDistance;
                break;
            case PositioningAction.Hold:
            default:
                rawDestination = targetPos + awayDir * (combatRange * 0.7f);
                break;
        }

        if (_arena != null) rawDestination = _arena.ClampToBounds(rawDestination);

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
            if (_arena != null && !_arena.Contains(col.transform)) continue;   // only fight inside our own arena
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
            _targetHpKnown = _targetDamageable.CurrentHealth;
            _trainingDummy = bestTarget.TryGetComponent<EnemyHealth>(out var dummyHealth) && dummyHealth.isTrainingDummy ? dummyHealth : null;
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
        // 1. Self state (9 values)
        if (_selfDamageable != null) _selfHpKnown = _selfDamageable.CurrentHealth;
        sensor.AddObservation(_selfDamageable != null ? (float)_selfDamageable.CurrentHealth / _selfDamageable.MaxHealth : 1f);
        sensor.AddObservation(_movement.IsStunned ? 1f : 0f);
        sensor.AddObservation(_movement.IsDashing ? 1f : 0f);
        sensor.AddObservation(GetCooldownNormalized(_abilityManager.primaryAttack));
        sensor.AddObservation(GetCooldownNormalized(_abilityManager.primaryAbility));
        sensor.AddObservation(GetCooldownNormalized(_abilityManager.secondaryAbility));
        sensor.AddObservation(GetCooldownNormalized(_abilityManager.mobilityAbility));
        sensor.AddObservation(GetCooldownNormalized(_abilityManager.ultimateAbility));
        sensor.AddObservation(_agent != null ? Mathf.Clamp01(_agent.velocity.magnitude / Mathf.Max(_agent.speed, 0.01f)) : 0f);

        // 2. Target context (10 values), directions in the agent's local frame
        if (_currentTarget != null)
        {
            Vector3 toTarget = _currentTarget.position - transform.position;
            float combatRange = Mathf.Max(GetCombatRange(), 0.01f);
            if (_targetDamageable != null) _targetHpKnown = _targetDamageable.CurrentHealth;

            sensor.AddObservation(transform.InverseTransformDirection(toTarget.normalized));
            sensor.AddObservation(Mathf.Clamp01(toTarget.magnitude / detectionRange));
            sensor.AddObservation(Mathf.Clamp01(toTarget.magnitude / (combatRange * 2f)));
            sensor.AddObservation(transform.InverseTransformDirection(Vector3.ClampMagnitude(_targetVelocity / 10f, 1f)));
            sensor.AddObservation(_targetDamageable != null ? (float)_targetDamageable.CurrentHealth / _targetDamageable.MaxHealth : 1f);
            sensor.AddObservation(_hasLineOfSight ? 1f : 0f);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(1f);
            sensor.AddObservation(1f);
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        // 3. Nearest NavMesh edge (3 values): lets the policy learn not to get knocked or dashed off the map
        AddEdgeObservations(sensor);
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
        bool unavailable = ability == null || exclusiveLocked || (actionIndex != 1 && ability.IsOnCooldown());   // slot 1 = "hold attack": stays selectable while cooling down
        if (unavailable)
            actionMask.SetActionEnabled(1, actionIndex, false);
    }

    private float GetCombatRange()
    {
        return _abilityManager != null && _abilityManager.primaryAttack != null
            ? _abilityManager.primaryAttack.range
            : 15f;
    }

    private void AddEdgeObservations(VectorSensor sensor)
    {
        if (NavMesh.FindClosestEdge(transform.position, out NavMeshHit edge, NavMesh.AllAreas))
        {
            Vector3 local = transform.InverseTransformDirection((edge.position - transform.position).normalized);
            sensor.AddObservation(Mathf.Clamp01(edge.distance / edgeObservationRange));
            sensor.AddObservation(local.x);
            sensor.AddObservation(local.z);
        }
        else
        {
            sensor.AddObservation(1f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
    }

    // Potential-based shaping (gamma matches the trainer's 0.99): gives a dense "get to a useful range"
    // signal without changing which policy is optimal.
    private void ApplyRangeShaping()
    {
        if (_currentTarget == null)
        {
            _hasPrevPotential = false;
            return;
        }

        float ideal = GetCombatRange() * 0.7f;
        float dist = Vector3.Distance(transform.position, _currentTarget.position);
        float potential = -Mathf.Abs(dist - ideal) / detectionRange;

        if (_hasPrevPotential)
            AddReward(rangeShaping * (0.99f * potential - _prevPotential));

        _prevPotential = potential;
        _hasPrevPotential = true;
    }

    public void SetArena(TrainingArena arena)
    {
        _arena = arena;
        if (_arena != null) defaultObjective = _arena.Objective;
    }

    private string GetHeroName()
    {
        if (TryGetComponent<HeroManager>(out var heroManager) && heroManager.currentHero != null)
            return heroManager.currentHero.name.Replace("(Clone)", "").Trim();
        return "NoHero";
    }

    private void TrackDecision()
    {
        _epDecisions++;
        if (_currentTarget == null) return;

        _epWithTarget++;
        if (_hasLineOfSight) _epWithLos++;
        if (Vector3.Distance(transform.position, _currentTarget.position) <= Mathf.Max(GetCombatRange(), 0.01f))
            _epInRange++;
    }

    // Shows up in TensorBoard under Combat/*. Tells you WHY reward is what it is.
    private void ReportEpisodeStats()
    {
        if (_epDecisions > 0)
        {
            var stats = Academy.Instance.StatsRecorder;
            float d = _epDecisions;
            stats.Add("Combat/DamageDealt", _epDamageDealt);   // fraction of target max health
            stats.Add("Combat/DamageTaken", _epDamageTaken);   // fraction of own max health
            stats.Add("Combat/Killed", _epKilled);
            stats.Add("Combat/Died", _epDied);
            stats.Add("Combat/TimedOut", (_epKilled + _epDied) > 0f ? 0f : 1f);
            stats.Add("Combat/HasTarget", _epWithTarget / d);
            stats.Add("Combat/LineOfSight", _epWithLos / d);
            stats.Add("Combat/InAttackRange", _epInRange / d);
            stats.Add("Combat/AttackUses", _epAttackUses);
            stats.Add("Combat/AbilityUses", _epAbilityUses);
            stats.Add("Combat/EpisodeDecisions", d);
            string hero = GetHeroName();
            stats.Add($"Hero/{hero}/DamageDealt", _epDamageDealt);
            stats.Add($"Hero/{hero}/Killed", _epKilled);
            stats.Add($"Hero/{hero}/AttackUses", _epAttackUses);
        }

        _epDamageDealt = _epDamageTaken = _epKilled = _epDied = 0f;
        _epDecisions = _epWithTarget = _epWithLos = _epInRange = _epAttackUses = _epAbilityUses = 0;
    }

    public void EndEpisodeOnce()
    {
        // Self-death and target-death can both fire in the same frame; only end the episode once.
        if (Time.frameCount == _lastEndFrame) return;
        _lastEndFrame = Time.frameCount;
        EndEpisode();
    }

    private float GetCooldownNormalized(Ability ability)
    {
        if (ability == null || ability.cooldown <= 0f) return 0f;
        float remaining = ability.lastUsedTime + ability.cooldown - Time.time;
        return Mathf.Clamp01(remaining / ability.cooldown);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        AddReward(existentialPenaltyPerStep);
        ApplyRangeShaping();
        TrackDecision();

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
                ReleasePrimary();
                return;
            case 1:
                HoldPrimary();
                return;
            case 2: targetAbility = _abilityManager.primaryAbility; break;
            case 3: targetAbility = _abilityManager.secondaryAbility; break;
            case 4: targetAbility = _abilityManager.mobilityAbility; break;
            case 5: targetAbility = _abilityManager.ultimateAbility; break;
        }

        if (targetAbility == null) return;
        if (targetAbility.IsOnCooldown() || _abilityManager.HasActiveExclusiveAbility()) return;

        _epAbilityUses++;
        targetAbility.Use();
    }

    // The primary attack is driven exactly like a player's mouse button: press starts it (Attack.Update then fires it
    // every cooldown), release stops it. Calling Use() directly left isExecuting stuck on, so plain attacks only fired
    // once per episode and the bows (which fire on release) never fired at all.
    private void HoldPrimary()
    {
        Attack atk = _abilityManager.primaryAttack;
        if (atk == null || _abilityManager.HasActiveExclusiveAbility()) return;
        if (_primaryHeld) return;                                    // keep holding; re-pressing would reset a bow's charge timer
        if (GetChargeTime(atk) > 0f && atk.IsOnCooldown()) return;   // charge weapons ignore presses while cooling down

        atk.OnPress();
        _primaryHeld = true;
        _primaryHeldSince = Time.time;
        _epAttackUses++;
    }

    private void ReleasePrimary()
    {
        if (!_primaryHeld) return;
        _primaryHeld = false;
        if (_abilityManager.primaryAttack != null) _abilityManager.primaryAttack.OnRelease();
    }

    private void UpdatePrimaryHold()
    {
        if (!_primaryHeld) return;

        Attack atk = _abilityManager.primaryAttack;
        if (atk == null)
        {
            _primaryHeld = false;
            return;
        }

        float chargeTime = GetChargeTime(atk);
        if (chargeTime > 0f && Time.time - _primaryHeldSince >= chargeTime)
            ReleasePrimary();   // fully charged: loose the arrow
    }

    private static float GetChargeTime(Attack atk)
    {
        if (atk is GoldenBow golden) return golden.maxChargeTime;
        if (atk is CrescentBow crescent) return crescent.maxChargeTime;
        return 0f;
    }

    private void HandleSelfDamageTaken(int damage, GameObject attacker)
    {
        int hpNow = _selfDamageable != null ? _selfDamageable.CurrentHealth : 0;
        float lost = hpNow > 0 ? damage : Mathf.Min(damage, _selfHpKnown);   // ignore overkill
        _selfHpKnown = hpNow;

        // Environment damage (KillZone etc.) has no attacker: the death penalty already covers it.
        if (attacker == null || attacker == gameObject || _selfDamageable == null) return;

        _epDamageTaken += lost / Mathf.Max(1, _selfDamageable.MaxHealth);
        AddReward(-damageTakenPenalty * lost / Mathf.Max(1, _selfDamageable.MaxHealth));
    }

    private void HandleSelfDeath(GameObject killer)
    {
        _epDied = 1f;
        AddReward(deathPenalty);
        EndEpisodeOnce();
    }

    private void HandleTargetDeath(GameObject killer)
    {
        if (killer == gameObject)
            { AddReward(killReward); _epKilled = 1f; }

        // If the target is also an ML-Agent (self-play or sparring AI), end its episode too so it respawns.
        if (_currentTarget != null && _currentTarget.TryGetComponent<Agent>(out var targetAgent))
        {
            if (targetAgent is RLEnemyCombatAgent other) other.EndEpisodeOnce();
            else targetAgent.EndEpisode();
        }

        UnsubscribeFromTarget();
        _currentTarget = null;
        _targetDamageable = null;

        EndEpisodeOnce();
    }

    private void HandleTargetDamageTaken(int damage, GameObject attacker)
    {
        if (_targetDamageable == null) return;

        int hpNow = _targetDamageable.CurrentHealth;
        float lost = hpNow > 0 ? damage : Mathf.Min(damage, _targetHpKnown);  // ignore overkill
        _targetHpKnown = hpNow;

        if (attacker != gameObject) return;
        _epDamageDealt += lost / Mathf.Max(1, _targetDamageable.MaxHealth);
        AddReward(damageDealtReward * lost / Mathf.Max(1, _targetDamageable.MaxHealth));
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