using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoonlightChase : Ultimate, IInitializable
{
    [Header("Hunt Parameters")]
    public float duration = 8f;
    public float speedBuffMultiplier = 1.3f; // +30% Movement Speed
    public float drawSpeedMultiplier = 2.5f; // 2.5x Faster bow draw

    [Header("Visual Indicators")]
    public GameObject huntScreenVfxPrefab;

    private IMovementController _movementController;
    private CrescentBow _bow;
    private float _originalSpeedMultiplier = 1f;
    private GameObject _activeScreenVfx;
    private List<Outline> _appliedOutlines = new List<Outline>();

    public void Initialize(GameObject playerRoot)
    {
        _movementController = playerRoot.GetComponent<IMovementController>();
        _bow = playerRoot.GetComponentInChildren<CrescentBow>();
    }

    protected override void Activate()
    {
        base.Activate();
        StartCoroutine(ChaseRoutine());
    }

    private IEnumerator ChaseRoutine()
    {
        // 1. Buff movement speed via controller interface
        if (_movementController != null)
        {
            _originalSpeedMultiplier = _movementController.SpeedMultiplier;
            _movementController.SpeedMultiplier = _originalSpeedMultiplier * speedBuffMultiplier;
        }

        // 2. Accelerate bow draw speed & enable piercing
        if (_bow != null)
        {
            _bow.SetUltimateModifications(true, drawSpeedMultiplier);
        }

        // 3. Screen overlay visuals
        if (huntScreenVfxPrefab != null && playerCamera != null)
        {
            _activeScreenVfx = Instantiate(huntScreenVfxPrefab, playerCamera.transform);
        }

        // 4. Reveal all enemy positions through geometry
        EnableTrueSight();

        yield return new WaitForSeconds(duration);

        EndUltimate();
    }

    private void EnableTrueSight()
    {
        _appliedOutlines.Clear();
        TeamMember myTeam = transform.root.GetComponent<TeamMember>();

        // Query TeamMember components across all living entities
        TeamMember[] allMembers = FindObjectsByType<TeamMember>(FindObjectsSortMode.None);
        foreach (var member in allMembers)
        {
            // Filter out self and friendly allies
            if (member.transform.root == transform.root) continue;
            if (myTeam != null && !myTeam.IsEnemy(member.gameObject)) continue;

        
            // Attach or toggle the Outline component on the enemy
            Outline outline = member.GetComponent<Outline>();
            if (outline == null)
            {
                outline = member.gameObject.AddComponent<Outline>();
            }
            outline.OutlineColor = new Color(0.7f, 0.9f, 1.0f, 1f); // Moonlight silver/cyan
            outline.OutlineWidth = 0.04f;
            outline.enabled = true;
            _appliedOutlines.Add(outline);
            
        }
    }

    private void EndUltimate()
    {
        if (_movementController != null)
        {
            _movementController.SpeedMultiplier = _originalSpeedMultiplier;
        }

        if (_bow != null)
        {
            _bow.SetUltimateModifications(false);
        }

        if (_activeScreenVfx != null)
        {
            Destroy(_activeScreenVfx);
            _activeScreenVfx = null;
        }

        // Disable true sight auras
        foreach (var outline in _appliedOutlines)
        {
            if (outline != null) outline.enabled = false;
        }
        _appliedOutlines.Clear();

        StartCooldown();
    }

    public override void Cancel()
    {
        base.Cancel();
        EndUltimate();
    }

    private void OnDisable()
    {
        EndUltimate();
    }
}