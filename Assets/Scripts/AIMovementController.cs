using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AIMovementController : MonoBehaviour, IMovementController
{
    private NavMeshAgent _agent;
    private EnemyAI _enemyAI;
    private float _baseSpeed;

    // Accumulated impulse applied over frames and decayed naturally
    private Vector3 _impulseVelocity = Vector3.zero;
    private const float ImpulseDrag = 5f;

    public bool IsDashing { get; set; } = false;

    // Delegates stun state ownership entirely to EnemyAI to avoid duplicate coroutines
    public bool IsStunned => _enemyAI != null && _enemyAI.IsStunned;

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

    private void Update()
    {
        if (_impulseVelocity.sqrMagnitude > 0.01f)
        {
            // Temporarily take manual control of position so the NavMeshAgent
            // does not override the impulse displacement this frame
            _agent.updatePosition = false;
            _agent.Move(_impulseVelocity * Time.deltaTime);

            // Decay the impulse each frame
            _impulseVelocity = Vector3.Lerp(_impulseVelocity, Vector3.zero, ImpulseDrag * Time.deltaTime);

            // Resync the agent's internal position so it does not snap back
            _agent.nextPosition = transform.position;
        }
        else
        {
            _impulseVelocity = Vector3.zero;
            _agent.updatePosition = true;
        }
    }

    // Accumulates into _impulseVelocity so it is applied across multiple frames
    // rather than as a single tiny one-shot displacement
    public void ApplyImpulseForce(Vector3 force)
    {
        _impulseVelocity += force;
    }

    public void TeleportOrMove(Vector3 displacement)
    {
        if (_agent.enabled && _agent.isOnNavMesh)
        {
            // Clear the active path so the agent does not fight the displacement
            _agent.ResetPath();
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
            // ResetPath clears the destination so the agent stops steering;
            // setting velocity alone is overridden on the next agent update
            _agent.ResetPath();
            _agent.velocity = Vector3.zero;
            _impulseVelocity = Vector3.zero;
        }
    }

    public Vector3 GetAimDirection()
    {
        return transform.forward;
    }

    // Clears any in-flight impulse then hands stun ownership to EnemyAI.
    // Previously this ran its own StunDurationTracker coroutine alongside
    // EnemyAI.StunRoutine, causing two simultaneous stun timers.
    public void Stun(float duration)
    {
        _impulseVelocity = Vector3.zero;
        if (_enemyAI != null && _enemyAI.enabled)
        {
            _enemyAI.Stun(duration);
        }
    }
}