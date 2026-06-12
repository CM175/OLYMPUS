using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AIMovementController : MonoBehaviour, IMovementController
{
    private NavMeshAgent _agent;
    private EnemyAI _enemyAI;
    private float _baseSpeed;

    public bool IsDashing { get; set; } = false;
        
    private bool _isStunned = false;
    public bool IsStunned => _isStunned;


    // Track speed changes dynamically on the NavMeshAgent
    private float _speedMultiplier = 1f;
    public float SpeedMultiplier
    {
        get => _speedMultiplier;
        set
        {
            _speedMultiplier = value;
            if (_agent != null)
            {
                _agent.speed = _baseSpeed * _speedMultiplier;
            }
        }
    }

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _enemyAI = GetComponent<EnemyAI>();
        if (_agent != null)
        {
            _baseSpeed = _agent.speed;
        }
    }

    public void ApplyImpulseForce(Vector3 force)
    {
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
            transform.position += displacement;
        }
    }

    public void HaltVelocity()
    {
        if (_agent.enabled && _agent.isOnNavMesh)
        {
            _agent.velocity = Vector3.zero;
        }
    }

    public Vector3 GetAimDirection()
    {
        // An AI's forward gaze vector represents its aiming direction context cleanly
        return transform.forward;
    }

    // Add this backing field and property to your AIMovementController class
    public void Stun(float duration)
    {
        if (_enemyAI != null && _enemyAI.enabled)
        {
            StartCoroutine(StunDurationTracker(duration));
            _enemyAI.Stun(duration);
        }
    }

    private System.Collections.IEnumerator StunDurationTracker(float duration)
    {
        _isStunned = true;
        yield return new WaitForSeconds(duration);
        _isStunned = false;
    }
}