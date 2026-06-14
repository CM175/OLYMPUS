using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class AIMovementController : MonoBehaviour, IMovementController
{
    private NavMeshAgent _agent;
    private float _originalAcceleration;
    private Coroutine _impulseCoroutine;
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
            
            _originalAcceleration = _agent.acceleration;
            _baseSpeed = _agent.speed;
        }
    }
    public void ApplyImpulseForce(Vector3 force)
    {
        // If an impulse is already acting on this character, override it cleanly
        if (_impulseCoroutine != null) 
            StopCoroutine(_impulseCoroutine);
        
        _impulseCoroutine = StartCoroutine(ImpulseRoutine(force));
    }

    private IEnumerator ImpulseRoutine(Vector3 initialForce)
    {

        // 1. DYNAMICALLY SPLIT THE FORCE
        // Isolate the horizontal plane (X and Z) for actual NavMesh movement
        Vector3 horizontalForce = new Vector3(initialForce.x, 0f, initialForce.z);
        float horizontalMagnitude = horizontalForce.magnitude;
        Vector3 horizontalDirection = horizontalForce.normalized;

        // Isolate the vertical component (Y) for visual height
        float verticalInitialVelocity = initialForce.y;

        // 2. DYNAMICALLY CALCULATE DURATION BASED ON FORCE
        // No hardcoded timers. More force = longer slide/hangtime, using a standard drag simulation.
        float drag = 5f; 
        float duration = horizontalMagnitude / drag;
        // Clamp duration so small impulses don't snap instantly, and massive ones don't slide forever
        duration = Mathf.Clamp(duration, 0.15f, 0.7f); 

        float timer = 0f;
        _agent.acceleration = 10000f; // Max out acceleration so response is instant

        while (timer < duration)
        {
            float t = timer / duration;

            // --- DECAYING HORIZONTAL VELOCITY ---
            // Uses an ease-out curve (Fast start, aggressive deceleration friction)
            float speedDecay = Mathf.Lerp(horizontalMagnitude, 0f, 1f - (1f - t) * (1f - t));
            _agent.velocity = horizontalDirection * speedDecay;

            timer += Time.deltaTime;
            yield return null;
        }

        // 3. CLEAN UP AND RESET
        _agent.velocity = Vector3.zero;
        _agent.acceleration = _originalAcceleration;

        _impulseCoroutine = null;
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