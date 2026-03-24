using UnityEngine;

public class HeroManager : MonoBehaviour
{
    [Header("Settings")]
    public Hero heroPrefab;
    public Camera playerCamera;

    [Header("State")]
    public Hero currentHero;

    private AbilityManager _abilityManager;

    void Start()
    {
        _abilityManager = GetComponent<AbilityManager>();

        if (heroPrefab != null)
            LoadHero(heroPrefab);
    }

    public void LoadHero(Hero prefab)
    {
        // 1. Destroy previous hero instance if one exists in the scene
        if (currentHero != null && currentHero.gameObject.scene.IsValid())
            Destroy(currentHero.gameObject);

        // 2. Instantiate and parent BEFORE any component reads the hierarchy.
        //    Awake() fires during Instantiate, but Start() fires next frame —
        //    setting the parent here guarantees Start() sees the correct tree.
        currentHero = Instantiate(prefab, transform.position, transform.rotation);
        currentHero.transform.SetParent(transform);

        // 3. Wire abilities from the live instance to AbilityManager
        _abilityManager.primaryAbility  = currentHero.primaryAbility;
        _abilityManager.secondaryAbility = currentHero.secondaryAbility;
        _abilityManager.mobilityAbility  = currentHero.mobilityAbility;
        _abilityManager.ultimateAbility  = currentHero.ultimateAbility;

        // 4. Give the player camera to every ability
        GiveCameraToAbilities();

        // 5. Initialize ultimate (sets PlayerStats.maxUltimateCharge)
        InitializeUltimate();

        // 6. Initialize passive (e.g. FrontalArmor sets DamageReduction)
        InitializePassive();

        // 7. Notify other systems
        PlayerHealth ph = GetComponent<PlayerHealth>();
        if (ph != null) ph.OnHeroLoaded(currentHero);

        HUDController hud = FindAnyObjectByType<HUDController>();
        if (hud != null) hud.OnHeroLoaded(currentHero);

        Debug.Log($"[HeroManager] Loaded hero: {currentHero.heroName}");
    }

    // ────────────────────────────────────────────────────────────────────────

    private void GiveCameraToAbilities()
    {
        if (_abilityManager.primaryAbility != null)
            _abilityManager.primaryAbility.playerCamera = playerCamera;

        if (_abilityManager.secondaryAbility != null)
            _abilityManager.secondaryAbility.playerCamera = playerCamera;

        if (_abilityManager.mobilityAbility != null)
            _abilityManager.mobilityAbility.playerCamera = playerCamera;

        if (_abilityManager.ultimateAbility != null)
            _abilityManager.ultimateAbility.playerCamera = playerCamera;
    }

    private void InitializeUltimate()
    {
        if (_abilityManager.ultimateAbility == null) return;

        PlayerStats stats = GetComponent<PlayerStats>();
        if (stats == null)
        {
            Debug.LogError("[HeroManager] No PlayerStats on Player — ultimate will not function.");
            return;
        }

        _abilityManager.ultimateAbility.Initialize(stats);
    }

    private void InitializePassive()
    {
        if (currentHero.passiveAbility == null) return;

        // FrontalArmor needs a reference to PlayerHealth
        FrontalArmor frontalArmor = currentHero.passiveAbility as FrontalArmor;
        if (frontalArmor != null)
        {
            PlayerHealth ph = GetComponent<PlayerHealth>();
            frontalArmor.Initialize(ph);
            return;
        }

        // Generic fallback for simpler passives
        currentHero.passiveAbility.Use();
    }
}