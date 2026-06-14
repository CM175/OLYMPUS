using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AIMovementController : MonoBehaviour, IMovementController
{
    private NavMeshAgent _agent;
    private EnemyAI _enemyAI;
    private float _baseSpeed;
    public float horizontalDrag = 8f; // Decay rate for horizontal velocity when under external force
    private Vector3 _velocity; // Tracks internal physics forces (including vertical velocity)


    public bool IsDashing { get; set; } = false;
    public bool IsUnderExternalForce { get; private set; }
    
    private bool _isStunned = false;
    public bool IsStunned => _isStunned;

    public float gravity = -9.81f; // Adjusted to match your high-gravity gameplay feel

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

    public bool IsGrounded => IsAgentGrounded();

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
        if (IsStunned && _agent.enabled && !_agent.isStopped)
        {
            _agent.isStopped = true;
        }

        if (IsUnderExternalForce)
        {
            // 1. Handle Vertical Physics (Gravity)
            if (!IsAgentGrounded() || _velocity.y > 0f)
            {
                _velocity.y += gravity * Time.deltaTime;
            }

            // 2. Handle Horizontal Physics (Drag decay)
            Vector3 horizontalVelocity = new Vector3(_velocity.x, 0f, _velocity.z);
            horizontalVelocity = Vector3.Lerp(horizontalVelocity, Vector3.zero, horizontalDrag * Time.deltaTime);
            _velocity = new Vector3(horizontalVelocity.x, _velocity.y, horizontalVelocity.z);

            // 3. CRITICAL: Move via Transform to allow true 3D vertical flight!
            transform.position += _velocity * Time.deltaTime;

            // 4. Landing and Recovery Check
            // If moving downward and close enough to a valid NavMesh floor, hand control back
            if (_velocity.y <= 0f && IsAgentGrounded())
            {
                EndExternalForce();
            }
        }
    }

    public void ApplyImpulseForce(Vector3 force)
    {
        IsUnderExternalForce = true;

        // Sever NavMesh control over position updates so the transform can fly vertically
        _agent.updatePosition = false;
        _agent.updateRotation = false;
        if (_agent.enabled) _agent.isStopped = true;

        // Accumulate the force directly into our physics velocity vector
        _velocity += force;
    }

    private void EndExternalForce()
    {
        IsUnderExternalForce = false;
        _velocity = Vector3.zero;

        // Cleanly warp the agent back onto the closest valid spot on the mesh to prevent sinking
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
        {
            _agent.Warp(hit.position);
        }

        // Re-enable native NavMesh tracking safely
        _agent.updatePosition = true;
        _agent.updateRotation = true;
        
        if (!IsStunned && _agent.enabled)
        {
            _agent.isStopped = false;
        }
    }

    public void Move(Vector3 displacement)
    {
        // If we are overriding coordinates (like during a dash or pinned ride), use transform
        if (IsUnderExternalForce || IsDashing)
        {
            transform.position += displacement;
        }
        else if (_agent.enabled)
        {
            _agent.Move(displacement);
        }
    }

    public void Teleport(Vector3 position)
    {
        if (_agent.enabled)
        {
            _agent.Warp(position);
        }
        else
        {
            transform.position = position;
        }
    }

    public void HaltVelocity()
    {
        _velocity = Vector3.zero;
        if (_agent.enabled && _agent.isOnNavMesh)
        {
            _agent.ResetPath();
        }
    }

    private bool IsAgentGrounded()
    {
        // Sample a narrow cylinder right beneath the bot's feet
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 0.4f, NavMesh.AllAreas))
        {
            return Mathf.Abs(transform.position.y - hit.position.y) <= 0.2f;
        }
        return false;
    }

    public Vector3 GetAimDirection() => transform.forward;

    public void Stun(float duration)
    {
        if (_enemyAI != null && _enemyAI.enabled)
        {
            HaltVelocity(); // Stop any current movement
            StartCoroutine(StunDurationTracker(duration));
            _enemyAI.Stun(duration);
        }
    }

    private System.Collections.IEnumerator StunDurationTracker(float duration)
    {
        _isStunned = true;
        yield return new WaitForSeconds(duration);
        _isStunned = false;
        
        if (!_isStunned && !IsUnderExternalForce && _agent.enabled)
        {
            _agent.isStopped = false;
        }
    }
}