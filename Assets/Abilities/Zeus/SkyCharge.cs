using UnityEngine;

public class SkyCharge : Ability, IInitializable
{
    public float dashDistance = 8f;

    private CharacterController _controller;

    /// <summary>
    /// Called by HeroManager on hero load.
    /// Retrieves the CharacterController from the player root directly
    /// rather than searching the entire scene.
    /// </summary>
    public void Initialize(GameObject playerRoot)
    {
        _controller = playerRoot.GetComponent<CharacterController>();

        if (_controller == null)
            Debug.LogError("[SkyCharge] No CharacterController found on player root.");
    }

    protected override void Activate()
    {
        if (_controller == null)
        {
            Debug.LogError("[SkyCharge] CharacterController not initialised — was Initialize() called?");
            StartCooldown();
            return;
        }

        Vector3 origin  = _controller.transform.position;
        Vector3 forward = _controller.transform.forward;
        Vector3 target  = origin + forward * dashDistance;

        if (Physics.Raycast(origin, forward, out RaycastHit hit, dashDistance))
            target = hit.point - forward * 0.5f;

        _controller.enabled = false;
        _controller.transform.position = target;
        _controller.enabled = true;

        StartCooldown();
    }
}