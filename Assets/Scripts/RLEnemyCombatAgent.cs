using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using System.Collections.Generic;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AIMovementController))]
[RequireComponent(typeof(AbilityManager))]
[RequireComponent(typeof(TeamMember))]
public class RLEnemyCombatAgent : Agent
{
    // Branch 2 = free movement: 0 = stand still, 1..8 = move in one of 8 directions, 45 degrees apart, clockwise,
    // starting from "straight at the opponent" (1 = toward, 3 = right, 5 = directly away, 7 = left). With no opponent in
    // range the reference direction is the objective instead, so the policy can still move purposefully.
    private const int MoveActions = 9;

    // Branch 3 = intent (what the agent is trying to do this decision). Learned, not scripted:
    // 0 = engage the opponent (branch 2 movement applies), 1 = go to / hold the objective (branch 2 is ignored;
    // aiming and attacking at any opponent in range still work, so it can capture while shooting).
    private const int IntentEngage = 0;
    private const int IntentObjective = 1;
    private const int IntentActions = 2;
    private int _intent = IntentEngage;
    private int _epObjectiveIntent;

    // Branch 4 = target choice: which of the TargetSlots nearest visible enemies to focus (slot 0 = nearest).
    // Branch sizes must be { 5, 6, 9, IntentActions, TargetSlots }; vector observation size must be ExpectedObservationSize.
    private const int TargetSlots = 3;
    private const int CandidateFeatures = 9;
    private const int ExpectedObservationSize = 89;   // 55 original + 2 objective context + 4 team context + 1 last intent + TargetSlots * CandidateFeatures
    private static readonly int[] ExpectedBranchSizes = { 5, 6, 9, IntentActions, TargetSlots };
    private const float ObjectiveShapingRange = 50f;
    private const float CountNorm = 5f;

    private struct Candidate
    {
        public Transform root;
        public IDamageable dmg;
        public float dist;
        public bool los;
    }
    private readonly Candidate[] _candidates = new Candidate[TargetSlots];
    private readonly HashSet<Transform> _seenRoots = new HashSet<Transform>();
    private int _candidateCount;
    private int _alliesNearby, _enemiesNearby, _alliesOnPoint, _enemiesOnPoint;
    private float _nextPerceptionTime;

    private float _lastOnPointTime = -1000f;
    private bool _hasPrevObjPotential, _hasPrevRetreat;
    private float _prevObjPotential, _prevRetreat;

    // objective / target-choice diagnostics
    private float _epTime, _epOnPointTime, _epContestTime, _epNeededAbsentTime;
    private float _epCaptures, _epContributedCaptures, _epNeutralizes, _epTargetSwitches;
    private int _epTargetDecisions, _epNonNearest;

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
    [SerializeField] private float damageDealtReward = 0.5f;
    [SerializeField] private float damageTakenPenalty = 0.5f;   // equal to damageDealtReward so even trades aren't net-positive in self-play
    [SerializeField] private float killReward = 0.5f;
    [SerializeField] private float deathPenalty = -0.5f;
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
    [Tooltip("On: the intent and movement branches let the policy reach the point, so these rewards are influenceable.")]
    [SerializeField] private bool useObjectiveRewards = true;
    [Tooltip("Per second on the point, ONLY while the point needs us (not ours yet, or an enemy is on it). Standing on an owned, uncontested point pays nothing.")]
    [SerializeField] private float objectivePresenceReward = 0.02f;
    [Tooltip("Per second while our team's capture progress is rising on a point we do not own yet.")]
    [SerializeField] private float objectiveCaptureProgressReward = 0.2f;
    [Tooltip("Potential-based pull toward the point (applied once per decision), only while the point needs us and we are healthy. Keep small.")]
    [SerializeField] private float objectiveApproachShaping = 0.05f;
    private PointOwner _lastObservedControl = PointOwner.Neutral;
    [Header("Objective Milestone Rewards")]
    [Tooltip("Should stay above a full kill (damage + kill reward) so winning the point is worth more than farming kills.")]
    [SerializeField] private float objectiveCapturedReward = 3.0f;
    [SerializeField] private float objectiveNeutralizedReward = 1.5f;
    [Tooltip("An agent counts as having contributed if it was on the point within this many seconds of the milestone.")]
    [SerializeField] private float objectiveContributionWindow = 5f;
    [Tooltip("Share of a milestone reward paid to teammates who did not contribute: the team still benefits, free-riding pays less.")]
    [SerializeField, Range(0f, 1f)] private float objectiveAssistFraction = 0.25f;
    [Header("Retreat Shaping")]
    [Tooltip("Potential-based reward for gaining distance from the nearest enemy while at or below escapeHpThreshold (applied once per decision).")]
    [SerializeField] private float retreatShaping = 0.05f;

