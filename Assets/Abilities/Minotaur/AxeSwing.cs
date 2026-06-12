using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;

public class AxeSwing : Attack, IInitializable
{
    [Header("Melee Combo Settings")]
    [Tooltip("The cone angle width for the axe slash (e.g., 90 degrees total front arc).")]
    [SerializeField] private float swingAngle = 90f;

    [Header("Combo Alternation Visuals")]
    [Tooltip("Optional: Left-to-Right swing visual prefab.")]
    [SerializeField] private GameObject vfxSwingLeftPrefab;
    [Tooltip("Optional: Right-to-Left swing visual prefab.")]
    [SerializeField] private GameObject vfxSwingRightPrefab;

    private bool _isLeftSwing = true;
    private Transform _rootTransform;

    public void Initialize(GameObject playerRoot)
    {
        _rootTransform = playerRoot.transform;
    }

    protected override void Activate()
    {
        if (playerCamera == null) return;

        // 1. Alternating swing logic for visuals
        GameObject chosenVFX = _isLeftSwing ? vfxSwingLeftPrefab : vfxSwingRightPrefab;
        if (chosenVFX == null) chosenVFX = vfxPrefab; // Fallback to base class prefab

        if (chosenVFX != null)
        {
            // Spawn the swing visual slightly in front of the camera view direction
            Vector3 spawnPos = playerCamera.transform.position + (playerCamera.transform.forward * 1.5f);
            Instantiate(chosenVFX, spawnPos, playerCamera.transform.rotation, _rootTransform);
        }

        // Toggle the combo flag for the next strike
        _isLeftSwing = !_isLeftSwing;

        // 2. Perform OverlapSphere to gather potential melee victims
        Collider[] potentialTargets = Physics.OverlapSphere(_rootTransform.position, range);

        float cosHalfAngle = Mathf.Cos(swingAngle * 0.5f * Mathf.Deg2Rad);
        Vector3 cameraForwardFlat = playerCamera.transform.forward;
        cameraForwardFlat.y = 0f;
        cameraForwardFlat.Normalize();

        foreach (var col in potentialTargets)
        {
            var myTeam = transform.root.GetComponent<TeamMember>();
                
            if (myTeam != null && !myTeam.IsEnemy(col.transform.root.gameObject))
            {
                continue;
            }

            // Melee Angle Verification Check (Using Dot Product matching the camera gaze)
            Vector3 toTargetFlat = col.transform.position - _rootTransform.position;
            toTargetFlat.y = 0f;
            float distance = toTargetFlat.magnitude;

            if (distance > 0.1f)
            {
                toTargetFlat.Normalize();
                // Check if target falls inside the horizontal forward cleave cone
                if (Vector3.Dot(cameraForwardFlat, toTargetFlat) < cosHalfAngle) continue;
            }

            // 3. Apply damage and safe NavMesh displacement callbacks
            IDamageable targetDamage = col.GetComponent<IDamageable>();
            if (targetDamage != null)
            {
                // Deal heavy base damage
                targetDamage.TakeDamage(damage);
            }
        }

        // 4. Log development hooks to the frame pipeline
        Debug.Log($"[MinotaurMelee] Cleaved arc direction! Next swing is left: {_isLeftSwing}");
    }

    // Draws the melee range and cleave angle visualizer inside the Unity scene pane editor
    private void OnDrawGizmosSelected()
    {
        Transform origin = transform.root;
        if (origin == null) origin = transform;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin.position, range);

        // Vector direction visualizations
        Vector3 forwardLook = playerCamera != null ? playerCamera.transform.forward : origin.forward;
        forwardLook.y = 0;
        forwardLook.Normalize();

        Vector3 leftBoundary = Quaternion.Euler(0, -swingAngle * 0.5f, 0) * forwardLook;
        Vector3 rightBoundary = Quaternion.Euler(0, swingAngle * 0.5f, 0) * forwardLook;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin.position, origin.position + leftBoundary * range);
        Gizmos.DrawLine(origin.position, origin.position + rightBoundary * range);
    }
}