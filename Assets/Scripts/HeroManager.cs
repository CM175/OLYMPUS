using UnityEngine;

public class HeroManager : MonoBehaviour
{
    [Header("Settings")]
    public Hero heroPrefab;
    public Camera playerCamera; 

    [Header("State")]
    public Hero currentHero;
    public bool isAIControlled = false;

    public event System.Action<Hero> OnHeroLoaded;

    private AbilityManager _abilityManager;

    private void Awake()
    {
        _abilityManager = GetComponent<AbilityManager>();
        
        if (GetComponent<RLEnemyCombatAgent>() != null || GetComponent<PlayerController>() == null)
        {
            isAIControlled = true;
        }

        // Guarantee PlayerStats exists so Ultimates function properly on both AI and Players
        if (GetComponent<PlayerStats>() == null)
        {
            gameObject.AddComponent<PlayerStats>();
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

        _abilityManager.primaryAttack    = currentHero.primaryAttack;
        _abilityManager.primaryAbility   = currentHero.primaryAbility;
        _abilityManager.secondaryAbility = currentHero.secondaryAbility;
        _abilityManager.mobilityAbility  = currentHero.mobilityAbility;
        _abilityManager.ultimateAbility  = currentHero.ultimateAbility;

        GiveCameraToAbilities();
        InitializeUltimate();
        InitializePassive();

        GetComponent<PlayerHealth>()?.OnHeroLoaded(currentHero);
        GetComponent<EnemyHealth>()?.OnHeroLoaded(currentHero);

        OnHeroLoaded?.Invoke(currentHero);

        if (TryGetComponent<TeamColorVisuals>(out var teamVisuals))
        {
            teamVisuals.ApplyVisuals();
        }

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
            if (TryGetComponent<RLEnemyCombatAgent>(out var ai))
            {
                ai.enabled = true;
            }
        }

        Debug.Log($"[HeroManager] Loaded hero: {currentHero.heroName} (AI: {isAIControlled})");
    }

    private void GiveCameraToAbilities()
    {
        InitializeAbility(_abilityManager.primaryAttack);
        InitializeAbility(_abilityManager.primaryAbility);
        InitializeAbility(_abilityManager.secondaryAbility);
        InitializeAbility(_abilityManager.mobilityAbility);
        InitializeAbility(_abilityManager.ultimateAbility);
    }

    private void InitializeAbility(Ability ability)
    {
        if (ability == null) return;
        ability.playerCamera = playerCamera;

        if (TryGetComponent<IMovementController>(out var controller))
        {
            ability.SetMovementController(controller);
        }

        if (TryGetComponent<AbilityManager>(out var manager))
        {
            ability.SetAbilityManager(manager);
        }

        if (ability is IInitializable init)
        {
            init.Initialize(gameObject);
        }
    }

    private void InitializeUltimate()
    {
        if (_abilityManager.ultimateAbility == null) return;
        PlayerStats stats = GetComponent<PlayerStats>();
        if (stats != null)
        {
            _abilityManager.ultimateAbility.Initialize(stats);
        }
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