    [Header("Episode / Performance")]
    [SerializeField] private float navRepathInterval = 0.2f;  // seconds between SetDestination calls
    [SerializeField] private float edgeObservationRange = 10f;
    [Tooltip("Seconds between enemy/ally scans (candidate list, team counts). Cheaper than scanning every frame.")]
    [SerializeField] private float perceptionInterval = 0.1f;
    [Tooltip("On: the policy chooses its target via branch 4. Off: always the nearest enemy (old behaviour; useful for A/B tests).")]
    [SerializeField] private bool policyPicksTarget = true;


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
        ValidateBrainSetup();

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
        ResetDecisionState();
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
        RefreshPerception();
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

    private void ResetDecisionState()
    {
        _intent = IntentEngage;
        _lastOnPointTime = -1000f;
        _hasPrevObjPotential = false;
        _hasPrevRetreat = false;
        _nextPerceptionTime = 0f;
        _candidateCount = 0;
    }

    private void ValidateBrainSetup()
    {
        var bp = GetComponent<BehaviorParameters>();
        if (bp == null) return;
        var brain = bp.BrainParameters;
        if (brain.VectorObservationSize != ExpectedObservationSize)
            Debug.LogError($"{name}: Behavior Parameters vector observation size is {brain.VectorObservationSize}, this script writes {ExpectedObservationSize}.", this);

        int[] branches = brain.ActionSpec.BranchSizes;
        bool ok = branches != null && branches.Length == ExpectedBranchSizes.Length;
        for (int i = 0; ok && i < branches.Length; i++) ok = branches[i] == ExpectedBranchSizes[i];
        if (!ok)
            Debug.LogError($"{name}: discrete branch sizes must be {{ {string.Join(", ", ExpectedBranchSizes)} }} (aim, combat, move, intent, target).", this);
    }

    private void ExecuteMacroNavigation()
    {
        if (_movement.IsUnderExternalForce || _movement.IsStunned)
            return;
        if (!_agent.isOnNavMesh) return;

        bool hasObjective = defaultObjective != null;

        // Intent branch: the policy commits to the point. The NavMesh only does the walking.
        if (hasObjective && _intent == IntentObjective)
        {
            _agent.stoppingDistance = 0f;
            _agent.SetDestination(ComputeObjectiveDestination());
            return;
        }

        // Engage: movement chosen by the policy, relative to the opponent (or the objective when nobody is in range).
        if (_currentTarget != null || hasObjective)
        {
            _agent.stoppingDistance = 0f;
            _agent.SetDestination(ComputeMoveDestination(false));
            return;
        }

        // No target and no objective: wander so the agent still finds fights.
        if (!_agent.hasPath || _agent.remainingDistance < 1f)
        {
            Vector3 randomDirection = Random.insideUnitSphere * 15f + transform.position;
            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, 15f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }
    }

    // Objective intent: walk to the point; once inside it, branch 2 becomes fine positioning that is clamped to stay on the point.
    private Vector3 ComputeObjectiveDestination()
    {
        Vector3 toCentre = defaultObjective.position - transform.position;
        toCentre.y = 0f;
        if (toCentre.magnitude > GetObjectiveRadius() * 0.9f)
            return defaultObjective.position;
        return ComputeMoveDestination(true);
    }

