using UnityEngine;

public class HeroManager : MonoBehaviour
{
    public Hero currentHero;
    public Camera playerCamera;

    AbilityManager abilityManager;

    void Start()
    {
        abilityManager = GetComponent<AbilityManager>();
        LoadHero(currentHero);
    }

    public void LoadHero(Hero hero)
    {
        currentHero = hero;

        abilityManager.primaryAbility = hero.primaryAbility;
        abilityManager.secondaryAbility = hero.secondaryAbility;
        abilityManager.mobilityAbility = hero.mobilityAbility;
        abilityManager.ultimateAbility = hero.ultimateAbility;

        GiveCameraToAbilities();
    }

    void GiveCameraToAbilities()
    {
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