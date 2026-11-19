using UnityEngine;

[RequireComponent(typeof(TeamMember))]
public class TeamOutlineVisuals : MonoBehaviour
{
    [Header("Rendering Layer Bits (Match OverwatchOutlineFeature)")]
    [SerializeField] private uint teamAOutlineLayerBit = 1u << 1; // Layer 1 (2)
    [SerializeField] private uint teamBOutlineLayerBit = 1u << 2; // Layer 2 (4)

    private TeamMember _teamMember;
    private Renderer[] _renderers;
    private Team _lastAppliedTeam;
    private bool _hasApplied;

    private void Awake()
    {
        _teamMember = GetComponent<TeamMember>();
        RefreshRenderers();
    }

    private void OnEnable()
    {
        ApplyVisuals();
    }

    private void LateUpdate()
    {
        // Automatically re-apply if the team was assigned after Instantiate/OnEnable
        if (!_hasApplied || _teamMember.team != _lastAppliedTeam)
        {
            ApplyVisuals();
        }
    }

    private void OnDisable()
    {
        ClearOutline();
        _hasApplied = false;
    }

    public void RefreshRenderers()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
    }

    public void ApplyVisuals()
    {
        if (_teamMember == null) _teamMember = GetComponent<TeamMember>();
        if (_renderers == null || _renderers.Length == 0) RefreshRenderers();

        uint targetBit = _teamMember.team == Team.TeamA ? teamAOutlineLayerBit : teamBOutlineLayerBit;
        uint clearMask = ~(teamAOutlineLayerBit | teamBOutlineLayerBit);

        foreach (var rend in _renderers)
        {
            if (rend == null) continue;
            rend.renderingLayerMask = (rend.renderingLayerMask & clearMask) | targetBit;
        }

        _lastAppliedTeam = _teamMember.team;
        _hasApplied = true;
    }

    public void ClearOutline()
    {
        if (_renderers == null) return;
        uint clearMask = ~(teamAOutlineLayerBit | teamBOutlineLayerBit);

        foreach (var rend in _renderers)
        {
            if (rend == null) continue;
            rend.renderingLayerMask &= clearMask;
        }
    }
}