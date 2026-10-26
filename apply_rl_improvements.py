#!/usr/bin/env python3
"""OLYMPUS RL improvements. Run from the repo root:  python apply_rl_improvements.py
CRLF/LF tolerant, idempotent, all-or-nothing per file (mismatching files are skipped and reported)."""
import re, sys, pathlib

ROOT = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else '.')
if not (ROOT / 'Assets').is_dir():
    sys.exit("Run this from the repo root (the folder containing Assets/).")
applied, skipped, already = [], [], []

def edit(path, subs, marker):
    p = ROOT / path
    if not p.exists():
        skipped.append((path, "file not found")); return
    raw = p.read_bytes().decode('utf-8')
    crlf = '\r\n' in raw
    s = raw.replace('\r\n', '\n')
    if re.search(marker, s):
        already.append(path); return
    for kind, a, b in subs:
        if kind == 's':
            if s.count(a) != 1:
                skipped.append((path, f"expected 1 match, found {s.count(a)}: {a.strip()[:70]!r}")); return
            s = s.replace(a, b)
        else:  # 're' (must match) / 'ro' (optional)
            s, n = re.subn(a, lambda m: b, s, flags=re.S)
            if n < 1 and kind == 're':
                skipped.append((path, f"no regex match: {a[:70]!r}")); return
    if crlf: s = s.replace('\n', '\r\n')
    p.write_bytes(s.encode('utf-8'))
    applied.append(path)

AGENT = 'Assets/Scripts/RLEnemyCombatAgent.cs'

# ---- 1. reward block: normalised, exploit-free ------------------------------------------------
REWARD_BLOCK = '''[Header("Reward Shaping (damage terms are fractions of max health, so a full kill is about +1)")]
    [SerializeField] private float damageDealtReward = 1.0f;
    [SerializeField] private float damageTakenPenalty = 0.5f;
    [SerializeField] private float killReward = 1.0f;
    [SerializeField] private float deathPenalty = -1.0f;
    [SerializeField] private float existentialPenaltyPerStep = -0.0005f;
    [SerializeField] private float rangeShaping = 0.1f;

    [Header("Episode / Performance")]
    [SerializeField] private int maxEpisodeSteps = 4000;      // ~80s of game time; time-outs bootstrap the value estimate
    [SerializeField] private float navRepathInterval = 0.2f;  // seconds between SetDestination calls
    [SerializeField] private float edgeObservationRange = 10f;'''

NEW_FIELDS = '''private bool _hasSpawnCached = false;

    private float _strafeSide = 1f;
    private float _nextNavTime;
    private int _selfHpKnown;
    private int _targetHpKnown;
    private int _lastEndFrame = -1;
    private bool _hasPrevPotential;
    private float _prevPotential;'''

NEW_OBS = '''public override void CollectObservations(VectorSensor sensor)
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

    public override void WriteDiscreteActionMask'''

NEW_HELPERS = '''private float GetCombatRange()
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

    public void EndEpisodeOnce()
    {
        // Self-death and target-death can both fire in the same frame; only end the episode once.
        if (Time.frameCount == _lastEndFrame) return;
        _lastEndFrame = Time.frameCount;
        EndEpisode();
    }

    private float GetCooldownNormalized(Ability ability)'''

NEW_HANDLERS = '''private void HandleSelfDamageTaken(int damage, GameObject attacker)
    {
        int hpNow = _selfDamageable != null ? _selfDamageable.CurrentHealth : 0;
        float lost = hpNow > 0 ? damage : Mathf.Min(damage, _selfHpKnown);   // ignore overkill
        _selfHpKnown = hpNow;

        // Environment damage (KillZone etc.) has no attacker: the death penalty already covers it.
        if (attacker == null || attacker == gameObject || _selfDamageable == null) return;

        AddReward(-damageTakenPenalty * lost / Mathf.Max(1, _selfDamageable.MaxHealth));
    }

    private void HandleSelfDeath(GameObject killer)
    {
        AddReward(deathPenalty);
        EndEpisodeOnce();
    }

    private void HandleTargetDeath(GameObject killer)
    {
        if (killer == gameObject)
            AddReward(killReward);

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
        AddReward(damageDealtReward * lost / Mathf.Max(1, _targetDamageable.MaxHealth));
    }
'''

