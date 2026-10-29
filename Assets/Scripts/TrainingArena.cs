using System.Collections;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// One self-contained training cell: an agent, a sparring dummy and the bounds they fight in.
///
/// IMPORTANT: the agent and dummy must be scene ROOTS, never children of this object. The combat code
/// resolves characters with transform.root (team checks, damage attribution, projectiles), so a nested
/// character is invisible to it. Assign agentPrefab / dummyPrefab and the arena spawns them as roots
/// for you, which also lets you duplicate the whole arena for parallel training.
///
/// Trainer environment parameters (see config/combat_config.yaml) override the defaults below.
/// </summary>
public class TrainingArena : MonoBehaviour
{
    [Header("Spawned by the arena as scene roots (preferred; lets you duplicate the arena)")]
    public RLEnemyCombatAgent agentPrefab;
    public EnemyHealth dummyPrefab;

    [Header("...or objects already in the scene (must NOT be children of this arena)")]
    public RLEnemyCombatAgent agent;
    public EnemyHealth dummy;

    [Header("Optional")]
    [Tooltip("Defaults to the arena centre; used as the agent's fallback objective.")]
    public Transform objective;
    [Tooltip("If set, a hero is loaded onto the agent at start.")]
    public CharacterDatabase characterDatabase;
    [Tooltip("-1 = random hero per arena at start.")]
    public int heroIndex = -1;

    [Header("Bounds (local space, metres, measured inside the walls)")]
    public Vector2 halfExtents = new Vector2(14f, 14f);
    public float spawnMargin = 2f;
    public Vector2 spawnDistanceRange = new Vector2(6f, 18f);
    public float chaseStopDistance = 1.5f;

    [Header("Defaults (trainer environment parameters override these)")]
    public float dummyHealth = 600f;   // env param: dummy_health
    public float dummyAttacks = 0f;    // env param: dummy_attacks  (0 = passive, 1 = fights back)
    public float dummySpeed = 0f;      // env param: dummy_speed    (m/s, 0 = stationary)

    private EnemyAttack _dummyAttack;
    private float _dummyYOffset;
    private float _dummySpeedNow;
    private bool _spawnedAgent;
    private bool _spawnedDummy;

    public Transform Objective => objective != null ? objective : transform;

    /// <summary>True if the transform belongs to this arena's agent or dummy.</summary>
    public bool Contains(Transform t)
    {
        if (t == null) return false;
        Transform root = t.root;
        return (agent != null && root == agent.transform.root) || (dummy != null && root == dummy.transform.root);
    }

    private void Awake()
    {
        if (agent == null && agentPrefab != null)
        {
            agent = Instantiate(agentPrefab, transform.position, transform.rotation);   // no parent: stays a scene root
            agent.name = name + "_Agent";
            _spawnedAgent = true;
        }

        if (dummy == null && dummyPrefab != null)
        {
            dummy = Instantiate(dummyPrefab, transform.position, transform.rotation);
            dummy.transform.position += Vector3.up;   // lift it up so it doesn't spawn half-buried in the floor
            dummy.name = name + "_Dummy";
            _spawnedDummy = true;
        }

        if (dummy != null)
        {
            _dummyAttack = dummy.GetComponent<EnemyAttack>();
            if (_dummyAttack != null) _dummyAttack.assignedAgent = agent;
            _dummyYOffset = dummy.transform.position.y - transform.position.y;
            if (dummy.TryGetComponent<TeamMember>(out var dummyTeam)) dummyTeam.team = Team.TeamA;
            dummy.isTrainingDummy = true;
        }

        if (agent != null)
        {
            if (agent.TryGetComponent<TeamMember>(out var agentTeam)) agentTeam.team = Team.TeamB;
            agent.SetArena(this);
        }
    }

    private void Start()
    {
        if (agent != null && characterDatabase != null && characterDatabase.CharacterCount > 0
            && agent.TryGetComponent<HeroManager>(out var heroManager))
        {
            int count = characterDatabase.CharacterCount;
            int index = heroIndex >= 0 ? heroIndex % count : Random.Range(0, count);
            heroManager.LoadHero(characterDatabase.GetHeroAtIndex(index));
        }

        StartCoroutine(ValidateAfterSetup());
    }

    private void OnDestroy()
    {
        if (_spawnedAgent && agent != null) Destroy(agent.gameObject);
        if (_spawnedDummy && dummy != null) Destroy(dummy.gameObject);
    }

    private void FixedUpdate()
    {
        if (dummy == null || agent == null || dummy.IsDead || _dummySpeedNow <= 0f) return;

        Vector3 toAgent = agent.transform.position - dummy.transform.position;
        toAgent.y = 0f;
        if (toAgent.magnitude <= chaseStopDistance) return;

        Vector3 next = dummy.transform.position + toAgent.normalized * (_dummySpeedNow * Time.fixedDeltaTime);
        dummy.transform.position = ClampToBounds(next);
    }

