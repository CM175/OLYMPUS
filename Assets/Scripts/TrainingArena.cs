using System.Collections;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// One self-contained training cell: an agent, an opponent agent, and the bounds they fight in.
/// </summary>
// Runs before the agents' own Awake/OnEnable so TeamId is set before ML-Agents registers each behaviour.
[DefaultExecutionOrder(-1000)]
public class TrainingArena : MonoBehaviour
{
    [Header("Training Match")]
    [SerializeField] private TrainingMatchManager matchManager;

    public TrainingMatchManager MatchManager => matchManager;
    [Header("Spawned by the arena as scene roots")]
    public RLEnemyCombatAgent agentPrefab;
    public RLEnemyCombatAgent opponentPrefab;

    [Header("Objects in the scene")]
    public RLEnemyCombatAgent agent;
    public RLEnemyCombatAgent opponent;

    [Header("Optional")]
    [Tooltip("Defaults to the arena centre; used as the agent's fallback objective.")]
    [SerializeField] private Transform objective;
    public CharacterDatabase characterDatabase;
    public int heroIndex = -1;

    [Header("Bounds (local space, metres, measured inside the walls)")]
    public Vector2 halfExtents = new Vector2(14f, 14f);
    public float spawnMargin = 2f;
    public Vector2 spawnDistanceRange = new Vector2(6f, 18f);

    private bool _spawnedAgent;
    private bool _spawnedOpponent;
    private GameObject _spawnHolder;

    public Transform Objective => objective != null ? objective : transform;

    /// <summary>True if the transform belongs to this arena's agent or opponent.</summary>
    public bool Contains(Transform t)
    {
        if (t == null) return false;
        Transform root = t.root;
        return (agent != null && root == agent.transform.root) || (opponent != null && root == opponent.transform.root);
    }

    private void Awake()
    {
        if (agent == null && agentPrefab != null)
        {
            agent = SpawnDormant(agentPrefab, transform.position, name + "_Agent");
            _spawnedAgent = true;
        }
        if (opponent == null && opponentPrefab != null)
        {
            opponent = SpawnDormant(opponentPrefab, transform.position + Vector3.up, name + "_Opponent");
            _spawnedOpponent = true;
        }

        if (opponent != null)
        {
            if (opponent.TryGetComponent<TeamMember>(out var oppTeam)) oppTeam.team = Team.TeamA;
            opponent.SetArena(this);
            if (opponent.TryGetComponent<EnemyHealth>(out var oppHealth)) oppHealth.isTrainingDummy = true;


            if (opponent.TryGetComponent<BehaviorParameters>(out var oppParams))
                oppParams.TeamId = 0;

        }

        if (agent != null)
        {
            if (agent.TryGetComponent<TeamMember>(out var agentTeam)) agentTeam.team = Team.TeamB;
            agent.SetArena(this);
            if (agent.TryGetComponent<EnemyHealth>(out var agentHealth)) agentHealth.isTrainingDummy = true;

            if (agent.TryGetComponent<BehaviorParameters>(out var agentParams))
                agentParams.TeamId = 1;
        }

        if (matchManager == null)
            matchManager = GetComponent<TrainingMatchManager>();

        if (matchManager == null)
            matchManager = gameObject.AddComponent<TrainingMatchManager>();

        // Everything (team, TeamId, arena) is configured: release spawned agents. Their Awake/OnEnable, and with it
        // ML-Agents' behaviour registration ("CombatAgent?team=N"), only run now, so TeamId is actually honoured.
        if (_spawnHolder != null)
        {
            if (_spawnedAgent && agent != null) agent.transform.SetParent(null, true);
            if (_spawnedOpponent && opponent != null) opponent.transform.SetParent(null, true);
            Destroy(_spawnHolder);
        }
    }

    /// <summary>Instantiates under an inactive holder so the clone's Awake/OnEnable don't run until the arena has configured it.</summary>
    private RLEnemyCombatAgent SpawnDormant(RLEnemyCombatAgent prefab, Vector3 position, string objectName)
    {
        if (_spawnHolder == null)
        {
            _spawnHolder = new GameObject(name + "_SpawnHolder");
            _spawnHolder.SetActive(false);
        }
        RLEnemyCombatAgent instance = Instantiate(prefab, position, transform.rotation, _spawnHolder.transform);
        instance.name = objectName;
        return instance;
    }