    // The policy picks a direction (relative to the opponent, else the objective) every decision and the NavMeshAgent
    // walks it. Nothing here knows about ideal range, cover or flanking; that all has to be learned.
    private Vector3 ComputeMoveDestination(bool clampToObjective)
    {
        Vector3 pos = transform.position;
        if (_moveAction <= 0) return pos;   // 0 = stand still

        Vector3 forward = GetReferenceDirection(pos);
        Vector3 dir = Quaternion.Euler(0f, (_moveAction - 1) * 45f, 0f) * forward;
        Vector3 dest = pos + dir * moveLookahead;

        if (clampToObjective && defaultObjective != null)
        {
            Vector3 offset = dest - defaultObjective.position;
            offset.y = 0f;
            float maxOffset = GetObjectiveRadius() * 0.9f;
            if (offset.magnitude > maxOffset)
                dest = defaultObjective.position + offset.normalized * maxOffset + Vector3.up * (dest.y - defaultObjective.position.y);
        }
        if (_arena != null) dest = _arena.ClampToBounds(dest);

        if (!NavMesh.SamplePosition(dest, out NavMeshHit sample, 2f, NavMesh.AllAreas))
            return pos;
        dest = sample.position;

        // Stop at the first edge or obstacle on the way instead of pathing around it, so a move never becomes a detour.
        if (NavMesh.Raycast(pos, dest, out NavMeshHit blocked, NavMesh.AllAreas))
            dest = blocked.position;
        return dest;
    }

    private Vector3 GetReferenceDirection(Vector3 pos)
    {
        Vector3 refPoint = _currentTarget != null
            ? _currentTarget.position
            : (defaultObjective != null ? defaultObjective.position : pos + transform.forward);
        Vector3 f = refPoint - pos;
        f.y = 0f;
        return f.sqrMagnitude > 0.001f ? f.normalized : transform.forward;
    }

    private float GetObjectiveRadius()
    {
        float radius = 2f;   // default if no BoxCollider is found
        if (defaultObjective != null && defaultObjective.TryGetComponent<BoxCollider>(out var box))
        {
            Vector3 scaledSize = Vector3.Scale(box.size, defaultObjective.localScale);
            radius = Mathf.Max(scaledSize.x, scaledSize.z) * 0.5f;
        }
        return radius;
    }

    private float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private bool IsOnPoint()
        => defaultObjective != null && FlatDistance(transform.position, defaultObjective.position) <= GetObjectiveRadius();

    private bool IsLowHealth()
        => _selfDamageable != null && _selfDamageable.CurrentHealth <= escapeHpThreshold * Mathf.Max(1, _selfDamageable.MaxHealth);

    private static PointOwner OwnerFor(Team team) => team == Team.TeamB ? PointOwner.TeamB : PointOwner.TeamA;

    private bool ObjectiveNeedsUs(CapturePoint capPoint)
        => capPoint.controllingTeam != OwnerFor(_teamMember.team) || _enemiesOnPoint > 0;

    // ---- perception: one scan every perceptionInterval builds the nearest-enemy candidate list and the team counts ----
    private void RefreshPerception()
    {
        ValidateCurrentTarget();
        if (Time.time >= _nextPerceptionTime)
        {
            _nextPerceptionTime = Time.time + perceptionInterval;
            ScanSurroundings();
        }

        // Old behaviour, kept as an option: lock the nearest enemy whenever there is none.
        if (!policyPicksTarget && _currentTarget == null && _candidateCount > 0 && _candidates[0].root != null)
            SetTarget(_candidates[0].root, _candidates[0].dmg);
    }

    private void ValidateCurrentTarget()
    {
        if (_currentTarget == null) return;
        if (!_currentTarget.gameObject.activeInHierarchy
            || (_targetDamageable != null && _targetDamageable.IsDead)
            || Vector3.Distance(transform.position, _currentTarget.position) > detectionRange * 1.5f)
            SetTarget(null, null);
    }

