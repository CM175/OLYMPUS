using UnityEngine;

public class HeroManager : MonoBehaviour
{
    [Header("Settings")]
    public Hero heroPrefab;
    public Camera playerCamera; // Can be null for AI Bots

    [Header("State")]
    public Hero currentHero;
    public bool isAIControlled = false;

    public event System.Action<Hero> OnHeroLoaded;

    private AbilityManager _abilityManager;

    private void Awake()
    {
        _abilityManager = GetComponent<AbilityManager>();
        
        // Auto-detect if this root object is meant to be an AI or human
        if (GetComponent<EnemyAI>() != null || GetComponent<PlayerController>() == null)
        {
            isAIControlled = true;
        }
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
            Debug.LogError($"[HeroManager] AbilityManager missing on {gameObject.name}");
            return;
        }

        if (currentHero != null && currentHero.gameObject.scene.IsValid())
            Destroy(currentHero.gameObject);

        currentHero = Instantiate(prefab, transform.position, transform.rotation);
        currentHero.transform.SetParent(transform);

        // Map Hero prefab abilities to the runtime AbilityManager
        _abilityManager.primaryAttack    = currentHero.primaryAttack;
        _abilityManager.primaryAbility   = currentHero.primaryAbility;
        _abilityManager.secondaryAbility = currentHero.secondaryAbility;
        _abilityManager.mobilityAbility  = currentHero.mobilityAbility;
        _abilityManager.ultimateAbility  = currentHero.ultimateAbility;

        GiveCameraToAbilities();
        InitializeUltimate();
        InitializePassive();

        // Support health tracking dynamically
        GetComponent<PlayerHealth>()?.OnHeroLoaded(currentHero);
        GetComponent<EnemyHealth>()?.OnHeroLoaded(currentHero); // Add hook if your AI uses a separate script

        OnHeroLoaded?.Invoke(currentHero);

        if (TryGetComponent<TeamColorVisuals>(out var teamVisuals))
        {
            teamVisuals.ApplyVisuals();
        }
        // Human-only initialization
        if (!isAIControlled)
        {
            if (TryGetComponent<PlayerController>(out var controller))
            {
                controller.enabled = true;
            }

            var mouseLook = GetComponentInChildren<MouseLook>();
            if (mouseLook != null)
            {
                mouseLook.enabled = true;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            // Enable AI loops once hero layout is completely set up
            if (TryGetComponent<EnemyAI>(out var ai))
            {
                ai.enabled = true;
            }
        }

        Debug.Log($"[HeroManager] Loaded hero: {currentHero.heroName} (AI: {isAIControlled})");
    }

    private void GiveCameraToAbilities()
    {
        // For human players, provide the camera; for AIs, it safely remains null
        InitializeAbility(_abilityManager.primaryAttack);
        InitializeAbility(_abilityManager.primaryAbility);
        InitializeAbility(_abilityManager.secondaryAbility);
        InitializeAbility(_abilityManager.mobilityAbility);
        InitializeAbility(_abilityManager.ultimateAbility);
    }

    private void InitializeAbility(Ability ability)
    {
        if (ability == null) return;

        ability.playerCamera = playerCamera; //
        
        if (TryGetComponent<IMovementController>(out var controller))
        {
            ability.SetMovementController(controller);
        }

        // ADDED HERE: Provide the ability with its sibling manager context link
        if (TryGetComponent<AbilityManager>(out var manager))
        {
            ability.SetAbilityManager(manager);
        }

        if (ability is IInitializable init) //
            init.Initialize(gameObject); //
    }

    private void InitializeUltimate()
    {
        if (_abilityManager.ultimateAbility == null) return;

        PlayerStats stats = GetComponent<PlayerStats>();
        if (stats == null) return; // AIs can utilize a simplified stats block or bypass

        _abilityManager.ultimateAbility.Initialize(stats);
    }

    private void InitializePassive()
    {
        if (currentHero.passiveAbility == null) return;

        if (currentHero.passiveAbility is IInitializable init)
        {
            init.Initialize(gameObject);
        }
        
        currentHero.passiveAbility.Use();
    }
}