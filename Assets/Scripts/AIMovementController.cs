using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AIMovementController : MonoBehaviour, IMovementController
{
    private NavMeshAgent _agent;
    private EnemyAI _enemyAI;

    public bool IsDashing { get; set; } = false;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _enemyAI = GetComponent<EnemyAI>();
    }

    public void ApplyImpulseForce(Vector3 force)
    {
        // NavMeshAgents don't use conventional physics forces. 
        // We simulate an impulse using standard NavMeshAgent translation or an instantaneous push.
        if (_agent.enabled && _agent.isOnNavMesh)
        {
            _agent.Move(force * Time.deltaTime);
        }
    }

    public void TeleportOrMove(Vector3 displacement)
    {
        if (_agent.enabled && _agent.isOnNavMesh)
        {
            _agent.Move(displacement);
        }
        else
        {
            // Fall back to direct transform displacement if the agent has been
            // pushed off the NavMesh (e.g. mid-charge/slam airborne movement).
            transform.position += displacement;
        }
    }

    public void HaltVelocity()
    {
        if (_agent.enabled && _agent.isOnNavMesh && !_agent.isStopped)
        {
            _agent.velocity = Vector3.zero;
        }
    }
}