edit(AGENT, [
 ('re', r'\[Header\("Reward Shaping"\)\].*?existentialPenaltyPerStep = -0\.0005f;', REWARD_BLOCK),
 ('s', 'private bool _hasSpawnCached = false;', NEW_FIELDS),
 ('s', '_isInitialized = true;', 'if (MaxStep <= 0) MaxStep = maxEpisodeSteps;\n        _isInitialized = true;'),
 ('re', r'Debug\.Log\(\$"\[RLEnemyCombatAgent\] OnEpisodeBegin\(\) called for \{gameObject\.name\}"\);\s*CacheSpawnPosition\(\);\s*UnsubscribeFromTarget\(\);',
  '''CacheSpawnPosition();

        // A time-out can leave the sparring dummy damaged; restore it so every episode starts equal.
        if (_targetDamageable is EnemyHealth staleTarget && staleTarget.isTrainingDummy && !staleTarget.IsDead)
            staleTarget.ResetHealth();

        UnsubscribeFromTarget();
        _strafeSide = Random.value < 0.5f ? 1f : -1f;
        _hasPrevPotential = false;'''),
 ('s', '// 2. Resolve Spawn Target', 'if (_selfDamageable != null) _selfHpKnown = _selfDamageable.CurrentHealth;\n\n        // 2. Resolve Spawn Target'),
 ('s', 'ExecuteMacroNavigation();', 'if (Time.time >= _nextNavTime)\n        {\n            _nextNavTime = Time.time + navRepathInterval;\n            ExecuteMacroNavigation();\n        }'),
 ('s', 'float combatRange = _abilityManager.primaryAttack != null ? _abilityManager.primaryAttack.range : 15f;', 'float combatRange = GetCombatRange();'),
 ('s', 'float side = (GetInstanceID() % 2 == 0) ? 1f : -1f;', 'float side = _strafeSide;'),
 ('s', '_targetDamageable.OnDied += HandleTargetDeath;', '_targetDamageable.OnDied += HandleTargetDeath;\n            _targetHpKnown = _targetDamageable.CurrentHealth;'),
 ('re', r'public override void CollectObservations\(VectorSensor sensor\)\s*\{.*?\n    \}\n\s*public override void WriteDiscreteActionMask', NEW_OBS),
 ('s', 'AddReward(existentialPenaltyPerStep);', 'AddReward(existentialPenaltyPerStep);\n        ApplyRangeShaping();'),
 ('s', 'private float GetCooldownNormalized(Ability ability)', NEW_HELPERS),
 ('re', r'private void HandleSelfDamageTaken\(int damage, GameObject attacker\)\s*\{.*?(?=\s*private void UnsubscribeFromTarget\(\))', NEW_HANDLERS),
], marker=r'float damageDealtReward = ')

# ---- 2. strip per-hit Debug.Log spam (real cost at 20x time-scale) -------------------------------
edit('Assets/Scripts/EnemyHealth.cs', [
 ('ro', r'\n[ \t]*Debug\.Log\(\$"\[EnemyHealth\][^\n]*\);', ''),
], marker=r'^(?![\s\S]*\[EnemyHealth\])[\s\S]*$')   # already clean if the tag no longer appears

# ---- 3. observation size on the prefab: 17 -> 22 -------------------------------------------------
edit('Assets/Prefabs/Enemy.prefab', [
 ('s', 'VectorObservationSize: 17', 'VectorObservationSize: 22'),
], marker=r'VectorObservationSize: 22')

# ---- 4. trainer config -----------------------------------------------------------------------------
CFG = '''# v2 - retuned after HybridCombat_01 (reward scale fix, larger batches, longer horizon)
behaviors:
  CombatAgent:
    trainer_type: ppo
    hyperparameters:
      batch_size: 512
      buffer_size: 10240
      learning_rate: 0.0003
      beta: 0.005
      epsilon: 0.2
      lambd: 0.95
      num_epoch: 3
      learning_rate_schedule: linear
      beta_schedule: constant
      epsilon_schedule: linear
    network_settings:
      normalize: true
      hidden_units: 256
      num_layers: 2
    reward_signals:
      extrinsic:
        gamma: 0.99
        strength: 1.0
    keep_checkpoints: 10
    checkpoint_interval: 100000
    max_steps: 3000000
    time_horizon: 256
    summary_freq: 10000
'''
cp = ROOT / 'config/combat_config.yaml'
if cp.exists() and b'# v2' in cp.read_bytes():
    already.append('config/combat_config.yaml')
else:
    if cp.exists():
        (ROOT / 'config/combat_config.yaml.bak').write_bytes(cp.read_bytes())
    cp.parent.mkdir(exist_ok=True)
    cp.write_text(CFG)
    applied.append('config/combat_config.yaml (old one saved as .bak)')

print(f"\nApplied: {len(applied)}, already done: {len(already)}")
for a in applied: print("  +", a)
if skipped:
    print(f"SKIPPED: {len(skipped)} (left untouched):")
    for p, why in skipped: print(f"  - {p}: {why}")
    print("\nSend me this list and I'll adapt the edits to your versions.")
