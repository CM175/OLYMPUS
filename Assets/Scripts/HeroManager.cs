using UnityEngine;

public class HeroManager : MonoBehaviour
{
    [Header("Settings")]
    public Hero heroPrefab;
    public Camera playerCamera;

    [Header("State")]
    public Hero currentHero;

    /// <summary>
    /// Fired whenever a hero is fully loaded and wired up.
    /// HUDController (and any other interested system) subscribes to this
    /// instead of being searched for by HeroManager.
    /// </summary>
    public event System.Action<Hero> OnHeroLoaded;

    private AbilityManager _abilityManager;

    void Start()
    {
        _abilityManager = GetComponent<AbilityManager>();

        if (heroPrefab != null)
            LoadHero(heroPrefab);
    }

    public void LoadHero(Hero prefab)
    {
        if (currentHero != null && currentHero.gameObject.scene.IsValid())
            Destroy(currentHero.gameObject);

        currentHero = Instantiate(prefab, transform.position, transform.rotation);
        currentHero.transform.SetParent(transform);

        _abilityManager.primaryAbility   = currentHero.primaryAbility;
        _abilityManager.secondaryAbility = currentHero.secondaryAbility;
        _abilityManager.mobilityAbility  = currentHero.mobilityAbility;
        _abilityManager.ultimateAbility  = currentHero.ultimateAbility;

        GiveCameraToAbilities();
        InitializeUltimate();
        InitializePassive();

        // Notify PlayerHealth directly — it's on the same GameObject,
        // so a GetComponent is fine here (same object, not a scene search)
        GetComponent<PlayerHealth>()?.OnHeroLoaded(currentHero);

        // Notify all subscribers (HUDController, future systems) via event
        // — HeroManager has zero knowledge of who is listening
        OnHeroLoaded?.Invoke(currentHero);

        Debug.Log($"[HeroManager] Loaded hero: {currentHero.heroName}");
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void GiveCameraToAbilities()
    {
        InitializeAbility(_abilityManager.primaryAbility);
        InitializeAbility(_abilityManager.secondaryAbility);
        InitializeAbility(_abilityManager.mobilityAbility);
        InitializeAbility(_abilityManager.ultimateAbility);
    }

    /// <summary>
    /// Gives every ability its camera reference, and calls Initialize()
    /// on any ability that needs player root access (e.g. SkyCharge).
    /// </summary>
    private void InitializeAbility(Ability ability)
    {
        if (ability == null) return;

        ability.playerCamera = playerCamera;

        if (ability is IInitializable init)
            init.Initialize(gameObject);
    }

    private void InitializeUltimate()
    {
        if (_abilityManager.ultimateAbility == null) return;

        PlayerStats stats = GetComponent<PlayerStats>();
        if (stats == null)
        {
            Debug.LogError("[HeroManager] No PlayerStats found — ultimate will not function.");
            return;
        }

        _abilityManager.ultimateAbility.Initialize(stats);
    }

    private void InitializePassive()
    {
        if (currentHero.passiveAbility == null) return;

        if (currentHero.passiveAbility is IInitializable init)
            init.Initialize(gameObject);
        else
            currentHero.passiveAbility.Use();
    }
}