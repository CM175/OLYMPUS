using UnityEngine;
using System.Collections;

public class HuntersLeap : Ability, IInitializable
{
    public float leapForce = 20f;
    public float leapHeight = 12f;
    
    [SerializeField] private float gravityScale = 30f;
    private IMovementController _movementController;
    private Transform _rootTransform;
    private bool hasLeftGround = false;

    public void Initialize(GameObject playerRoot)
    {
        
        _rootTransform = playerRoot.transform;
        _movementController = playerRoot.GetComponent<IMovementController>();
    }

    protected override void Activate()
    {
        if (_movementController != null)
        {
            StartCoroutine(LeapRoutine());
            
        }

    }

    private IEnumerator LeapRoutine()
    {
        _movementController.HaltVelocity();
        _movementController.IsDashing = true;
        Vector3 leapDirection = -_rootTransform.forward;
        leapDirection.y = 0f;
        leapDirection.Normalize();

        Vector3 airVelocity = (leapDirection * leapForce) + (Vector3.up * leapHeight);
        hasLeftGround = false;

        while (isExecuting)
        {
            airVelocity.y -= gravityScale * Time.deltaTime;
            Vector3 frameMovement = airVelocity * Time.deltaTime;

            // Displace layout coordinates completely via the abstraction interface
            _movementController.Move(frameMovement);
            if (!_movementController.IsGrounded)
            {
                hasLeftGround = true;
            }
            // Ground check abstracted from explicit component type
            if (hasLeftGround && _movementController.IsGrounded)
            {
                break;
            }

            yield return null;
        }
        
        _movementController.HaltVelocity();
        _movementController.IsDashing = false;

        StartCooldown();
    }
}