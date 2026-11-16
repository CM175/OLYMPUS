#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class OlympusVFXGenerator
{
    private const string RootFolder = "Assets/GeneratedVFX";

    [MenuItem("Olympus/Generate All Ability VFX")]
    public static void GenerateAll()
    {
        EnsureFolder(RootFolder);
        Texture2D glowTex = CreateSoftGlowTexture();
        Material particleMat = CreateParticleMaterial(glowTex);

        // --- ZEUS (Electric Cyan & Gold) ---
        Color zeusCore = new Color(0.3f, 0.85f, 1f) * 2.5f;
        Color zeusAccent = new Color(1f, 0.9f, 0.3f) * 2.0f;
        CreateDashVFX("Zeus", "VFX_Zeus_SkyCharge", particleMat, zeusCore, zeusAccent, 0.4f);
        CreateZoneVFX("Zeus", "VFX_Zeus_OlympianWrathZone", particleMat, zeusCore, zeusAccent, 6.5f);

        // --- POSEIDON (Deep Ocean Teal & Seafoam) ---
        Color seaCore = new Color(0.05f, 0.65f, 0.85f) * 2.0f;
        Color seaFoam = new Color(0.5f, 1f, 0.95f) * 2.2f;
        CreateSlashVFX("Poseidon", "VFX_Poseidon_TridentThrust", particleMat, seaCore, seaFoam, 2.2f, 0f);
        CreateImpactVFX("Poseidon", "VFX_Poseidon_TridentImpact", particleMat, seaCore, seaFoam, 1.5f);
        CreateDashVFX("Poseidon", "VFX_Poseidon_UndertowSurge", particleMat, seaCore, seaFoam, 2.0f);
        CreateImpactVFX("Poseidon", "VFX_Poseidon_WallSplash", particleMat, seaFoam, seaCore, 3.0f);
        CreateZoneVFX("Poseidon", "VFX_Poseidon_KrakenZone", particleMat, seaCore, seaFoam, 2.7f);

        // --- MINOTAUR (Molten Crimson & Earth Amber) ---
        Color minoFire = new Color(1f, 0.25f, 0.05f) * 2.5f;
        Color minoDust = new Color(0.85f, 0.55f, 0.25f) * 1.5f;
        CreateSlashVFX("Minotaur", "VFX_Minotaur_AxeSwingLeft", particleMat, minoFire, minoDust, 3.5f, 25f);
        CreateSlashVFX("Minotaur", "VFX_Minotaur_AxeSwingRight", particleMat, minoFire, minoDust, 3.5f, -25f);
        CreateDashVFX("Minotaur", "VFX_Minotaur_Charge", particleMat, minoFire, minoDust, 2.0f);
        CreateShockwaveVFX("Minotaur", "VFX_Minotaur_EarthshakerStomp", particleMat, minoFire, minoDust, 12f, 0.6f);

        // --- HERMES (Wind Emerald & Swift Gold) ---
        Color hermesWind = new Color(0.2f, 1f, 0.65f) * 2.2f;
        Color hermesGold = new Color(1f, 0.85f, 0.25f) * 2.2f;
        CreateImpactVFX("Hermes", "VFX_Hermes_DaggerImpact", particleMat, hermesWind, hermesGold, 1.0f);
        CreateDashVFX("Hermes", "VFX_Hermes_BlinkDash", particleMat, hermesWind, hermesGold, 0.35f);
        CreateDashVFX("Hermes", "VFX_Hermes_SonicBoomTrail", particleMat, hermesGold, hermesWind, 1.2f);
        CreateShockwaveVFX("Hermes", "VFX_Hermes_SonicBoomImpact", particleMat, hermesGold, hermesWind, 5f, 0.4f);

        // --- HADES (Underworld Violet & Soul Green) ---
        Color hadesViolet = new Color(0.55f, 0.15f, 1f) * 2.5f;
        Color hadesSoul = new Color(0.2f, 1f, 0.55f) * 2.2f;
        CreateImpactVFX("Hades", "VFX_Hades_BidentImpact", particleMat, hadesViolet, hadesSoul, 1.4f);
        CreateZoneVFX("Hades", "VFX_Hades_HellFireZone", particleMat, hadesViolet, hadesSoul, 4.5f);
        CreateShockwaveVFX("Hades", "VFX_Hades_ShadowSink", particleMat, hadesViolet, Color.black, 3f, 0.45f);
        CreateShockwaveVFX("Hades", "VFX_Hades_ShadowEmerge", particleMat, hadesViolet, hadesSoul, 9f, 0.55f);

        // --- ARTEMIS (Moonlight Silver & Celestial Blue) ---
        Color moonCyan = new Color(0.45f, 0.85f, 1f) * 2.3f;
        Color moonSilver = new Color(0.9f, 0.95f, 1f) * 2.0f;
        CreateImpactVFX("Artemis", "VFX_Artemis_ArrowImpact", particleMat, moonCyan, moonSilver, 1.2f);
        CreateShockwaveVFX("Artemis", "VFX_Artemis_TrapTrigger", particleMat, moonCyan, moonSilver, 4f, 0.4f);
        CreateDashVFX("Artemis", "VFX_Artemis_HuntersLeap", particleMat, moonSilver, moonCyan, 0.6f);

        // --- APOLLO (Radiant Sun Gold & Warm White) ---
        Color sunGold = new Color(1f, 0.72f, 0.1f) * 2.8f;
        Color sunWhite = new Color(1f, 0.96f, 0.75f) * 2.5f;
        CreateImpactVFX("Apollo", "VFX_Apollo_SolarArrowImpact", particleMat, sunGold, sunWhite, 1.4f);
        CreateShockwaveVFX("Apollo", "VFX_Apollo_AscensionLeap", particleMat, sunGold, sunWhite, 4.5f, 0.6f);
        CreateShockwaveVFX("Apollo", "VFX_Apollo_HymnOfRadiance", particleMat, sunGold, sunWhite, 16f, 0.7f);
        CreateShockwaveVFX("Apollo", "VFX_Apollo_SolarBlind", particleMat, sunWhite, sunGold, 10f, 0.35f);
        CreateZoneVFX("Apollo", "VFX_Apollo_ChariotSolarPillar", particleMat, sunGold, sunWhite, 0.4f);

        // --- ACHILLES (Spartan Bronze & Ares Crimson) ---
        Color spartanGold = new Color(1f, 0.65f, 0.15f) * 2.4f;
        Color aresRed = new Color(1f, 0.15f, 0.15f) * 2.6f;
        CreateSlashVFX("Achilles", "VFX_Achilles_SpearThrust", particleMat, spartanGold, aresRed, 2.5f, 0f);
        CreateImpactVFX("Achilles", "VFX_Achilles_SpearImpact", particleMat, spartanGold, aresRed, 1.5f);
        CreateSlashVFX("Achilles", "VFX_Achilles_ShieldBash", particleMat, spartanGold, aresRed, 4.0f, 0f);
        CreateAuraVFX("Achilles", "VFX_Achilles_BattleCry", particleMat, aresRed, spartanGold, 5.0f);
        CreateAuraVFX("Achilles", "VFX_Achilles_Fury", particleMat, aresRed, spartanGold, 6.0f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=green>[OlympusVFXGenerator]</color> Successfully generated all hero VFX prefabs in Assets/GeneratedVFX!");
    }

    // 1. IMPACT / BURST VFX
    private static void CreateImpactVFX(string hero, string name, Material mat, Color primary, Color secondary, float scale)
    {
        GameObject root = new GameObject(name);
        var auto = root.AddComponent<AutoDestroyVFX>();
        auto.lifetime = 0.8f;

        var ps = root.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f * scale, 8f * scale);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f * scale, 0.4f * scale);
        main.startColor = new ParticleSystem.MinMaxGradient(primary, secondary);
        main.gravityModifier = 0.4f;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f * scale;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));

        SetupRenderer(ps, mat);
        SavePrefab(hero, name, root);
    }

    // 2. EXPANDING SHOCKWAVE / AOE NOVA VFX
    private static void CreateShockwaveVFX(string hero, string name, Material mat, Color primary, Color secondary, float targetDiameter, float duration)
    {
        GameObject root = new GameObject(name);
        var auto = root.AddComponent<AutoDestroyVFX>();
        auto.lifetime = duration;

        // Expanding Ring Mesh
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "ShockwaveRing";
        ring.transform.SetParent(root.transform);
        ring.transform.localPosition = Vector3.up * 0.1f;
        Object.DestroyImmediate(ring.GetComponent<Collider>());

        var ringAuto = ring.AddComponent<AutoDestroyVFX>();
        ringAuto.lifetime = duration;
        ringAuto.expandOverTime = true;
        ringAuto.startScale = new Vector3(0.5f, 0.05f, 0.5f);
        ringAuto.endScale = new Vector3(targetDiameter, 0.02f, targetDiameter);
        ring.GetComponent<Renderer>().sharedMaterial = CreateAdditiveMeshMat(primary);

        // Radial Sparks
        var ps = root.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.startLifetime = duration * 0.9f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(targetDiameter * 0.6f, targetDiameter * 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startColor = new ParticleSystem.MinMaxGradient(primary, secondary);

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 35) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.rotation = new Vector3(90f, 0f, 0f);
        shape.radius = 0.2f;

        SetupRenderer(ps, mat);
        SavePrefab(hero, name, root);
    }

    // 3. GROUND ZONE VFX (Normalized to 1m diameter so `localScale = radius * 2` scales accurately!)
    private static void CreateZoneVFX(string hero, string name, Material mat, Color primary, Color secondary, float duration)
    {
        GameObject root = new GameObject(name);
        var auto = root.AddComponent<AutoDestroyVFX>();
        auto.lifetime = duration + 0.5f;
        auto.fadeMaterialAlpha = false;

        // Normalized 1m diameter ground disc (radius = 0.5m)
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "ZoneBoundaryDisc";
        disc.transform.SetParent(root.transform);
        disc.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        disc.transform.localScale = new Vector3(1f, 0.01f, 1f);
        Object.DestroyImmediate(disc.GetComponent<Collider>());
        Color zoneTint = primary;
        zoneTint.a = 0.28f;
        disc.GetComponent<Renderer>().sharedMaterial = CreateAdditiveMeshMat(zoneTint);

        // Rising ambient particles inside radius 0.5
        var ps = root.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = duration;
        main.loop = true;
        main.startLifetime = 0.8f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(primary, secondary);
        main.gravityModifier = -0.2f; // Float upward

        var emission = ps.emission;
        emission.rateOverTime = 30f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.rotation = new Vector3(90f, 0f, 0f);
        shape.radius = 0.48f; // Matches 1m diameter when scaled by radius * 2

        SetupRenderer(ps, mat);
        SavePrefab(hero, name, root);
    }

    // 4. MELEE SLASH / THRUST CONE VFX
    private static void CreateSlashVFX(string hero, string name, Material mat, Color primary, Color secondary, float width, float rollAngle)
    {
        GameObject root = new GameObject(name);
        root.transform.rotation = Quaternion.Euler(0f, 0f, rollAngle);
        var auto = root.AddComponent<AutoDestroyVFX>();
        auto.lifetime = 0.35f;

        var ps = root.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.startLifetime = 0.22f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startColor = new ParticleSystem.MinMaxGradient(primary, secondary);

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 25) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = width * 0.25f;

        var trails = ps.trails;
        trails.enabled = true;
        trails.lifetime = 0.15f;
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));

        SetupRenderer(ps, mat, true);
        SavePrefab(hero, name, root);
    }

    // 5. DASH / SPEED TRAIL VFX
    private static void CreateDashVFX(string hero, string name, Material mat, Color primary, Color secondary, float duration)
    {
        GameObject root = new GameObject(name);
        var auto = root.AddComponent<AutoDestroyVFX>();
        auto.lifetime = duration + 0.6f;

        var ps = root.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = duration;
        main.loop = false;
        main.startLifetime = 0.35f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startColor = new ParticleSystem.MinMaxGradient(primary, secondary);

        var emission = ps.emission;
        emission.rateOverTime = 60f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 20f;
        shape.radius = 0.5f;

        var trails = ps.trails;
        trails.enabled = true;
        trails.lifetime = 0.25f;

        SetupRenderer(ps, mat, true);
        SavePrefab(hero, name, root);
    }

    // 6. SELF-BUFF AURA VFX
    private static void CreateAuraVFX(string hero, string name, Material mat, Color primary, Color secondary, float duration)
    {
        GameObject root = new GameObject(name);
        var auto = root.AddComponent<AutoDestroyVFX>();
        auto.lifetime = duration;

        var ps = root.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = duration;
        main.loop = true;
        main.startLifetime = 0.7f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(primary, secondary);

        var emission = ps.emission;
        emission.rateOverTime = 25f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        shape.radius = 0.8f;

        SetupRenderer(ps, mat);
        SavePrefab(hero, name, root);
    }

    // --- ASSET & MATERIAL HELPERS ---
    private static void SetupRenderer(ParticleSystem ps, Material mat, bool withTrails = false)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        if (withTrails) r.trailMaterial = mat;
    }

    private static Texture2D CreateSoftGlowTexture()
    {
        string path = $"{RootFolder}/SoftGlow.png";
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                float alpha = Mathf.Clamp01(1f - dist);
                alpha = Mathf.Pow(alpha, 2.2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material CreateParticleMaterial(Texture2D tex)
    {
        string path = $"{RootFolder}/Mat_ProceduralParticle.mat";
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Particles/Standard Unlit");
        Material mat = new Material(shader);
        mat.mainTexture = tex;
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f);     // Additive
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static Material CreateAdditiveMeshMat(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent");
        Material mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f);
        string safeName = $"Mat_Mesh_{Mathf.Abs(color.GetHashCode())}.mat";
        string path = $"{RootFolder}/{safeName}";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) == null)
            AssetDatabase.CreateAsset(mat, path);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    private static void SavePrefab(string hero, string name, GameObject root)
    {
        string folder = $"{RootFolder}/{hero}";
        EnsureFolder(folder);
        string path = $"{folder}/{name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        string folderName = Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, folderName);
    }
}
#endif