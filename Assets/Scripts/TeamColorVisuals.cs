using UnityEngine;

[RequireComponent(typeof(TeamMember))]
public class TeamColorVisuals : MonoBehaviour
{
    [Header("Team Visual Configurations")]
    public Color teamAColor = new Color(0.2f, 0.6f, 1.0f, 1.0f); // Blue
    public Color teamBColor = new Color(1.0f, 0.2f, 0.2f, 1.0f); // Red

    [Header("Shader Property Overrides")]
    [Tooltip("Target property name in your shader (e.g., _Color, _BaseColor, or _OutlineColor)")]
    [SerializeField] private string colorPropertyName = "_BaseColor";
    [SerializeField] private string emissionPropertyName = "_EmissionColor";

    private MaterialPropertyBlock _propBlock;
    private TeamMember _teamMember;

    private void Awake()
    {
        _propBlock = new MaterialPropertyBlock();
        _teamMember = GetComponent<TeamMember>();
    }

    public void ApplyVisuals()
    {
        if (_teamMember == null) _teamMember = GetComponent<TeamMember>();

        Color chosenColor = _teamMember.team == Team.TeamA ? teamAColor : teamBColor;
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        int colorId = Shader.PropertyToID(colorPropertyName);
        int emissionId = Shader.PropertyToID(emissionPropertyName);

        foreach (var rend in renderers)
        {
            rend.GetPropertyBlock(_propBlock);

            // Apply base tint
            _propBlock.SetColor(colorId, chosenColor);

            // Apply aura / glow if emission is supported
            if (rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(emissionPropertyName))
            {
                _propBlock.SetColor(emissionId, chosenColor * 1.5f); // 1.5x multiplier for HDR glow
            }

            rend.SetPropertyBlock(_propBlock);
        }
    }
}