    private void ScanSurroundings()
    {
        _candidateCount = 0;
        _alliesNearby = _enemiesNearby = _alliesOnPoint = _enemiesOnPoint = 0;
        _seenRoots.Clear();

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, detectionRange, _overlapBuffer, searchMask);
        if (hitCount == _overlapBuffer.Length)
            Debug.LogWarning($"{name}: overlap buffer full ({hitCount}); the opponent may be missed. Space arenas further apart or raise the buffer size.", this);

        bool hasObjective = defaultObjective != null;
        float radius = GetObjectiveRadius();

        for (int c = 0; c < hitCount; c++)
        {
            var col = _overlapBuffer[c];
            if (col.gameObject == gameObject) continue;
            if (_arena != null && !_arena.Contains(col.transform)) continue;   // only fight inside our own arena

            Transform root = col.transform.root;
            if (root == transform.root || !_seenRoots.Add(root)) continue;

            IDamageable dmg = root.GetComponent<IDamageable>();
            if (dmg == null || dmg.IsDead) continue;

            bool enemy = _teamMember.IsEnemy(root.gameObject);
            bool ally = !enemy && root.GetComponent<TeamMember>() != null;
            if (!enemy && !ally) continue;

            bool onPoint = hasObjective && FlatDistance(root.position, defaultObjective.position) <= radius;
            if (ally)
            {
                _alliesNearby++;
                if (onPoint) _alliesOnPoint++;
                continue;
            }

            _enemiesNearby++;
            if (onPoint) _enemiesOnPoint++;
            InsertCandidate(root, dmg, Vector3.Distance(transform.position, root.position));
        }

