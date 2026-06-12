using UnityEngine;

public interface IMovementController
{
    bool IsDashing { get; set; }
    bool IsStunned { get; }
    float SpeedMultiplier { get; set; }

    void ApplyImpulseForce(Vector3 force);
    void TeleportOrMove(Vector3 displacement);
    void HaltVelocity();
    
    Vector3 GetAimDirection();
    void Stun(float duration);
}