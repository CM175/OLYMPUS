using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class CapturePointVisuals : MonoBehaviour
{
    [SerializeField] private CapturePoint point;
    [SerializeField] private BoxCollider boxCollider;
    [SerializeField] private float floorYOffset = 0.05f;

    [Header("Colors")]
    [SerializeField] private Color neutralColor = new(1f, 1f, 1f, 0.4f);
    [SerializeField] private Color teamAColor = new(0.2f, 0.6f, 1f, 0.8f);
    [SerializeField] private Color teamBColor = new(0.9f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color contestedColor = new(1f, 0.85f, 0.1f, 1f);

    private LineRenderer line;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 4;

        if (boxCollider == null && point != null)
        {
            boxCollider = point.GetComponent<BoxCollider>();
        }

        UpdateBoxPerimeter();
    }

    public void UpdateBoxPerimeter()
    {
        if (boxCollider == null || line == null) return;

        Vector3 center = boxCollider.center;
        Vector3 size = boxCollider.size;

        float halfX = size.x * 0.5f;
        float halfZ = size.z * 0.5f;
        // Project corners to the base of the collider plus an offset to prevent z-fighting
        float baseY = center.y - (size.y * 0.5f) + floorYOffset;

        line.SetPosition(0, new Vector3(center.x - halfX, baseY, center.z - halfZ));
        line.SetPosition(1, new Vector3(center.x - halfX, baseY, center.z + halfZ));
        line.SetPosition(2, new Vector3(center.x + halfX, baseY, center.z + halfZ));
        line.SetPosition(3, new Vector3(center.x + halfX, baseY, center.z - halfZ));
    }

    private void Update()
    {
        if (point == null) return;

        Color targetColor = point.isContested
            ? contestedColor
            : point.controllingTeam switch
            {
                PointOwner.TeamA => teamAColor,
                PointOwner.TeamB => teamBColor,
                _ => neutralColor
            };

        line.startColor = targetColor;
        line.endColor = targetColor;
    }

    private void OnValidate()
    {
        if (boxCollider == null && point != null)
        {
            boxCollider = point.GetComponent<BoxCollider>();
        }

        if (line == null) line = GetComponent<LineRenderer>();
        if (line != null && boxCollider != null)
        {
            line.positionCount = 4;
            line.loop = true;
            UpdateBoxPerimeter();
        }
    }
}