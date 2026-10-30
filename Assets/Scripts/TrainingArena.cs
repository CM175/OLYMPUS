using System.Collections;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// One self-contained training cell: an agent, an opponent agent, and the bounds they fight in.
/// </summary>
public class TrainingArena : MonoBehaviour
{
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
            agent = Instantiate(agentPrefab, transform.position, transform.rotation);
            agent.name = name + "_Agent";
            _spawnedAgent = true;
        }
        if (opponent == null && opponentPrefab != null)
        {
            opponent = Instantiate(opponentPrefab, transform.position + Vector3.up, transform.rotation);
            opponent.name = name + "_Opponent";
            _spawnedOpponent = true;
        }

        if (opponent != null)
        {
            if (opponent.TryGetComponent<TeamMember>(out var oppTeam)) oppTeam.team = Team.TeamA;
            opponent.SetArena(this);
            if (opponent.TryGetComponent<EnemyHealth>(out var oppHealth)) oppHealth.isTrainingDummy = true;
        }

        if (agent != null)
        {
            if (agent.TryGetComponent<TeamMember>(out var agentTeam)) agentTeam.team = Team.TeamB;
            agent.SetArena(this);
            if (agent.TryGetComponent<EnemyHealth>(out var agentHealth)) agentHealth.isTrainingDummy = true;
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

        if (opponent != null && characterDatabase != null && characterDatabase.CharacterCount > 0
            && opponent.TryGetComponent<HeroManager>(out var oppHeroManager))
        {
            int count = characterDatabase.CharacterCount;
            int index = heroIndex >= 0 ? (heroIndex + 1) % count : Random.Range(0, count);
            oppHeroManager.LoadHero(characterDatabase.GetHeroAtIndex(index));
        }

        StartCoroutine(ValidateAfterSetup());
    }

    private void OnDestroy()
    {
        if (_spawnedAgent && agent != null) Destroy(agent.gameObject);
        if (_spawnedOpponent && opponent != null) Destroy(opponent.gameObject);
    }

    /// <summary>Called by the agent at the start of every episode. Resets the opponent and returns the agent's spawn pose.</summary>
    public void ResetEpisode(out Vector3 agentPosition, out Quaternion agentRotation)
    {
        Vector3 spot = RandomPoint(spawnMargin);
        if (opponent != null)
        {
            if (opponent.TryGetComponent<EnemyHealth>(out var oppHealth))
            {
                oppHealth.ResetHealth();
            }
            opponent.transform.position = new Vector3(spot.x, transform.position.y, spot.z);
        }

        Vector3 anchor = opponent != null ? opponent.transform.position : transform.position;
        float distance = Random.Range(spawnDistanceRange.x, spawnDistanceRange.y);
        Vector3 offset = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * distance;
        Vector3 desired = ClampToBounds(anchor + offset);
        desired.y = transform.position.y;

        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            desired = hit.position;

        Vector3 face = anchor - desired;
        face.y = 0f;
        agentPosition = desired;
        agentRotation = face.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(face)
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