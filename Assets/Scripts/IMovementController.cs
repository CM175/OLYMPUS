using UnityEngine;

public interface IMovementController
{
    bool IsDashing { get; set; }
    void ApplyImpulseForce(Vector3 force);
    void TeleportOrMove(Vector3 displacement);
    void HaltVelocity();
}