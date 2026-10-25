using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AIMovementController : MonoBehaviour, IMovementController
{
    [Header("Physics & Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float horizontalDrag = 8f;
    [SerializeField] private LayerMask obstacleMask = ~0;

    [Header("NavMesh Recovery")]
    [SerializeField] private float navMeshRecoveryRadius = 1.0f;
    [SerializeField] private float groundCheckTolerance = 0.25f;
    [SerializeField] private float aimErrorAngle = 5f;

    private NavMeshAgent _agent;
    private RLEnemyCombatAgent _enemyAI;
    private float _baseSpeed;
    private Vector3 _velocity;
    private Coroutine _stunCoroutine;

    private bool _isDashing;
    private bool _isStunned;
    private bool _isUnderExternalForce;

    public bool IsDashing
    {
        get => _isDashing;
        set
        {
            if (_isDashing == value) return;
            _isDashing = value;

            if (!_isDashing)
            {
                SyncAgentToTransform();
            }

            EvaluateAgentControl();
        }
    }

    public bool IsUnderExternalForce
    {
        get => _isUnderExternalForce;
        set
        {
            if (_isUnderExternalForce == value) return;
            _isUnderExternalForce = value;

            if (_isUnderExternalForce)
            {
                _agent.enabled = false;
            }
            else
            {
                EndExternalForce();
            }
        }
    }

    public bool IsStunned => _isStunned;
    public bool IsGrounded => CheckGrounded();

    private float _speedMultiplier = 1f;
    
    private float _gravityMultiplier = 1f;
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
        _enemyAI = GetComponent<RLEnemyCombatAgent>();

        if (_agent != null)
        {
            _baseSpeed = _agent.speed;
        }
    }

    private Coroutine _slowCoroutine;
    private float _preSlowMultiplier = 1f;

    private void Update()
    {
        // Auto-detect if we walked off an edge during a dash or knockback
        if (!CheckGrounded() && !_agent.isOnNavMesh)
        {
            IsUnderExternalForce = true;
        }

        if (_isUnderExternalForce)
        {
            ApplyAirPhysics();
        }
    }

    public void ApplyImpulseForce(Vector3 force)
    {
        _velocity += force;
        IsUnderExternalForce = true;
    }

    public void HaltVelocity()
    {
        _velocity = Vector3.zero;

        if (IsGrounded)
        {
            SyncAgentToTransform();
        }

        if (_agent.enabled && _agent.isOnNavMesh)
        {
            _agent.ResetPath();
        }
    }

    public void Move(Vector3 displacement)
    {
        if (_isUnderExternalForce || _isDashing)
        {
            transform.position += displacement;
        }
        else if (_agent.enabled && _agent.isOnNavMesh)
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

    public void Stun(float duration)
    {
        if (_stunCoroutine != null)
        {
            StopCoroutine(_stunCoroutine);
        }

        _stunCoroutine = StartCoroutine(StunRoutine(duration));
    }

    public Vector3 GetAimDirection()
    {
        return transform.forward;
    }

    // --- Private Implementation ---

    private void ApplyAirPhysics()
    {
        // 1. Gravity
        if (!IsGrounded || _velocity.y > 0f)
        {
            _velocity.y += gravity * _gravityMultiplier * Time.deltaTime;
        }

        // 2. Horizontal Drag
        Vector3 horizontalVelocity = new Vector3(_velocity.x, 0f, _velocity.z);
        horizontalVelocity = Vector3.Lerp(horizontalVelocity, Vector3.zero, horizontalDrag * Time.deltaTime);
        _velocity = new Vector3(horizontalVelocity.x, _velocity.y, horizontalVelocity.z);

        Vector3 displacement = _velocity * Time.deltaTime;

        // --- GROUND / CEILING DETECTION ---
        if (_velocity.y < 0f)
        {
            float downDistance = Mathf.Abs(displacement.y) + groundCheckTolerance;
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit groundHit, downDistance + 0.5f, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                transform.position = new Vector3(transform.position.x + displacement.x, groundHit.point.y, transform.position.z + displacement.z);
                _velocity.y = 0f;
                IsUnderExternalForce = false;
                return;
            }
        }

        // --- HORIZONTAL OBSTACLE DETECTION ---
        if (displacement.sqrMagnitude > 0.0001f)
        {
            Vector3 origin = transform.position + (Vector3.up * 0.5f);
            if (Physics.SphereCast(origin, _agent.radius, displacement.normalized, out RaycastHit hit, displacement.magnitude, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                displacement = Vector3.ProjectOnPlane(displacement, hit.normal);
                _velocity = Vector3.ProjectOnPlane(_velocity, hit.normal);
            }
        }

        transform.position += displacement;

        // 3. Touchdown check
        if (_velocity.magnitude < 0.01f && _velocity.y <= 0f && IsGrounded)
        {
            IsUnderExternalForce = false;
        }
    }

    private void EndExternalForce()
    {
        _velocity = Vector3.zero;

        // Enable agent before warping so internal solver binds correctly
        _agent.enabled = true;
        SyncAgentToTransform();
        EvaluateAgentControl();
    }

    private void SyncAgentToTransform()
    {
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshRecoveryRadius, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            if (_agent.enabled)
            {
                _agent.Warp(hit.position);
                _agent.nextPosition = hit.position;
            }
        }
        else if (_agent.enabled)
        {
            _agent.Warp(transform.position);
            _agent.nextPosition = transform.position;
        }
    }

    private void EvaluateAgentControl()
    {
        if (!_agent.enabled) return;

        bool overrideTransform = _isDashing || _isUnderExternalForce;

        _agent.updatePosition = !overrideTransform;
        //_agent.updateRotation = !overrideTransform;

        if (overrideTransform)
        {
            if (_agent.isOnNavMesh)
            {
                _agent.ResetPath();
                _agent.isStopped = true;
            }
        }
        else if (_isStunned)
        {
            if (_agent.isOnNavMesh) _agent.isStopped = true;
        }
        else
        {
            if (_agent.isOnNavMesh) _agent.isStopped = false;
        }
    }

   private IEnumerator StunRoutine(float duration)
    {
        _isStunned = true;
        HaltVelocity();

        if (TryGetComponent<AbilityManager>(out var abilityManager))
        {
            abilityManager.InterruptAllAbilities();
        }

        EvaluateAgentControl();

        yield return new WaitForSeconds(duration);

        _isStunned = false;

        EvaluateAgentControl();
    }

    private bool CheckGrounded()
    {
        // 1. Raycast downward against the ground/geometry layer
        if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, groundCheckTolerance + 0.3f, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        // 2. NavMesh ground check fallback
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, 0.4f, NavMesh.AllAreas))
        {
            return Mathf.Abs(transform.position.y - navHit.position.y) <= groundCheckTolerance;
        }

        return false;
    }

    public void SetGravityMultiplier(float value)
    {
        _gravityMultiplier = value;
    }
    public void HaltVerticalVelocity()
    {
        _velocity.y = 0f;
    }

    public void ApplySlow(float slowMultiplier, float duration, bool decaying = false)
    {
        if (_slowCoroutine != null)
        {
            StopCoroutine(_slowCoroutine);
            SpeedMultiplier = _preSlowMultiplier;
        }

        _slowCoroutine = StartCoroutine(SlowRoutine(slowMultiplier, duration, decaying));
    }

    private IEnumerator SlowRoutine(float slowMultiplier, float duration, bool decaying)
    {
        _preSlowMultiplier = SpeedMultiplier;
        float targetSpeed = _preSlowMultiplier * slowMultiplier;
        SpeedMultiplier = targetSpeed;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (decaying)
            {
                SpeedMultiplier = Mathf.Lerp(targetSpeed, _preSlowMultiplier, elapsed / duration);
            }
            yield return null;
        }

        SpeedMultiplier = _preSlowMultiplier;
        _slowCoroutine = null;
    }

}