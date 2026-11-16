using UnityEngine;

public class AutoDestroyVFX : MonoBehaviour
{
    public float lifetime = 1.5f;
    public bool expandOverTime = false;
    public Vector3 startScale = Vector3.one;
    public Vector3 endScale = Vector3.one;
    public bool fadeMaterialAlpha = true;

    private float _elapsed;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _propBlock;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        _propBlock = new MaterialPropertyBlock();
        if (expandOverTime) transform.localScale = startScale;
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, lifetime));

        if (expandOverTime)
        {
            transform.localScale = Vector3.Lerp(startScale, endScale, 1f - Mathf.Pow(1f - t, 3f)); // Ease-out
        }

        if (fadeMaterialAlpha && _renderers != null)
        {
            float alpha = 1f - t;
            foreach (var r in _renderers)
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                r.GetPropertyBlock(_propBlock);
                Color c = r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColorId)
                    ? r.sharedMaterial.GetColor(BaseColorId)
                    : Color.white;
                c.a *= alpha;
                _propBlock.SetColor(BaseColorId, c);
                _propBlock.SetColor(ColorId, c);
                r.SetPropertyBlock(_propBlock);
            }
        }

        if (_elapsed >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}