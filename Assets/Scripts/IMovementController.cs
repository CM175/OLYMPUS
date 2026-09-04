using UnityEngine;

public interface IMovementController
{
    bool IsDashing { get; set; }
    bool IsStunned { get; }
    float SpeedMultiplier { get; set; }
    bool IsGrounded { get; } 
    bool IsUnderExternalForce { get; set; }

    void ApplyImpulseForce(Vector3 force);
    void Teleport(Vector3 position);
    void Move(Vector3 displacement);
    void HaltVelocity();
    void HaltVerticalVelocity(); // Add this
    void SetGravityMultiplier(float value); // Add this
    void ApplySlow(float slowMultiplier, float duration, bool decaying = false);
    Vector3 GetAimDirection();
    void Stun(float duration);
}