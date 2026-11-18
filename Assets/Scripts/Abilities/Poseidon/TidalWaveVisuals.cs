using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visual layer for the Tidal Surge wave (Wuyang Guardian Wave look).
/// Add to the same GameObject as TidalWaveMeshGenerator, and delete the placeholder Cube child.
/// Renders the procedural wave mesh with the TidalWater shader, adds crest spray and ground
/// splash particles, fits the trigger collider to the mesh, and bursts spray when the wave
/// passes through a character. Does not touch gameplay logic.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TidalWaveMeshGenerator))]
public class TidalWaveVisuals : MonoBehaviour
{
    [Header("Shaders (auto-filled when the component is added)")]
    public Shader waterShader;
    public Shader sprayShader;

    [Header("Optional: your own water material (overrides waterShader)")]
    public Material waterMaterial;

    [Header("Debug")]
    [Tooltip("Renders the wave as a flat cyan unlit material to test the mesh and renderer.")]
    public bool debugSolidTest = false;

    [Header("Collider")]
    public bool fitColliderToMesh = true;

    [Header("Spray")]
    public Color sprayColor = new Color(0.90f, 0.98f, 1f, 0.9f);
    public Color splashColor = new Color(0.50f, 0.88f, 0.95f, 0.55f);
    [Min(0f)] public float crestSprayRate = 90f;
    [Min(0f)] public float groundSplashRate = 70f;
    [Min(0)] public int impactBurstCount = 30;

    private Material _waterMat, _sprayMat;
    private float _spawnTime;
    private ParticleSystem _impact;
    private readonly HashSet<Transform> _burstRoots = new();

    private void Reset()
    {
        waterShader = Shader.Find("Custom/TidalWaveWater");
        sprayShader = Shader.Find("Olympus/TidalSpray");
    }

    private void Awake()
    {
        _spawnTime = Time.time;
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();

        if (waterMaterial != null)
        {
            mr.sharedMaterial = waterMaterial;
        }
        else
        {
            Shader s = waterShader != null ? waterShader : Shader.Find("Custom/TidalWaveWater");
            if (s == null) { Debug.LogError("TidalWaveVisuals: TidalWaveWater shader not found.", this); }
            else
            {
                if (!s.isSupported)
                    Debug.LogError("TidalWaveVisuals: TidalWaveWater shader failed to compile. Check the Console for 'Shader error'.", this);
                _waterMat = new Material(s);
                mr.sharedMaterial = _waterMat;
            }
        }
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        if (debugSolidTest)
        {
            Shader solid = Shader.Find("Unlit/Color");
            if (solid != null)
            {
                _waterMat = new Material(solid);
                _waterMat.color = Color.cyan;
                mr.sharedMaterial = _waterMat;
            }
        }

        Shader sp = sprayShader != null ? sprayShader : Shader.Find("Olympus/TidalSpray");
        if (sp != null) _sprayMat = new Material(sp);
    }

    private void Start()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh == null)
            GetComponent<TidalWaveMeshGenerator>().BuildWaveMesh(); // safety net
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogError("TidalWaveVisuals: MeshFilter still has no mesh after rebuild. See the error above.", this);
            return;
        }
        Bounds b = mf.sharedMesh.bounds;
        MeshRenderer rend = GetComponent<MeshRenderer>();
        Material m = rend != null ? rend.sharedMaterial : null;
        Debug.Log($"TidalWaveVisuals: bounds {b.min}..{b.max} | renderer enabled={(rend != null && rend.enabled)} " +
                  $"| shader={(m != null ? m.shader.name : "none")} supported={(m != null && m.shader.isSupported)} " +
                  $"| layer={gameObject.layer} | pos={transform.position}", this);

        if (fitColliderToMesh)
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null) { box.center = b.center; box.size = b.size; }
        }

        if (_sprayMat == null) return;

        // Emitter boxes are (width, depth, thickness): the shape is rotated so it emits upward.
        MakeParticles("CrestSpray",
            new Vector3(0f, b.max.y * 0.9f, b.max.z - 0.4f),
            new Vector3(b.size.x * 0.85f, 0.6f, 0.1f),
            crestSprayRate, new Vector2(0.5f, 0.9f), new Vector2(1.5f, 3.5f),
            new Vector2(0.15f, 0.40f), 1.2f, sprayColor, false);

        MakeParticles("GroundSplash",
            new Vector3(0f, 0.1f, b.center.z),
            new Vector3(b.size.x * 0.9f, Mathf.Max(0.5f, b.size.z * 0.3f), 0.1f),
            groundSplashRate, new Vector2(0.6f, 1.0f), new Vector2(0.5f, 1.5f),
            new Vector2(0.40f, 0.90f), 0.3f, splashColor, false);

        _impact = MakeParticles("ImpactBurst",
            Vector3.zero, new Vector3(0.8f, 0.8f, 0.1f),
            0f, new Vector2(0.5f, 0.9f), new Vector2(3f, 6f),
            new Vector2(0.30f, 0.70f), 1.5f, sprayColor, true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_impact == null || impactBurstCount <= 0) return;

        Transform root = other.transform.root;
        if (root == transform.root) return;
        if (root.GetComponent<IMovementController>() == null) return;
        if (!_burstRoots.Add(root)) return;

        _impact.transform.position = other.bounds.ClosestPoint(transform.position);
        _impact.Emit(impactBurstCount);
    }

    private ParticleSystem MakeParticles(string objName, Vector3 localPos, Vector3 box,
        float rate, Vector2 life, Vector2 speed, Vector2 size, float gravity,
        Color color, bool burstOnly)
    {
        GameObject go = new GameObject(objName);
        go.SetActive(false); // configure before it starts playing
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // shape +Z -> world up

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = Color.white;
        main.gravityModifier = gravity;
        main.maxParticles = 300;

        var em = ps.emission;
        em.rateOverTime = burstOnly ? 0f : rate;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = box;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(color.a, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.6f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0.2f)));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = _sprayMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        go.SetActive(true);
        return ps;
    }

    private void OnDestroy()
    {
        if (Application.isPlaying && gameObject.scene.isLoaded)
            Debug.Log($"TidalWaveVisuals: wave destroyed after {Time.time - _spawnTime:F2}s at {transform.position}");
        if (_waterMat != null) Destroy(_waterMat);
        if (_sprayMat != null) Destroy(_sprayMat);
    }
}