        for (int i = 0; i < _candidateCount; i++)
            _candidates[i].los = HasLineOfSightTo(_candidates[i].root);
    }

    // Keeps the TargetSlots nearest enemies, sorted nearest first.
    private void InsertCandidate(Transform root, IDamageable dmg, float dist)
    {
        int pos = _candidateCount;
        if (pos == TargetSlots)
        {
            if (dist >= _candidates[TargetSlots - 1].dist) return;
            pos = TargetSlots - 1;
        }
        else _candidateCount++;

        while (pos > 0 && _candidates[pos - 1].dist > dist)
        {
            _candidates[pos] = _candidates[pos - 1];
            pos--;
        }
        _candidates[pos] = new Candidate { root = root, dmg = dmg, dist = dist };
    }

    private bool HasLineOfSightTo(Transform t)
    {
        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 dir = (t.position + Vector3.up * 1.0f) - origin;
        return !Physics.Raycast(origin, dir.normalized, dir.magnitude, lineOfSightBlockMask);
    }

    private void SetTarget(Transform target, IDamageable damageable)
    {
        if (target == _currentTarget) return;
        UnsubscribeFromTarget();
        _currentTarget = target;
        _targetDamageable = damageable;
        _targetVelocity = Vector3.zero;
        _hasPrevPotential = false;
        if (target == null)
        {
            _trainingDummy = null;
            return;
        }

        _lastTargetPosition = target.position;
        _targetDamageable.OnDamageReceived += HandleTargetDamageTaken;
        _targetDamageable.OnDied += HandleTargetDeath;
        _targetHpKnown = _targetDamageable.CurrentHealth;
        _trainingDummy = target.TryGetComponent<EnemyHealth>(out var dummyHealth) && dummyHealth.isTrainingDummy ? dummyHealth : null;
    }

    // Branch 4. Damage rewards only flow from the CURRENT target, so a switch is a real commitment.
    private void ApplyTargetChoice(int slot)
    {
        if (slot < 0 || slot >= _candidateCount) return;
        Candidate c = _candidates[slot];
        if (c.root == null || !c.root.gameObject.activeInHierarchy || c.dmg == null || c.dmg.IsDead) return;

        if (_candidateCount >= 2)
        {
            _epTargetDecisions++;
            if (slot > 0) _epNonNearest++;
        }
        if (c.root == _currentTarget) return;
        SetTarget(c.root, c.dmg);
        _epTargetSwitches += 1f;
    }

    private void UpdateTargetVelocity()
    {
        if (_currentTarget == null) return;
        _targetVelocity = (_currentTarget.position - _lastTargetPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        _lastTargetPosition = _currentTarget.position;
    }

    private void UpdateLineOfSight()
    {
        _hasLineOfSight = _currentTarget != null && HasLineOfSightTo(_currentTarget);
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

        // 5. Objective context (2): are we on the point, and is our team's capture progress rising
        bool pushing = false;
        if (defaultObjective != null && _teamMember != null && defaultObjective.TryGetComponent<CapturePoint>(out var cp))
            pushing = cp.captureProgress > 0f && cp.currentChallenger == _teamMember.team;
        sensor.AddObservation(IsOnPoint() ? 1f : 0f);
        sensor.AddObservation(pushing ? 1f : 0f);

        // 6. Team context (4), limited to detectionRange: allies / enemies nearby and on the point
        sensor.AddObservation(Mathf.Clamp01(_alliesNearby / CountNorm));
        sensor.AddObservation(Mathf.Clamp01(_enemiesNearby / CountNorm));
        sensor.AddObservation(Mathf.Clamp01(_alliesOnPoint / CountNorm));
        sensor.AddObservation(Mathf.Clamp01(_enemiesOnPoint / CountNorm));

        // 7. Previous intent (1)
        sensor.AddObservation(_intent == IntentObjective ? 1f : 0f);

        // 8. Target candidates (TargetSlots x CandidateFeatures): nearest enemies, so branch 4 can pick by health, distance, LOS and objective position
        AddCandidateObservations(sensor);
    }

    private void AddCandidateObservations(VectorSensor sensor)
    {
        float radius = GetObjectiveRadius();
        for (int i = 0; i < TargetSlots; i++)
        {
            if (i < _candidateCount && _candidates[i].root != null)
            {
                Candidate c = _candidates[i];
                Vector3 to = c.root.position - transform.position;
                Vector3 local = transform.InverseTransformDirection(to.normalized);
                float hp = c.dmg != null ? (float)c.dmg.CurrentHealth / Mathf.Max(1, c.dmg.MaxHealth) : 1f;
                float objDist = defaultObjective != null ? FlatDistance(c.root.position, defaultObjective.position) : ObjectiveShapingRange;

                sensor.AddObservation(1f);                                              // present
                sensor.AddObservation(local.x);
                sensor.AddObservation(local.z);
                sensor.AddObservation(Mathf.Clamp01(to.magnitude / detectionRange));
                sensor.AddObservation(hp);
                sensor.AddObservation(c.los ? 1f : 0f);
                sensor.AddObservation(Mathf.Clamp01(objDist / ObjectiveShapingRange));  // how far the enemy is from the point
                sensor.AddObservation(objDist <= radius ? 1f : 0f);                     // enemy is contesting the point
                sensor.AddObservation(c.root == _currentTarget ? 1f : 0f);              // currently focused
            }
            else
            {
                sensor.AddObservation(0f); sensor.AddObservation(0f); sensor.AddObservation(0f);
                sensor.AddObservation(1f); sensor.AddObservation(0f); sensor.AddObservation(0f);
                sensor.AddObservation(1f); sensor.AddObservation(0f); sensor.AddObservation(0f);
            }
        }
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

        if (defaultObjective == null)
            actionMask.SetActionEnabled(3, IntentObjective, false);   // nothing to go to

        // Branch 4: only slots that hold an enemy. Slot 0 stays enabled so the branch is never fully masked.
        for (int i = 1; i < TargetSlots; i++)
            if (i >= _candidateCount)
                actionMask.SetActionEnabled(4, i, false);
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
    // Always runs (stats + milestone detection); AddReward calls are gated by useObjectiveRewards.
    private void EvaluateObjectiveRewards()
    {
        if (defaultObjective == null || _teamMember == null) return;
        if (!defaultObjective.TryGetComponent<CapturePoint>(out var capPoint)) return;

        // Between matches the point is reset after OnEpisodeBegin has already run: resync instead of rewarding the reset.
        if (!MatchLive)
        {
            _lastObservedControl = capPoint.controllingTeam;
            return;
        }

        float dt = Time.deltaTime;
        PointOwner myOwner = OwnerFor(_teamMember.team);
        bool owned = capPoint.controllingTeam == myOwner;
        bool onPoint = IsOnPoint();
        bool needsUs = ObjectiveNeedsUs(capPoint);

        _epTime += dt;
        if (onPoint)
        {
            _epOnPointTime += dt;
            _lastOnPointTime = Time.time;
            if (needsUs) _epContestTime += dt;
        }
        else if (needsUs) _epNeededAbsentTime += dt;

        if (useObjectiveRewards && onPoint)
        {
            // No pay for idling on a point that is already ours and uncontested.
            if (needsUs) AddReward(objectivePresenceReward * dt);
            if (!owned && capPoint.captureProgress > 0f && capPoint.currentChallenger == _teamMember.team)
                AddReward(objectiveCaptureProgressReward * dt);
        }

        if (capPoint.controllingTeam != _lastObservedControl)
        {
            // Whole team benefits from a capture, but players who were not there get only a fraction.
            bool contributed = Time.time - _lastOnPointTime <= objectiveContributionWindow;
            float scale = contributed ? 1f : objectiveAssistFraction;

            if (capPoint.controllingTeam == myOwner)
            {
                _epCaptures += 1f;
                if (contributed) _epContributedCaptures += 1f;
                if (useObjectiveRewards) AddReward(objectiveCapturedReward * scale);
            }
            else if (capPoint.controllingTeam == PointOwner.Neutral && _lastObservedControl != PointOwner.Neutral && _lastObservedControl != myOwner)
            {
                _epNeutralizes += 1f;
                if (useObjectiveRewards) AddReward(objectiveNeutralizedReward * scale);
            }
            _lastObservedControl = capPoint.controllingTeam;
        }
    }

    // Potential-based (applied once per decision, gamma 0.99 like the trainer): pull toward the point only while it needs us
    // and we are healthy, so it never fights the retreat term.
    private void ApplyObjectiveApproachShaping()
    {
        if (!useObjectiveRewards || defaultObjective == null || _teamMember == null || !MatchLive || IsLowHealth()
            || !defaultObjective.TryGetComponent<CapturePoint>(out var capPoint) || !ObjectiveNeedsUs(capPoint))
        {
            _hasPrevObjPotential = false;
            return;
        }

        float outside = Mathf.Max(0f, FlatDistance(transform.position, defaultObjective.position) - GetObjectiveRadius());
        float potential = -Mathf.Min(outside, ObjectiveShapingRange) / ObjectiveShapingRange;
        if (_hasPrevObjPotential)
            AddReward(objectiveApproachShaping * (0.99f * potential - _prevObjPotential));
        _prevObjPotential = potential;
        _hasPrevObjPotential = true;
    }

    // Potential-based: gaining distance from the nearest enemy while low on health. Cannot be farmed (telescopes), and
    // is complementary to the mobility escape reward, which pays for a specific ability.
    private void ApplyRetreatShaping()
    {
        if (retreatShaping <= 0f || !MatchLive || !IsLowHealth() || _candidateCount == 0)
        {
            _hasPrevRetreat = false;
            return;
        }

        float nearest = float.MaxValue;
        for (int i = 0; i < _candidateCount; i++)
            if (_candidates[i].root != null)
                nearest = Mathf.Min(nearest, Vector3.Distance(transform.position, _candidates[i].root.position));
        if (nearest == float.MaxValue)
        {
            _hasPrevRetreat = false;
            return;
        }

        float potential = Mathf.Min(nearest, detectionRange) / detectionRange;
        if (_hasPrevRetreat)
            AddReward(retreatShaping * (0.99f * potential - _prevRetreat));
        _prevRetreat = potential;
        _hasPrevRetreat = true;
    }

    // Potential-based shaping (gamma matches the trainer's 0.99): gives a dense "get to a useful range"
    // signal without changing which policy is optimal.
    private void ApplyRangeShaping()
    {
        if (_currentTarget == null || _intent != IntentEngage || IsLowHealth() || !MatchLive)
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
            stats.Add("Combat/ObjectiveIntent", _epObjectiveIntent / d);   // fraction of decisions spent heading for the point
            if (_epTime > 0f)
            {
                stats.Add("Objective/OnPointFraction", _epOnPointTime / _epTime);
                stats.Add("Objective/AwayFraction", 1f - _epOnPointTime / _epTime);
                stats.Add("Objective/ContestingFraction", _epContestTime / _epTime);               // on the point while it needed us
                stats.Add("Objective/NeededButAbsentFraction", _epNeededAbsentTime / _epTime);     // point needed us, we were elsewhere
            }
            stats.Add("Objective/TeamCaptures", _epCaptures);
            stats.Add("Objective/ContributedCaptures", _epContributedCaptures);
            stats.Add("Objective/Neutralizations", _epNeutralizes);
            stats.Add("Combat/TargetSwitches", _epTargetSwitches);
            if (_epTargetDecisions > 0)
                stats.Add("Combat/NonNearestTargetFraction", _epNonNearest / (float)_epTargetDecisions);
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
        _epDecisions = _epWithTarget = _epWithLos = _epInRange = _epAttackUses = _epAbilityUses = _epObjectiveIntent = 0;
        _epTime = _epOnPointTime = _epContestTime = _epNeededAbsentTime = 0f;
        _epCaptures = _epContributedCaptures = _epNeutralizes = _epTargetSwitches = 0f;
        _epTargetDecisions = _epNonNearest = 0;
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

        if (_movement == null)
        {
            _hasAimTarget = false;
            return;
        }

        var discreteActions = actions.DiscreteActions;
        if (discreteActions.Length < 5)
        {
            Debug.LogError($"RLEnemyCombatAgent requires 5 discrete branches (aim 5, combat 6, move 9, intent 2, target {TargetSlots}), but received {discreteActions.Length}.");
            return;
        }

        if (_movement.IsStunned || _movement.IsUnderExternalForce)
        {
            _hasAimTarget = false;
            return;
        }

        // Branch 3: Intent. Applies even with no opponent in range, so the policy owns the fight-vs-objective call.
        _intent = Mathf.Clamp(discreteActions[3], 0, IntentActions - 1);
        if (_intent == IntentObjective) _epObjectiveIntent++;

        // Branch 2: Positioning. Read before the target check: movement matters with no opponent too (relative to the objective).
        _moveAction = Mathf.Clamp(discreteActions[2], 0, MoveActions - 1);

        ApplyObjectiveApproachShaping();
        ApplyRetreatShaping();

        // Branch 4: Target selection
        if (policyPicksTarget) ApplyTargetChoice(discreteActions[4]);

        if (_currentTarget == null)
        {
            _hasAimTarget = false;
            return;
        }

        ApplyRangeShaping();   // was defined but never called; now only active while engaging and healthy

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
        ResetDecisionState();
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
        bool won = _teamMember != null && _teamMember.team == winner.Value;
        Academy.Instance.StatsRecorder.Add("Match/Win", won ? 1f : 0f);   // TensorBoard mean = win rate
        AddReward(won ? winRewardValue : lossRewardValue);
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

        if (discreteActions.Length > 3)
            discreteActions[3] = Input.GetKey(KeyCode.O) ? IntentObjective : IntentEngage;   // hold O = go to objective

        if (discreteActions.Length > 4)
            discreteActions[4] = 0;   // nearest enemy
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