    /// <summary>Called by the agent at the start of every episode. Resets the dummy and returns the agent's spawn pose.</summary>
    public void ResetEpisode(out Vector3 agentPosition, out Quaternion agentRotation)
    {
        var p = Academy.Instance.EnvironmentParameters;
        int hp = Mathf.Max(1, Mathf.RoundToInt(p.GetWithDefault("dummy_health", dummyHealth)));
        bool attacks = p.GetWithDefault("dummy_attacks", dummyAttacks) > 0.5f;
        _dummySpeedNow = p.GetWithDefault("dummy_speed", dummySpeed);

        if (dummy != null)
        {
            dummy.maxHealth = hp;
            dummy.ResetHealth();
            if (_dummyAttack != null) _dummyAttack.enabled = attacks;

            Vector3 spot = RandomPoint(spawnMargin);
            dummy.transform.position = new Vector3(spot.x, transform.position.y + _dummyYOffset, spot.z);
        }

        Vector3 anchor = dummy != null ? dummy.transform.position : transform.position;
        // env param spawn_distance_scale > 0: start inside the hero's attack range so early random actions can land hits
        float closeScale = p.GetWithDefault("spawn_distance_scale", 0f);
        float distance = closeScale > 0f && agent != null
            ? Mathf.Max(1.5f, Random.Range(0.4f, 1f) * closeScale * agent.CombatRange)
            : Random.Range(spawnDistanceRange.x, spawnDistanceRange.y);
        Vector3 offset = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * distance;

        Vector3 desired = ClampToBounds(anchor + offset);
        desired.y = transform.position.y;
        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            desired = hit.position;

        Vector3 face = anchor - desired;
        face.y = 0f;
        agentPosition = desired;
        agentRotation = face.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(face) * Quaternion.Euler(0f, Random.Range(-60f, 60f), 0f)
            : Quaternion.identity;
    }

    public Vector3 ClampToBounds(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        local.x = Mathf.Clamp(local.x, -halfExtents.x, halfExtents.x);
        local.z = Mathf.Clamp(local.z, -halfExtents.y, halfExtents.y);
        return transform.TransformPoint(local);
    }

    private Vector3 RandomPoint(float margin)
    {
        float x = Random.Range(-halfExtents.x + margin, halfExtents.x - margin);
        float z = Random.Range(-halfExtents.y + margin, halfExtents.y - margin);
        return transform.TransformPoint(new Vector3(x, 0f, z));
    }

    // ---------------------------------------------------------------- diagnostics

    private IEnumerator ValidateAfterSetup()
    {
        yield return null;
        yield return null;   // give HeroManager.LoadHero a frame to equip abilities
        Validate();
    }

    [ContextMenu("Validate Arena")]
    public void Validate()
    {
        string tag = $"[TrainingArena '{name}'] ";
        int problems = 0;
        void Fail(string msg) { problems++; Debug.LogError(tag + msg, this); }
        void Warn(string msg) { problems++; Debug.LogWarning(tag + msg, this); }

        if (agent == null) { Fail("No agent. Assign agentPrefab (or an agent that is a scene root)."); return; }
        if (dummy == null) { Fail("No dummy. Assign dummyPrefab (or a dummy that is a scene root)."); return; }

        if (agent.transform.parent != null)
            Fail($"'{agent.name}' is a child of '{agent.transform.parent.name}'. Characters must be scene roots (the combat code uses transform.root), so the agent will never find or hit targets. Unparent it, or use agentPrefab.");
        if (dummy.transform.parent != null)
            Fail($"'{dummy.name}' is a child of '{dummy.transform.parent.name}'. Characters must be scene roots (TeamMember.IsEnemy uses transform.root), so the agent will never target it. Unparent it, or use dummyPrefab.");

        bool dummyInMask = false;
        foreach (var c in dummy.GetComponentsInChildren<Collider>())
            if ((agent.SearchMask.value & (1 << c.gameObject.layer)) != 0) { dummyInMask = true; break; }
        if (!dummyInMask)
            Fail($"None of the dummy's colliders are on a layer in the agent's searchMask, so it can't be detected (dummy layer = {LayerMask.LayerToName(dummy.gameObject.layer)}).");

        if (!agent.TryGetComponent<AbilityManager>(out var abilities) || abilities.primaryAttack == null)
            Warn("The agent has no abilities, so it cannot attack. No hero is loaded: assign characterDatabase on this arena.");

        if (agent.TryGetComponent<BehaviorParameters>(out var bp)
            && bp.BehaviorType == BehaviorType.Default && bp.Model == null && !Academy.Instance.IsCommunicatorOn)
            Warn("No trainer is connected and the agent has no model, so it runs the Heuristic (it only attacks while you hold Space). Start mlagents-learn first, then press Play.");

        if (!NavMesh.SamplePosition(agent.transform.position, out _, 3f, NavMesh.AllAreas))
            Warn("The agent is not on a baked NavMesh. Bake the NavMeshSurface after placing the arenas.");

        if (dummy.IsDead)
            Warn("The dummy starts dead.");

        if (problems == 0)
            Debug.Log(tag + "setup looks good.", this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.up * 0.5f, new Vector3(halfExtents.x * 2f, 1f, halfExtents.y * 2f));
    }
}
