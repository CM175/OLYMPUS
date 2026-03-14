using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    [Header("Detection")]
    public float detectionRange = 20f;
    public float loseRange = 25f;

    [Header("Patrol")]
    public float patrolWaitTime = 2f;
    public float patrolRadius = 10f;

    private NavMeshAgent _agent;
    private Transform _player;
    private EnemyAttack _attack;

    private enum State { Idle, Patrol, Chase }
    private State _state = State.Idle;

    private float _patrolWaitTimer;
    private bool _waitingAtPoint;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _attack = GetComponent<EnemyAttack>();
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            _player = playerObj.transform;

        _state = State.Patrol;
        SetNewPatrolPoint();
    }

    private void Update()
    {
        if (_player == null) return;

        float distToPlayer = Vector3.Distance(transform.position, _player.position);

        switch (_state)
        {
            case State.Idle:
            case State.Patrol:
                HandlePatrol();
                if (distToPlayer <= detectionRange)
                    _state = State.Chase;
                break;

            case State.Chase:
                HandleChase();
                if (distToPlayer > loseRange)
                {
                    _state = State.Patrol;
                    SetNewPatrolPoint();
                }
                break;
        }
    }

    // ── Patrol ───────────────────────────────────────────────────────

    private void HandlePatrol()
    {
        if (_agent.remainingDistance <= _agent.stoppingDistance)
        {
            if (!_waitingAtPoint)
            {
                _waitingAtPoint = true;
                _patrolWaitTimer = patrolWaitTime;
            }

            _patrolWaitTimer -= Time.deltaTime;

            if (_patrolWaitTimer <= 0f)
            {
                _waitingAtPoint = false;
                SetNewPatrolPoint();
            }
        }
    }

    private void SetNewPatrolPoint()
    {
        Vector3 randomDir = Random.insideUnitSphere * patrolRadius;
        randomDir += transform.position;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDir, out hit, patrolRadius, NavMesh.AllAreas))
            _agent.SetDestination(hit.position);
    }

    // ── Chase ────────────────────────────────────────────────────────

    private void HandleChase()
    {
        // Stop short of the player so EnemyAttack can take over at melee range
        float stopAt = _attack != null ? _attack.attackRange * 0.9f : 1.5f;
        _agent.stoppingDistance = stopAt;
        _agent.SetDestination(_player.position);
    }

    // ── Gizmos ───────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, loseRange);
    }
}