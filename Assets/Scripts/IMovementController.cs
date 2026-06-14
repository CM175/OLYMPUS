using UnityEngine;

public interface IMovementController
{
    bool IsDashing { get; set; }
    bool IsStunned { get; }
    float SpeedMultiplier { get; set; }
    bool IsGrounded { get; } 

    void ApplyImpulseForce(Vector3 force);
    void Teleport(Vector3 position);
    void Move(Vector3 displacement);
    void HaltVelocity();
    Vector3 GetAimDirection();
    void Stun(float duration);
}