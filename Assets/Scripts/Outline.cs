using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class Outline : MonoBehaviour
{
    public Color OutlineColor = Color.white;
    public float OutlineWidth = 0.02f;

    private Renderer _renderer;
    private Material _outlineMaterial;
    private List<Material> _materials = new List<Material>();

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        Shader shader = Shader.Find("Custom/SilhouetteOutline");
        if (shader != null)
        {
            _outlineMaterial = new Material(shader);
        }
    }

    private void OnEnable()
    {
        ApplyOutline();
    }

    private void OnDisable()
    {
        RemoveOutline();
    }

    public void ApplyOutline()
    {
        if (_renderer == null || _outlineMaterial == null) return;

        UpdateProperties();

        _materials.Clear();
        _materials.AddRange(_renderer.sharedMaterials);

        if (!_materials.Contains(_outlineMaterial))
        {
            _materials.Add(_outlineMaterial);
            _renderer.materials = _materials.ToArray();
        }
    }

    public void RemoveOutline()
    {
        if (_renderer == null || _outlineMaterial == null) return;

        _materials.Clear();
        _materials.AddRange(_renderer.sharedMaterials);

        if (_materials.Remove(_outlineMaterial))
        {
            _renderer.materials = _materials.ToArray();
        }
    }

    public void UpdateProperties()
    {
        if (_outlineMaterial == null) return;
        _outlineMaterial.SetColor("_OutlineColor", OutlineColor);
        _outlineMaterial.SetFloat("_OutlineWidth", OutlineWidth);
    }
}