    private void Start()
    {
        if (agent != null && characterDatabase != null && characterDatabase.CharacterCount > 0
            && agent.TryGetComponent<HeroManager>(out var heroManager))
        {
            int count = characterDatabase.CharacterCount;
            int index = heroIndex >= 0 ? heroIndex % count : Random.Range(0, count);
            heroManager.LoadHero(characterDatabase.GetHeroAtIndex(index));
            agent.ResyncAfterHeroChange();
        }

        if (opponent != null && characterDatabase != null && characterDatabase.CharacterCount > 0
            && opponent.TryGetComponent<HeroManager>(out var oppHeroManager))
        {
            int count = characterDatabase.CharacterCount;
            int index = heroIndex >= 0 ? (heroIndex + 1) % count : Random.Range(0, count);
            oppHeroManager.LoadHero(characterDatabase.GetHeroAtIndex(index));
            opponent.ResyncAfterHeroChange();
        }

        StartCoroutine(ValidateAfterSetup());
    }

    private void OnDestroy()
    {
        if (_spawnedAgent && agent != null) Destroy(agent.gameObject);
        if (_spawnedOpponent && opponent != null) Destroy(opponent.gameObject);
    }

    /// <summary>Re-rolls both heroes between matches so a single policy sees every matchup (skipped when heroIndex pins them).</summary>
    public void RerollHeroes()
    {
        if (heroIndex >= 0 || characterDatabase == null || characterDatabase.CharacterCount == 0) return;
        int count = characterDatabase.CharacterCount;
        if (agent != null && agent.TryGetComponent<HeroManager>(out var hm))
            hm.LoadHero(characterDatabase.GetHeroAtIndex(Random.Range(0, count)));
        if (opponent != null && opponent.TryGetComponent<HeroManager>(out var ohm))
            ohm.LoadHero(characterDatabase.GetHeroAtIndex(Random.Range(0, count)));
    }

    /// <summary>Returns a spawn pose for one agent without moving or resetting its opponent.</summary>
    public void GetSpawnPose(RLEnemyCombatAgent requestingAgent, out Vector3 position, out Quaternion rotation)
    {
        TeamMember member = requestingAgent != null ? requestingAgent.GetComponent<TeamMember>() : null;
        bool teamA = member != null && member.team == Team.TeamA;

        float side = Mathf.Max(1f, halfExtents.x * 0.65f);
        float localX = teamA ? -side : side;
        float localZ = Random.Range(-halfExtents.y + spawnMargin, halfExtents.y - spawnMargin);
        Vector3 localSpawn = new Vector3(localX, 0f, localZ);
        Vector3 desired = transform.TransformPoint(localSpawn);
        desired = ClampToBounds(desired);

        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            desired = hit.position;

        Vector3 centreDirection = Objective.position - desired;
        centreDirection.y = 0f;
        rotation = centreDirection.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(centreDirection.normalized)
            : transform.rotation;
        position = desired;
    }

    /// <summary>Resets objective state after a completed match.</summary>
    public void ResetObjectiveState()
    {
        CapturePoint point = Objective != null ? Objective.GetComponent<CapturePoint>() : null;
        if (point != null) point.ResetForTraining();
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

    private IEnumerator ValidateAfterSetup()
    {
        yield return null;
        yield return null;
        Validate();
    }

    [ContextMenu("Validate Arena")]
    public void Validate()
    {
        string tag = $"[TrainingArena '{name}'] ";
        int problems = 0;
        void Fail(string msg) { problems++; Debug.LogError(tag + msg, this); }
        void Warn(string msg) { problems++; Debug.LogWarning(tag + msg, this); }

        if (agent == null) { Fail("No agent."); return; }
        if (opponent == null) { Fail("No opponent."); return; }

        if (agent.TryGetComponent<BehaviorParameters>(out var ap) && opponent.TryGetComponent<BehaviorParameters>(out var op))
        {
            if (ap.TeamId == op.TeamId) Fail($"Agent and opponent share TeamId {ap.TeamId}; self-play needs different team ids.");
            if (ap.BehaviorName != op.BehaviorName) Warn($"Behaviour names differ ('{ap.BehaviorName}' vs '{op.BehaviorName}'); they won't share a policy.");
        }

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