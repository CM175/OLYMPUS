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
    // Branch 2 = free movement: 0 = stand still, 1..8 = move in one of 8 directions, 45 degrees apart, clockwise,
    // starting from "straight at the opponent" (1 = toward, 3 = right, 5 = directly away, 7 = left).
    private const int MoveActions = 9;

    [Header("Macro / Navigation")]
    [SerializeField] private float detectionRange = 25f;
    [SerializeField] private LayerMask searchMask;
    [SerializeField] private LayerMask lineOfSightBlockMask; 
    [SerializeField] private Transform defaultObjective;
    [SerializeField] private Transform trainingSpawnPoint;
    [Tooltip("How far ahead (metres) a movement action aims. Keep above speed * navRepathInterval so the agent never stalls between decisions.")]
    [SerializeField] private float moveLookahead = 5f;
    
    private Vector3 _initialSpawnPosition;
    
    private Quaternion _initialSpawnRotation;

    [Header("Aim Settings")]
    [SerializeField] private float maxAimLeadAngle = 15f;
    [SerializeField] private float aimTurnSpeed = 15f;

    [Header("Reward Shaping (damage terms are fractions of max health, so a full kill is about +1)")]
    [SerializeField] private float damageDealtReward = 1.0f;
    [SerializeField] private float damageTakenPenalty = 1.0f;   // equal to damageDealtReward so even trades aren't net-positive in self-play
    [SerializeField] private float killReward = 1.0f;
    [SerializeField] private float deathPenalty = -1.0f;
    [SerializeField] private float rangeShaping = 0.1f;
    [Header("Ability Rewards (outcome-based: pays for what an ability achieves, not for pressing it)")]
    [SerializeField] private bool useAbilityRewards = true;
    [Tooltip("Index 0..3 = primary ability, secondary ability, mobility, ultimate. Seconds after a press in which damage is credited to that ability.")]
    [SerializeField] private float[] abilityWindow = { 2.5f, 2.5f, 1.5f, 6f };
    [Tooltip("Extra reward per full target health dealt inside the window (on top of damageDealtReward).")]
    [SerializeField] private float[] abilityDamageBonus = { 0.25f, 0.25f, 0f, 0.5f };
    [Tooltip("Penalty when the window closes with zero damage. Leave 0 for utility slots (shields, heals, mobility) that are not meant to deal damage.")]
    [SerializeField] private float[] abilityWastePenalty = { 0.05f, 0f, 0f, 0.3f };
    [Tooltip("Reward per full max-health actually healed. Keep below damageTakenPenalty.")]
    [SerializeField] private float healReward = 0.5f;
    [Tooltip("Healthy: mobility is rewarded for ending closer to ideal range (scaled gain in the range potential).")]
    [SerializeField] private float mobilityRangeBonus = 0f;
    [Tooltip("At or below this health fraction when mobility is pressed, it is judged as an escape instead of an engage.")]
    [SerializeField, Range(0f, 1f)] private float escapeHpThreshold = 0.4f;
    [Tooltip("Max reward for a successful escape. Keep below the damage already lost to get that low, so it can never pay to get hurt on purpose.")]
    [SerializeField] private float escapeReward = 0.4f;
    [Tooltip("Metres of extra distance that count as a full escape.")]
    [SerializeField] private float escapeDistance = 10f;
    [Header("Objective Reward Shaping")]
    [Tooltip("Off by default: navigation is scripted, so the policy cannot influence these rewards yet.")]
    [SerializeField] private bool useObjectiveRewards = false;
    [SerializeField] private float objectivePresenceReward = 0.02f; // Rewarded per step spent on the point
    [SerializeField] private float objectiveCaptureProgressReward = 0.2f; // Rewarded when progress increases for our team
    private PointOwner _lastObservedControl = PointOwner.Neutral;
