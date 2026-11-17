using UnityEngine;

/// <summary>
/// Code-generated particle / line / trail effects for abilities, so no prefabs or editor work are needed.
/// Everything is built at runtime from one shared material and a procedurally generated soft-circle texture.
///
/// Effects are skipped entirely while training (TrainingArena sets <see cref="Suppressed"/>) and in
/// batch mode, so they cost nothing for RL runs.
/// </summary>
public static class AbilityVfx
{
    /// <summary>When true every call below returns immediately. Set by TrainingArena.</summary>
    public static bool Suppressed;

    private static bool Off => Suppressed || Application.isBatchMode;

    // ------------------------------------------------------------------ palette
    public static readonly Color Gold    = new Color(1.00f, 0.82f, 0.30f);
    public static readonly Color Crimson = new Color(0.95f, 0.20f, 0.15f);
    public static readonly Color Azure   = new Color(0.30f, 0.70f, 1.00f);
    public static readonly Color Teal    = new Color(0.20f, 0.90f, 0.80f);
    public static readonly Color Violet  = new Color(0.65f, 0.30f, 1.00f);
    public static readonly Color Silver  = new Color(0.75f, 0.85f, 1.00f);
    public static readonly Color Cyan    = new Color(0.40f, 1.00f, 1.00f);
    public static readonly Color Storm   = new Color(0.80f, 0.90f, 1.00f);
    public static readonly Color Dust    = new Color(0.60f, 0.55f, 0.45f);

