using UnityEngine;

public class HuntersLeap : Ability, IInitializable
{
    public float leapForce = 10f;
    public float leapHeight = 0.5f;
    private IMovementController _movementController;
    private Transform _rootTransform;

    public void Initialize(GameObject playerRoot)
    {
        
        _rootTransform = playerRoot.transform;
        _movementController = playerRoot.GetComponent<IMovementController>();
    }

    protected override void Activate()
    {
        if (_movementController != null)
        {
            // Calculate direction using character root transform forward 
            // completely bypassing the camera view calculations!
            Vector3 leapDirection = -_rootTransform.forward;
            leapDirection.y = leapHeight; 
            leapDirection.Normalize();

            _movementController.ApplyImpulseForce(leapDirection * leapForce);
        }

        StartCooldown();
    }
}