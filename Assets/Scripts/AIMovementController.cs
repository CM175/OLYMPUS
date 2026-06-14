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
    
    private Vector3 _velocity;
    
    public float gravity = -9.81f;
    
    private bool _isGrounded;


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
    private void Update()
    {
        HandleGroundCheck();
        ExecuteMovement();
    }

    public void ExecuteMovement()
    {

        _velocity.y += gravity * Time.deltaTime;

        _agent.Move(_velocity * Time.deltaTime);

    }

    public void ApplyImpulseForce(Vector3 force)
    {
        _agent.velocity += force;
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

    bool IsAgentGrounded()
    {
        NavMeshHit hit;
        float maxDistance = 0.5f; // Threshold to define "grounded"
        
        if (NavMesh.SamplePosition(_agent.transform.position, out hit, maxDistance, NavMesh.AllAreas))
        {
            // Check if the hit position is reasonably close to the agent's Y level
            return Mathf.Abs(_agent.transform.position.y - hit.position.y) <= 0.1f;
        }
        return false;
    }
    private void HandleGroundCheck()
    {
        bool wasGrounded = _isGrounded; // Store the state from last frame
        _isGrounded = IsAgentGrounded();

        if (_isGrounded)
        {
            // If we just landed this frame...
            if (!wasGrounded)
            {
                OnLand();
            }

            if (_agent.velocity.y < 0f)
            {
                Vector3 vel = _agent.velocity;
                vel.y = -2f; 
                _agent.velocity =vel; 
            }
        }
    }

    private void OnLand()
    {
        // Zero out horizontal velocity (X and Z) so the leap momentum stops
        Vector3 vel = _agent.velocity;
        vel.x = 0f;
        vel.z = 0f;
        _agent.velocity = vel;
        
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