    // ------------------------------------------------------------------ shared resources
    private static Material _mat;
    private static Texture2D _tex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Suppressed = false;
        _mat = null;
        _tex = null;
    }

    private static Texture2D SoftTexture
    {
        get
        {
            if (_tex != null) return _tex;
            const int n = 64;
            _tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            Vector2 c = new Vector2(n * 0.5f, n * 0.5f);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / (n * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            }
            _tex.SetPixels32(px);
            _tex.Apply(false, true);
            return _tex;
        }
    }

    private static Material Mat
    {
        get
        {
            if (_mat != null) return _mat;

            Shader sh = Shader.Find("Legacy Shaders/Particles/Additive");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");

            _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            _mat.mainTexture = SoftTexture;
            if (_mat.HasProperty("_TintColor")) _mat.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            return _mat;
        }
    }

    // ------------------------------------------------------------------ particle plumbing
    private static ParticleSystem Create(string name, Vector3 pos, Quaternion rot, Transform parent)
    {
        var go = new GameObject("VFX_" + name);
        go.SetActive(false);
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
        }
        else
        {
            go.transform.SetPositionAndRotation(pos, rot);
        }

        var ps = go.AddComponent<ParticleSystem>();
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    private static void Setup(ParticleSystem ps, float duration, bool loop, float lifeMin, float lifeMax,
                              float speedMin, float speedMax, float sizeMin, float sizeMax,
                              Color color, float gravity, int maxParticles, bool worldSpace = true)
    {
        var main = ps.main;
        main.duration = Mathf.Max(0.05f, duration);
        main.loop = loop;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.5f));
        main.gravityModifier = gravity;
        main.maxParticles = maxParticles;
        main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        main.stopAction = loop ? ParticleSystemStopAction.None : ParticleSystemStopAction.Destroy;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
    }

    private static void BurstOnce(ParticleSystem ps, int count)
    {
        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 1, short.MaxValue)) });
    }

    private static void Launch(ParticleSystem ps) => ps.gameObject.SetActive(true);

    // ------------------------------------------------------------------ one-shot effects
    public static void Burst(Vector3 pos, Color color, float size = 1f, int count = 24, float gravity = 0f)
    {
        if (Off) return;
        var ps = Create("Burst", pos, Quaternion.identity, null);
        Setup(ps, 0.1f, false, 0.35f, 0.7f, 2f * size, 5f * size, 0.15f * size, 0.35f * size, color, gravity, count + 8);
        BurstOnce(ps, count);
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f * size;
        Launch(ps);
    }

    public static void Impact(Vector3 pos, Color color, float scale = 1f)
    {
        if (Off) return;
        Burst(pos, color, 0.6f * scale, 14);
        Flash(pos, color, 1.1f * scale);
    }

    public static void Flash(Vector3 pos, Color color, float size = 1f)
    {
        if (Off) return;
        var ps = Create("Flash", pos, Quaternion.identity, null);
        Setup(ps, 0.1f, false, 0.12f, 0.16f, 0f, 0f, size, size, color, 0f, 4);
        BurstOnce(ps, 1);
        Launch(ps);
    }

    public static void Ring(Vector3 pos, Color color, float radius, float duration = 0.45f)
    {
        if (Off) return;
        var ps = Create("Ring", pos, Quaternion.Euler(-90f, 0f, 0f), null);
        float speed = radius / Mathf.Max(0.05f, duration);
        Setup(ps, 0.1f, false, duration, duration, speed, speed, 0.3f, 0.45f, color, 0f, 96);
        BurstOnce(ps, 64);
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.1f; sh.radiusThickness = 0f;
        Launch(ps);
    }

    public static void Cone(Vector3 pos, Vector3 forward, Color color, float range, float halfAngleDeg = 45f)
    {
        if (Off || forward.sqrMagnitude < 0.0001f) return;
        var ps = Create("Cone", pos, Quaternion.LookRotation(forward), null);
        float life = 0.35f;
        float speed = range / life;
        Setup(ps, 0.1f, false, life * 0.8f, life, speed * 0.6f, speed, 0.2f, 0.4f, color, 0f, 64);
        BurstOnce(ps, 40);
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = Mathf.Clamp(halfAngleDeg, 1f, 89f); sh.radius = 0.1f;
        Launch(ps);
    }

    public static void Muzzle(Vector3 pos, Vector3 dir, Color color, float scale = 1f)
    {
        if (Off || dir.sqrMagnitude < 0.0001f) return;
        var ps = Create("Muzzle", pos, Quaternion.LookRotation(dir), null);
        Setup(ps, 0.1f, false, 0.15f, 0.25f, 4f * scale, 8f * scale, 0.1f * scale, 0.2f * scale, color, 0f, 24);
        BurstOnce(ps, 10);
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 18f; sh.radius = 0.05f;
        Launch(ps);
        Flash(pos, color, 0.6f * scale);
    }

    // ------------------------------------------------------------------ sustained effects
    public static ParticleSystem Aura(Transform parent, Color color, float duration, float radius = 0.7f)
    {
        if (Off || parent == null) return null;
        var ps = Create("Aura", Vector3.up, Quaternion.identity, parent);
        Setup(ps, duration, false, 0.6f, 1.0f, 0f, 0f, 0.15f, 0.3f, color, 0f, 120);
        var em = ps.emission; em.enabled = true; em.rateOverTime = 30f;
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = radius;
        RiseUp(ps, 1.5f);
        Launch(ps);
        return ps;
    }

    public static ParticleSystem Glow(Transform parent, Vector3 localPos, Color color, float radius = 0.6f)
    {
        if (Off || parent == null) return null;
        var ps = Create("Glow", localPos, Quaternion.identity, parent);
        Setup(ps, 1f, true, 0.8f, 1.2f, 0f, 0f, 0.2f, 0.4f, color, 0f, 60);
        var em = ps.emission; em.enabled = true; em.rateOverTime = 12f;
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = radius;
        RiseUp(ps, 0.6f);
        Launch(ps);
        return ps;
    }

    private static void RiseUp(ParticleSystem ps, float speed)
    {
        var v = ps.velocityOverLifetime;
        v.enabled = true;
        v.space = ParticleSystemSimulationSpace.World;
        v.x = new ParticleSystem.MinMaxCurve(0f);
        v.y = new ParticleSystem.MinMaxCurve(speed);
        v.z = new ParticleSystem.MinMaxCurve(0f);
    }

    public static void Trail(GameObject target, Color color, float width = 0.25f, float time = 0.3f)
    {
        if (Off || target == null) return;
        var tr = target.AddComponent<TrailRenderer>();
        tr.sharedMaterial = Mat;
        tr.time = time;
        tr.minVertexDistance = 0.1f;
        tr.widthMultiplier = width;
        tr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        tr.numCapVertices = 2;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.Lerp(color, Color.white, 0.4f), 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = g;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;
    }

    public static void Bolt(Vector3 from, Vector3 to, Color color, float width = 0.3f, float duration = 0.12f, float jitter = 0.5f)
    {
        if (Off) return;
        var go = new GameObject("VFX_Bolt");
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = Mat;
        lr.useWorldSpace = true;
        lr.textureMode = LineTextureMode.Stretch;
        lr.widthMultiplier = width;
        lr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.2f);
        lr.numCapVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        lr.colorGradient = g;

        go.AddComponent<VfxArc>().Init(lr, from, to, jitter, duration);
    }

    private sealed class VfxArc : MonoBehaviour
    {
        private LineRenderer _lr;
        private Vector3 _a, _b;
        private float _jitter, _endTime;
        private int _segments;

        public void Init(LineRenderer lr, Vector3 a, Vector3 b, float jitter, float duration)
        {
            _lr = lr; _a = a; _b = b; _jitter = jitter;
            _endTime = Time.time + Mathf.Max(0.02f, duration);
            _segments = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(a, b) * 0.6f), 6, 24);
            _lr.positionCount = _segments;
            Redraw();
        }

        private void Update()
        {
            if (Time.time >= _endTime) { Destroy(gameObject); return; }
            Redraw();
        }

        private void Redraw()
        {
            _lr.SetPosition(0, _a);
            for (int i = 1; i < _segments - 1; i++)
            {
                Vector3 p = Vector3.Lerp(_a, _b, i / (float)(_segments - 1));
                _lr.SetPosition(i, p + Random.insideUnitSphere * _jitter);
            }
            _lr.SetPosition(_segments - 1, _b);
        }
    }
}
