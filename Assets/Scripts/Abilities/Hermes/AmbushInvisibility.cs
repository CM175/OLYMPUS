using System.Collections;
using UnityEngine;

public class AmbushInvisibility : Ability, IInitializable
{
    [Header("Vanish Settings")]
    [SerializeField] private float duration = 4.0f;
    [SerializeField] private float speedMultiplier = 1.25f;
    [SerializeField] private float damageRevealDuration = 1.0f;

    private IMovementController _movementController;
    private float _originalSpeedMultiplier = 1f;
    private bool _isInvisible = false;

    public void Initialize(GameObject playerRoot)
    {
        _movementController = playerRoot.GetComponent<IMovementController>();
    }

    protected override void Activate()
    {
        StartCoroutine(VanishRoutine());
    }

    private IEnumerator VanishRoutine()
    {
        _isInvisible = true;
        SetRenderersVisible(false);

        if (_movementController != null)
        {
            _originalSpeedMultiplier = _movementController.SpeedMultiplier;
            _movementController.SpeedMultiplier = _originalSpeedMultiplier * speedMultiplier;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            // If taking damage flags a temporary reveal, handle it
            yield return null;
            elapsed += Time.deltaTime;
        }

        EndVanish();
        StartCooldown();
    }

    public void NotifyDamageTaken()
    {
        if (!_isInvisible) return;
        
        // Briefly flicker visible when taking damage without breaking stealth entirely
        StartCoroutine(TemporaryRevealRoutine(damageRevealDuration));
    }

    private IEnumerator TemporaryRevealRoutine(float revealTime)
    {
        SetRenderersVisible(true);
        yield return new WaitForSeconds(revealTime);
        if (_isInvisible)
        {
            SetRenderersVisible(false);
        }
    }

    public void BreakStealth()
    {
        if (!_isInvisible) return;
        StopAllCoroutines();
        EndVanish();
    }

    private void EndVanish()
    {
        _isInvisible = false;
        SetRenderersVisible(true);

        if (_movementController != null)
        {
            _movementController.SpeedMultiplier = _originalSpeedMultiplier;
        }
    }

    private void SetRenderersVisible(bool isVisible)
    {
        MeshRenderer[] renderers = transform.root.GetComponentsInChildren<MeshRenderer>();
        foreach (var r in renderers)
        {
            // Avoid hiding UI/VFX elements if nested; target character renderers specifically
            if (r.gameObject != gameObject) 
            {
                r.enabled = isVisible;
            }
        }
    }
}