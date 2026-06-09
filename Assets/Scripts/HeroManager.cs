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

    private void Awake()
    {
        _abilityManager = GetComponent<AbilityManager>();
    }

    void Start()
    {
        if (heroPrefab != null)
            LoadHero(heroPrefab);
    }

    public void LoadHero(Hero prefab)
    {
        if (_abilityManager == null)
        {
            Debug.Log("Ability manager is null");
        }

        if (currentHero != null && currentHero.gameObject.scene.IsValid())
            Destroy(currentHero.gameObject);

        currentHero = Instantiate(prefab, transform.position, transform.rotation);
        currentHero.transform.SetParent(transform);

        _abilityManager.primaryAttack   = currentHero.primaryAttack;
        _abilityManager.primaryAbility = currentHero.primaryAbility;
        _abilityManager.secondaryAbility = currentHero.secondaryAbility;
        _abilityManager.mobilityAbility  = currentHero.mobilityAbility;
        _abilityManager.ultimateAbility  = currentHero.ultimateAbility;

        GiveCameraToAbilities();
        InitializeUltimate();
        InitializePassive();

        GetComponent<PlayerHealth>()?.OnHeroLoaded(currentHero);
        OnHeroLoaded?.Invoke(currentHero);

        // Turn on movement loops now that the layout is completely initialized
        if (TryGetComponent<PlayerController>(out var controller))
        {
            controller.enabled = true;
        }

        var mouseLook = GetComponentInChildren<MouseLook>();
        if (mouseLook != null)
        {
            mouseLook.enabled = true;
            Debug.Log("[HeroManager] MouseLook script successfully re-enabled.");
        }
        else
        {
            Debug.LogWarning("[HeroManager] Could not find a mouse look script on the player or its children.");
        }
        // ─────────────────────────────────────────────────────────────────────

        Debug.Log($"[HeroManager] Loaded hero: {currentHero.heroName}");
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void GiveCameraToAbilities()
    {
        InitializeAbility(_abilityManager.primaryAttack);
        
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
        
        Debug.Log("Init Passive");
        if (currentHero.passiveAbility == null) return;

        if (currentHero.passiveAbility is IInitializable init)
        {
            Debug.Log("Passive Initialized");
            init.Initialize(gameObject);
        }
        
        currentHero.passiveAbility.Use();
    }
}