[Header("Objective Milestone Rewards")]
[SerializeField] private float objectiveCapturedReward = 2.0f;     // Rewarded when our team captures/locks the point
[SerializeField] private float objectiveNeutralizedReward = 1.0f; // Rewarded when our team successfully neutralizes an enemy point

    [Header("Episode / Performance")]
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
    private int _moveAction = 0;
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
    private bool _isTrainingDead;

    public bool IsTrainingDead => _isTrainingDead;

    // per-episode diagnostics (reported to TensorBoard)
    private float _epDamageDealt, _epDamageTaken, _epKilled, _epDied;
    private int _epDecisions, _epWithTarget, _epWithLos, _epInRange, _epAttackUses, _epAbilityUses;
    private EnemyHealth _trainingDummy;

    // ability reward window state (slot index 0..3, -1 = no open window)
    private const int AbilitySlots = 4;
    private static readonly string[] AbilitySlotNames = { "Primary", "Secondary", "Mobility", "Ultimate" };
    private int _abilitySlot = -1;
    private int _lastHealCheckHp = -1;
    private float _abilityStartHpFrac, _abilityStartDist, _abilityDamageTaken;
    private bool _abilityStartLos;
    private float _epHealed;
    private float _abilityWindowEnd, _abilityDamage, _abilityStartPotential;
    private readonly float[] _epSlotUses = new float[AbilitySlots];
    private readonly float[] _epSlotDamage = new float[AbilitySlots];

    // Hero one-hot slots (roster size must be <= HeroSlots; changing this changes the observation size)
    private const int HeroSlots = 8;
    private HeroManager _selfHeroManager;
    private Transform _cachedTarget;
    private AbilityManager _targetAbilities;
    private IMovementController _targetMovement;
    private HeroManager _targetHeroManager;

    private readonly Collider[] _overlapBuffer = new Collider[256];   // other arenas inside detectionRange also land here; too small a buffer can drop the real opponent
    private bool _warnedHeroOverflow;

    // False between a match ending and the next one starting; rewards booked in that window leak into the next episode.
    private bool MatchLive => _arena == null || _arena.MatchManager == null || _arena.MatchManager.IsMatchActive;

    public LayerMask SearchMask => searchMask;
    public float CombatRange => GetCombatRange();
    protected override void Awake()
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
        _selfHeroManager = GetComponent<HeroManager>();

        if (defaultObjective == null)
            defaultObjective = GameObject.FindWithTag("Objective")?.transform;

        if (_agent != null)
            _agent.updateRotation = false;

        if (_selfDamageable != null)
        {
            _selfDamageable.OnDamageReceived += HandleSelfDamageTaken;
            _selfDamageable.OnDied += HandleSelfDeath;
        }

        CacheSpawnPosition();   // Awake already cached it; don't overwrite a pose set via SetSpawnPoint
        // The TrainingMatchManager ends both agents together at match end.
        // Disable per-agent MaxStep timeouts so one agent cannot reset mid-match.
        MaxStep = 0;
        if (_arena == null) SetArena(GetComponentInParent<TrainingArena>());

        // Only flag training-dummy health inside a training arena, so the same prefab behaves normally in the real game
        if (_arena != null && TryGetComponent<EnemyHealth>(out var eh))
            eh.isTrainingDummy = true;

        _isInitialized = true;
    }

    public override void OnEpisodeBegin()
    {
        CacheSpawnPosition();
        UnsubscribeFromTarget();
        ReportEpisodeStats();

        _isTrainingDead = false;
        _abilitySlot = -1;
        _lastHealCheckHp = -1;
        _strafeSide = Random.value < 0.5f ? 1f : -1f;
        _hasPrevPotential = false;
        _currentTarget = null;
        _targetDamageable = null;
        _targetVelocity = Vector3.zero;
        _hasLineOfSight = false;
        _hasAimTarget = false;
        _moveAction = 0;
        _nextNavTime = 0f;
        _lastObservedControl = PointOwner.Neutral;

        ReleasePrimary();
        if (_abilityManager != null)
            _abilityManager.InterruptAllAbilities();

        if (TryGetComponent<EnemyHealth>(out var health))
            health.ResetHealth();

        if (_selfDamageable != null)
            _selfHpKnown = _selfDamageable.CurrentHealth;

        Vector3 spawnPosition = trainingSpawnPoint != null ? trainingSpawnPoint.position : _initialSpawnPosition;
        Quaternion spawnRotation = trainingSpawnPoint != null ? trainingSpawnPoint.rotation : _initialSpawnRotation;

        if (_arena != null)
            _arena.GetSpawnPose(this, out spawnPosition, out spawnRotation);

        if (_movement != null)
            _movement.ResetForTraining(spawnPosition, spawnRotation);
        else
        {
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            if (_agent != null)
            {
                if (!_agent.enabled) _agent.enabled = true;
                if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                    _agent.Warp(hit.position);
                _agent.ResetPath();
                _agent.isStopped = false;
            }
        }

        if (defaultObjective != null && defaultObjective.TryGetComponent<CapturePoint>(out var point))
            _lastObservedControl = point.controllingTeam;
    }

    private void Update()
    {
        if (_isTrainingDead) return;

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
        EvaluateObjectiveRewards();
        if (_abilitySlot >= 0 && Time.time >= _abilityWindowEnd) CloseAbilityWindow();
        UpdateHealReward();
    }

    private void ExecuteMacroNavigation()
    {
        if (_movement.IsUnderExternalForce || _movement.IsStunned)
            return;

        if (_currentTarget != null)
        {
            _agent.stoppingDistance = 0f;
            if (_agent.isOnNavMesh)
                _agent.SetDestination(ComputeMoveDestination());
        }
        else if (defaultObjective != null)
        {
            if (!_agent.isOnNavMesh) return;
            _agent.stoppingDistance = 2f;
            _agent.SetDestination(defaultObjective.position);
        }
        else
        {
            // If no objective and no target, pick a random point on the NavMesh to patrol
            if (!_agent.hasPath || _agent.remainingDistance < 1f)
            {
                Vector3 randomDirection = Random.insideUnitSphere * 15f;
                randomDirection += transform.position;
                if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, 15f, NavMesh.AllAreas))
                {
                    _agent.SetDestination(hit.position);
                }
            }
        }
    }

    // The policy fully controls where to go: it picks a direction (relative to the opponent) every decision and the
    // NavMeshAgent walks it. Nothing here knows about ideal range, cover or flanking; that all has to be learned.
    private Vector3 ComputeMoveDestination()
    {
        Vector3 pos = transform.position;
        if (_moveAction <= 0) return pos;   // 0 = stand still

        Vector3 forward = _currentTarget.position - pos;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : transform.forward;

        Vector3 dir = Quaternion.Euler(0f, (_moveAction - 1) * 45f, 0f) * forward;
        Vector3 dest = pos + dir * moveLookahead;
        if (_arena != null) dest = _arena.ClampToBounds(dest);

        if (!NavMesh.SamplePosition(dest, out NavMeshHit sample, 2f, NavMesh.AllAreas))
            return pos;
        dest = sample.position;

        // Stop at the first edge or obstacle on the way instead of pathing around it, so a move never becomes a detour.
        if (NavMesh.Raycast(pos, dest, out NavMeshHit blocked, NavMesh.AllAreas))
            dest = blocked.position;
        return dest;
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

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, detectionRange, _overlapBuffer, searchMask);
        float closestDist = float.MaxValue;
        Transform bestTarget = null;
        IDamageable bestDamageable = null;

        if (hitCount == _overlapBuffer.Length)
            Debug.LogWarning($"{name}: overlap buffer full ({hitCount}); the opponent may be missed. Space arenas further apart or raise the buffer size.", this);
        for (int c = 0; c < hitCount; c++)
        {
            var col = _overlapBuffer[c];
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
                
                if (_arena == null && Physics.Raycast(origin, dir.normalized, dir.magnitude, lineOfSightBlockMask))
                    continue; // Hidden behind obstacle (outside training only; in an arena the policy sees LOS as an observation)

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
        // 1. Self state (9 + 2 velocity + HeroSlots hero one-hot)
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
        Vector3 localVel = (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            ? transform.InverseTransformDirection(Vector3.ClampMagnitude(_agent.velocity / Mathf.Max(_agent.speed, 0.01f), 1f))
            : Vector3.zero;
        sensor.AddObservation(localVel.x);
        sensor.AddObservation(localVel.z);
        AddHeroOneHot(sensor, _selfHeroManager);

        // 2. Target context (10 + 9 opponent state + HeroSlots hero one-hot), directions in the agent's local frame
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

            RefreshTargetCache();
            sensor.AddObservation(_targetMovement != null && _targetMovement.IsStunned ? 1f : 0f);
            sensor.AddObservation(_targetMovement != null && _targetMovement.IsDashing ? 1f : 0f);
            sensor.AddObservation(GetCooldownNormalized(_targetAbilities != null ? _targetAbilities.primaryAttack : null));
            sensor.AddObservation(GetCooldownNormalized(_targetAbilities != null ? _targetAbilities.primaryAbility : null));
            sensor.AddObservation(GetCooldownNormalized(_targetAbilities != null ? _targetAbilities.secondaryAbility : null));
            sensor.AddObservation(GetCooldownNormalized(_targetAbilities != null ? _targetAbilities.mobilityAbility : null));
            sensor.AddObservation(GetCooldownNormalized(_targetAbilities != null ? _targetAbilities.ultimateAbility : null));
            Vector3 targetFacing = transform.InverseTransformDirection(_currentTarget.forward);
            sensor.AddObservation(targetFacing.x);
            sensor.AddObservation(targetFacing.z);
            AddHeroOneHot(sensor, _targetHeroManager);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(1f);
            sensor.AddObservation(1f);
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            for (int i = 0; i < 9 + HeroSlots; i++) sensor.AddObservation(0f);   // opponent state + hero one-hot
        }

        // 3. Add Objective Observations (6 values): direction and distance to the default objective, plus capture progress and ownership if applicable
        if (defaultObjective != null)
        {
            Vector3 toObjective = defaultObjective.position - transform.position;
            sensor.AddObservation(transform.InverseTransformDirection(toObjective.normalized));
            sensor.AddObservation(Mathf.Clamp01(toObjective.magnitude / 100f)); // Normalized distance to objective
            
            // If the objective is a CapturePoint, observe its progress and ownership
            if (defaultObjective.TryGetComponent<CapturePoint>(out var capPoint))
            {
                sensor.AddObservation(capPoint.captureProgress / 100f);
                // 1. Determine what PointOwner matches this agent's team
                PointOwner agentTeamOwner = (_teamMember != null && _teamMember.team == Team.TeamB) 
                    ? PointOwner.TeamB 
                    : PointOwner.TeamA;

                // 2. Evaluate relative to us (+1 for us, -1 for enemy, 0 for neutral)
                float controlValue = 0f;
                if (capPoint.controllingTeam != PointOwner.Neutral)
                {
                    controlValue = (capPoint.controllingTeam == agentTeamOwner) ? 1f : -1f;
                }

                sensor.AddObservation(controlValue);
            }
            else
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
            }
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(1f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);   // keep size at 6 to match the objective branch
        }

        // 4. Nearest NavMesh edge (3 values): lets the policy learn not to get knocked or dashed off the map
        AddEdgeObservations(sensor);


    }

    private void RefreshTargetCache()
    {
        if (_currentTarget == _cachedTarget) return;
        _cachedTarget = _currentTarget;
        _targetAbilities = _currentTarget != null ? _currentTarget.GetComponent<AbilityManager>() : null;
        _targetMovement = _currentTarget != null ? _currentTarget.GetComponent<IMovementController>() : null;
        _targetHeroManager = _currentTarget != null ? _currentTarget.GetComponent<HeroManager>() : null;
    }

    private void AddHeroOneHot(VectorSensor sensor, HeroManager hm)
    {
        int idx = GetHeroIndex(hm);
        for (int i = 0; i < HeroSlots; i++) sensor.AddObservation(i == idx ? 1f : 0f);
    }

    private int GetHeroIndex(HeroManager hm)
    {
        if (hm == null || hm.currentHero == null || _arena == null || _arena.characterDatabase == null) return -1;
        var heroes = _arena.characterDatabase.GetAllHeroes();
        if (heroes.Count > HeroSlots && !_warnedHeroOverflow)
        {
            _warnedHeroOverflow = true;
            Debug.LogWarning($"Roster has {heroes.Count} heroes but HeroSlots is {HeroSlots}; extra heroes are invisible to the policy.", this);
        }
        for (int i = 0; i < heroes.Count && i < HeroSlots; i++)
            if (heroes[i] != null && heroes[i].heroName == hm.currentHero.heroName) return i;
        return -1;
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
        bool unavailable = ability == null || exclusiveLocked || (actionIndex != 1 && (ability.IsOnCooldown() || ability.isExecuting));   // slot 1 = "hold attack": stays selectable while cooling down
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
    private void EvaluateObjectiveRewards()
    {
        if (!useObjectiveRewards || defaultObjective == null) return;

        if (defaultObjective.TryGetComponent<CapturePoint>(out var capPoint))
        {
            // Between matches the point is reset after OnEpisodeBegin has already run: resync instead of rewarding the reset.
            if (_arena != null && _arena.MatchManager != null && !_arena.MatchManager.IsMatchActive)
            {
                _lastObservedControl = capPoint.controllingTeam;
                return;
            }

            // Determine what PointOwner matches this agent's team
            TeamMember teamMember = GetComponent<TeamMember>();
            PointOwner agentTeamOwner = (teamMember != null && teamMember.team == Team.TeamB) 
                ? PointOwner.TeamB 
                : PointOwner.TeamA;

            // 1. Continuous presence shaping (standing near the point)
            float distToObjective = Vector3.Distance(transform.position, defaultObjective.position);
            float captureRadius = 2f; // Default radius if no BoxCollider is found
            if (defaultObjective.TryGetComponent<BoxCollider>(out var box))
            {
                // Take the largest horizontal dimension of the box scale/size
                Vector3 scaledSize = Vector3.Scale(box.size, defaultObjective.localScale);
                captureRadius = Mathf.Max(scaledSize.x, scaledSize.z) * 0.5f;
            }
    
            if (distToObjective <= captureRadius) 
            {
                AddReward(objectivePresenceReward * Time.deltaTime);

                // If our team is actively pushing or capturing the point, grant a progress reward
                if (capPoint.captureProgress > 0f && capPoint.currentChallenger == teamMember.team)
                {
                    AddReward(objectiveCaptureProgressReward * Time.deltaTime);
                }
            }

            // 2. Milestone detection: Did the point ownership change this episode/round?
            if (capPoint.controllingTeam != _lastObservedControl)
            {
                // If it just became ours
                if (capPoint.controllingTeam == agentTeamOwner)
                {
                    AddReward(objectiveCapturedReward);
                }
                // If it became neutral from an enemy hold (successful neutralization)
                else if (capPoint.controllingTeam == PointOwner.Neutral && _lastObservedControl != PointOwner.Neutral && _lastObservedControl != agentTeamOwner)
                {
                    AddReward(objectiveNeutralizedReward);
                }
                
                _lastObservedControl = capPoint.controllingTeam;
            }
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
            stats.Add("Combat/HasTarget", _epWithTarget / d);
            stats.Add("Combat/LineOfSight", _epWithLos / d);
            stats.Add("Combat/InAttackRange", _epInRange / d);
            stats.Add("Combat/AttackUses", _epAttackUses);
            stats.Add("Combat/AbilityUses", _epAbilityUses);
            stats.Add("Combat/Healed", _epHealed);
            for (int i = 0; i < AbilitySlots; i++)
            {
                stats.Add($"Ability/{AbilitySlotNames[i]}/Uses", _epSlotUses[i]);
                stats.Add($"Ability/{AbilitySlotNames[i]}/Damage", _epSlotDamage[i]);
            }
            stats.Add("Combat/EpisodeDecisions", d);
            string hero = GetHeroName();
            stats.Add($"Hero/{hero}/DamageDealt", _epDamageDealt);
            stats.Add($"Hero/{hero}/Killed", _epKilled);
            stats.Add($"Hero/{hero}/AttackUses", _epAttackUses);
        }

        _epDamageDealt = _epDamageTaken = _epKilled = _epDied = 0f;
        _epHealed = 0f;
        System.Array.Clear(_epSlotUses, 0, AbilitySlots);
        System.Array.Clear(_epSlotDamage, 0, AbilitySlots);
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
        if (_isTrainingDead) return;

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
        float aimAngleOffset = (aimAction - 2) * (maxAimLeadAngle * 0.5f);   // 5 options: -max, -max/2, 0, +max/2, +max
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
        _moveAction = Mathf.Clamp(discreteActions[2], 0, MoveActions - 1);
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
        if (targetAbility.IsOnCooldown() || targetAbility.isExecuting || _abilityManager.HasActiveExclusiveAbility()) return;

        targetAbility.Use();

        // Use() silently refuses (stunned, etc.), so only count it if it really started
        if (targetAbility.isExecuting || targetAbility.IsOnCooldown())
        {
            _epAbilityUses++;
            OpenAbilityWindow(actionIndex - 2);
        }
    }

    // Which of our own slots (0..3) an ability belongs to. -1 = unknown source, -2 = primary attack / anything else.
    private int SlotOf(Ability a)
    {
        if (a == null || _abilityManager == null) return -1;
        if (a == _abilityManager.primaryAbility) return 0;
        if (a == _abilityManager.secondaryAbility) return 1;
        if (a == _abilityManager.mobilityAbility) return 2;
        if (a == _abilityManager.ultimateAbility) return 3;
        return -2;
    }

    // Reward HP actually restored (overheal pays nothing). healReward < damageTakenPenalty, so taking damage
    // just to heal it back can never be profitable.
    private void UpdateHealReward()
    {
        if (_selfDamageable == null) return;
        int hp = _selfDamageable.CurrentHealth;
        if (_lastHealCheckHp >= 0 && hp > _lastHealCheckHp && MatchLive)
        {
            float gained = (hp - _lastHealCheckHp) / (float)Mathf.Max(1, _selfDamageable.MaxHealth);
            _epHealed += gained;
            AddReward(healReward * gained);
        }
        _lastHealCheckHp = hp;
    }

    private static float SlotValue(float[] values, int i, float fallback = 0f)
        => values != null && i >= 0 && i < values.Length ? values[i] : fallback;

    private float CurrentRangePotential()
    {
        if (_currentTarget == null) return 0f;
        float ideal = GetCombatRange() * 0.7f;
        return -Mathf.Abs(Vector3.Distance(transform.position, _currentTarget.position) - ideal) / detectionRange;
    }

    private void OpenAbilityWindow(int slot)
    {
        if (slot < 0 || slot >= AbilitySlots) return;
        _epSlotUses[slot] += 1f;
        CloseAbilityWindow();   // a new press settles the previous one
        if (!useAbilityRewards) return;
        _abilitySlot = slot;
        _abilityWindowEnd = Time.time + Mathf.Max(0.1f, SlotValue(abilityWindow, slot, 2f));
        _abilityDamage = 0f;
        _abilityStartPotential = CurrentRangePotential();
        _abilityStartHpFrac = _selfDamageable != null
            ? _selfDamageable.CurrentHealth / (float)Mathf.Max(1, _selfDamageable.MaxHealth)
            : 1f;
        _abilityStartDist = _currentTarget != null ? Vector3.Distance(transform.position, _currentTarget.position) : 0f;
        _abilityStartLos = _hasLineOfSight;
        _abilityDamageTaken = 0f;
    }

    private void CloseAbilityWindow()
    {
        if (_abilitySlot < 0) return;
        int slot = _abilitySlot;
        _abilitySlot = -1;
        if (!MatchLive) return;

        _epSlotDamage[slot] += _abilityDamage;
        if (_abilityDamage > 0f)
            AddReward(SlotValue(abilityDamageBonus, slot) * _abilityDamage);
        else
            AddReward(-SlotValue(abilityWastePenalty, slot));

        if (slot == 2)   // mobility: judged by intent (escape when hurt, engage when healthy)
        {
            bool hadTarget = _abilityStartDist > 0f;
            if (hadTarget && _abilityStartHpFrac <= escapeHpThreshold)
            {
                // Low health: reward breaking away. Closing in is deliberately NOT rewarded here.
                float distFrac = _currentTarget == null
                    ? 1f   // target lost entirely (left detection range)
                    : Mathf.Clamp01((Vector3.Distance(transform.position, _currentTarget.position) - _abilityStartDist) / Mathf.Max(1f, escapeDistance));
                float losBroken = _abilityStartLos && !_hasLineOfSight ? 1f : 0f;
                float survived = 1f - Mathf.Clamp01(_abilityDamageTaken / 0.25f);   // got hit hard on the way out = not an escape
                AddReward(escapeReward * Mathf.Max(distFrac, losBroken) * survived);
            }
            else if (_currentTarget != null)
            {
                float gain = CurrentRangePotential() - _abilityStartPotential;
                if (gain > 0f) AddReward(mobilityRangeBonus * gain);
            }
        }
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
        if (!MatchLive) return;
        if (attacker == null || attacker.transform.root == transform.root || _selfDamageable == null) return;

        _epDamageTaken += lost / Mathf.Max(1, _selfDamageable.MaxHealth);
        AddReward(-damageTakenPenalty * lost / Mathf.Max(1, _selfDamageable.MaxHealth));
        if (_abilitySlot >= 0) _abilityDamageTaken += lost / Mathf.Max(1, _selfDamageable.MaxHealth);
    }

    private void HandleSelfDeath(GameObject killer)
    {
        if (_isTrainingDead) return;

        bool live = MatchLive;
        _isTrainingDead = true;
        if (live)
        {
            _epDied += 1f;
            AddReward(deathPenalty);
        }
        _hasAimTarget = false;
        _abilitySlot = -1;   // death is already penalised; do not also settle the window
        _lastHealCheckHp = -1;
        UnsubscribeFromTarget();
        _currentTarget = null;
        _targetDamageable = null;
        ReleasePrimary();

        if (_abilityManager != null)
            _abilityManager.InterruptAllAbilities();

        if (_agent != null && _agent.enabled)
        {
            if (_agent.isOnNavMesh)
            {
                _agent.ResetPath();
                _agent.isStopped = true;
            }
        }

        if (!live) return;   // died in the gap between matches: no elimination, no respawn timer (manager revives at next start)

        if (_arena != null && _arena.MatchManager != null)
        {
            TeamMember killerMember = killer != null
                ? killer.transform.root.GetComponent<TeamMember>()
                : null;

            if (killerMember != null && _teamMember != null && killerMember.team != _teamMember.team)
                _arena.MatchManager.RegisterElimination(killerMember.team);

            _arena.MatchManager.ScheduleRespawn(this);
        }
        else
        {
            Debug.LogWarning($"{name} died, but no TrainingMatchManager is assigned; it cannot respawn.", this);
        }
    }

    private void HandleTargetDeath(GameObject killer)
    {
        bool wasKilledByThisAgent = killer != null && killer.transform.root == transform.root;

        if (wasKilledByThisAgent && MatchLive)
        {
            AddReward(killReward);
            _epKilled += 1f;
        }

        // The victim handles the elimination and respawn. A kill does not end either episode.
        UnsubscribeFromTarget();
        _currentTarget = null;
        _targetDamageable = null;
        _targetVelocity = Vector3.zero;
        _hasAimTarget = false;
        _hasPrevPotential = false;
    }

    public void RespawnForTraining()
    {
        if (!_isTrainingDead) return;
        if (_arena == null || (_arena.MatchManager != null && !_arena.MatchManager.IsMatchActive)) return;

        Vector3 spawnPosition = _initialSpawnPosition;
        Quaternion spawnRotation = _initialSpawnRotation;
        if (_arena != null)
            _arena.GetSpawnPose(this, out spawnPosition, out spawnRotation);

        ReleasePrimary();
        if (_abilityManager != null)
            _abilityManager.InterruptAllAbilities();

        if (TryGetComponent<EnemyHealth>(out var health))
            health.ResetHealth();

        _currentTarget = null;
        _targetDamageable = null;
        _targetVelocity = Vector3.zero;
        _hasLineOfSight = false;
        _hasAimTarget = false;
        _hasPrevPotential = false;
        _nextNavTime = 0f;
        _lastObservedControl = PointOwner.Neutral;

        if (_movement != null)
            _movement.ResetForTraining(spawnPosition, spawnRotation);
        else
        {
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            if (_agent != null)
            {
                if (!_agent.enabled) _agent.enabled = true;
                if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                    _agent.Warp(hit.position);
                _agent.ResetPath();
                _agent.isStopped = false;
            }
        }

        if (_selfDamageable != null)
            _selfHpKnown = _selfDamageable.CurrentHealth;

        if (defaultObjective != null && defaultObjective.TryGetComponent<CapturePoint>(out var point))
            _lastObservedControl = point.controllingTeam;

        _isTrainingDead = false;
    }

    // EndEpisode() runs OnEpisodeBegin immediately, i.e. BEFORE the arena rerolls heroes. Call this after a hero swap
    // so health, cooldown state and known-HP bookkeeping match the new hero.
    public void ResyncAfterHeroChange()
    {
        _lastHealCheckHp = -1;
        ReleasePrimary();
        if (_abilityManager != null)
            _abilityManager.InterruptAllAbilities();
        if (TryGetComponent<EnemyHealth>(out var health))
            health.ResetHealth();
        if (_selfDamageable != null)
            _selfHpKnown = _selfDamageable.CurrentHealth;
        _hasPrevPotential = false;
    }

    public void ApplyMatchOutcome(Team? winner, float winRewardValue, float lossRewardValue)
    {
        if (!winner.HasValue) return;
        AddReward(_teamMember != null && _teamMember.team == winner.Value
            ? winRewardValue
            : lossRewardValue);
    }

    private void HandleTargetDamageTaken(int damage, GameObject attacker)
    {
        if (_targetDamageable == null) return;

        int hpNow = _targetDamageable.CurrentHealth;
        float lost = hpNow > 0 ? damage : Mathf.Min(damage, _targetHpKnown);  // ignore overkill
        _targetHpKnown = hpNow;

        if (!MatchLive) return;
        if (attacker == null || attacker.transform.root != transform.root) return;
        float fraction = lost / Mathf.Max(1, _targetDamageable.MaxHealth);
        _epDamageDealt += fraction;
        AddReward(damageDealtReward * fraction);
        if (_abilitySlot >= 0 && Time.time <= _abilityWindowEnd)
        {
            int dealer = SlotOf(Ability.CurrentDealer);
            if (dealer == _abilitySlot || dealer == -1) _abilityDamage += fraction;   // -1: unknown source (DoT/projectile) falls back to the window
        }
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
            discreteActions[0] = 2;

        if (discreteActions.Length > 1)
            discreteActions[1] = Input.GetKey(KeyCode.Space) ? 1 : 0;

        if (discreteActions.Length > 2)
        {
            // Arrow keys: up = toward the opponent, down = away, left/right = strafe
            float x = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float z = (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            discreteActions[2] = (x == 0f && z == 0f)
                ? 0
                : (Mathf.RoundToInt(Mathf.Atan2(x, z) * Mathf.Rad2Deg / 45f) + 8) % 8 + 1;
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