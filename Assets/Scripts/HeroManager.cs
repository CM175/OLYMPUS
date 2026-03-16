using UnityEngine;

public class HeroManager : MonoBehaviour
{
    [Header("Settings")]
    public Hero heroPrefab; // Drag your Zeus prefab here
    public Camera playerCamera;
    
    [Header("State")]
    public Hero currentHero;

    private AbilityManager abilityManager;

    void Start()
    {
        abilityManager = GetComponent<AbilityManager>();
        
        if (heroPrefab != null)
        {
            LoadHero(heroPrefab);
        }
    }

    public void LoadHero(Hero prefab)
    {
        // 1. Cleanup old hero if one exists
        if (currentHero != null)
        {
            Destroy(currentHero.gameObject);
        }

        // 2. Instantiate the hero prefab into the scene
        // We use the prefab's Hero component to create the instance
        currentHero = Instantiate(prefab, transform.position, transform.rotation);
        
        // 3. Parent it to this object so it moves with the player
        currentHero.transform.SetParent(this.transform);

        // 4. Assign abilities from the LIVE instance to the AbilityManager
        // This ensures Coroutines run on an active object in the scene
        abilityManager.primaryAbility = currentHero.primaryAbility;
        abilityManager.secondaryAbility = currentHero.secondaryAbility;
        abilityManager.mobilityAbility = currentHero.mobilityAbility;
        abilityManager.ultimateAbility = currentHero.ultimateAbility;

        GiveCameraToAbilities();
        
        Debug.Log($"Loaded Hero: {currentHero.heroName}");
    }

    void GiveCameraToAbilities()
    {
        // Assign the player camera to the instantiated ability components
        if (abilityManager.primaryAbility != null)
            abilityManager.primaryAbility.playerCamera = playerCamera;

        if (abilityManager.secondaryAbility != null)
            abilityManager.secondaryAbility.playerCamera = playerCamera;

        if (abilityManager.mobilityAbility != null)
            abilityManager.mobilityAbility.playerCamera = playerCamera;

        if (abilityManager.ultimateAbility != null)
            abilityManager.ultimateAbility.playerCamera = playerCamera;
    }
}