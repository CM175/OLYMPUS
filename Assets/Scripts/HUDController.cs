using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDController : MonoBehaviour
{
    [Header("References")]
    public PlayerHealth playerHealth;
    public PlayerStats playerStats;
    public AbilityManager abilityManager;
    public HeroManager heroManager;

    [Header("Health")]
    public Image healthBarFill;
    public TextMeshProUGUI healthText;

    [Header("Hero Info")]
    public TextMeshProUGUI heroNameText;
    public TextMeshProUGUI heroRoleText;

    [Header("Ability HUD Elements")]
    // Assign these in the Inspector to GameObjects that have the AbilityHUD script
    
    public AbilityHUD primaryHUD;
    public AbilityHUD secondaryHUD;
    public AbilityHUD mobilityHUD;
    public AbilityHUD ultimateHUD;

    [Header("Ult Ready")]
    public GameObject ultReadyIndicator;

    private void OnEnable()
    {
        if (heroManager != null)
            heroManager.OnHeroLoaded += OnHeroLoaded;
    }

    private void OnDisable()
    {
        if (heroManager != null)
            heroManager.OnHeroLoaded -= OnHeroLoaded;
    }

    private void OnHeroLoaded(Hero hero)
    {
        if (heroNameText != null) heroNameText.text = hero.heroName;
        if (heroRoleText != null) heroRoleText.text = hero.role;
        SetupAbilityIcons();
    }

    private void Update()
    {
        UpdateHealth();
        UpdateUltCharge();
        UpdateCooldowns();
    }

    private void UpdateHealth()
    {
        if (playerHealth == null || playerHealth.maxHealth == 0) return;

        float fraction = (float)playerHealth.currentHealth / playerHealth.maxHealth;
        healthBarFill.fillAmount = fraction;
        
        if (healthText != null)
            healthText.text = $"{playerHealth.currentHealth} / {playerHealth.maxHealth}";
    }

    private void UpdateUltCharge()
    {
        if (playerStats == null || ultimateHUD == null) return;

        // Use the new UpdateCharge method for the Ultimate specifically
        ultimateHUD.UpdateCharge(playerStats.currentUltimateCharge, playerStats.maxUltimateCharge);

        bool isReady = playerStats.currentUltimateCharge >= playerStats.maxUltimateCharge;
        
        // Visual feedback for being ready (like the yellow glow mentioned in your script)
        ultimateHUD.icon.color = isReady ? Color.yellow : Color.gray;

        if (ultReadyIndicator != null)
            ultReadyIndicator.SetActive(isReady);
    }

    private void UpdateCooldowns()
    {
        if (abilityManager == null) return;

        // Use the AbilityHUD's internal logic to update overlays and fills
        
        if (primaryHUD != null) primaryHUD.UpdateStatus(abilityManager.primaryAbility);
        if (secondaryHUD != null) secondaryHUD.UpdateStatus(abilityManager.secondaryAbility);
        if (mobilityHUD != null) mobilityHUD.UpdateStatus(abilityManager.mobilityAbility);
        if (ultimateHUD != null) ultimateHUD.UpdateStatus(abilityManager.ultimateAbility);
    }

    private void SetupAbilityIcons()
    {
        if (abilityManager == null) return;

        // Helper function to handle the logic cleanly for all slots
        SetupSingleHUD(primaryHUD, abilityManager.primaryAbility);
        SetupSingleHUD(secondaryHUD, abilityManager.secondaryAbility);
        SetupSingleHUD(mobilityHUD, abilityManager.mobilityAbility);
        SetupSingleHUD(ultimateHUD, abilityManager.ultimateAbility);
    }

    private void SetupSingleHUD(AbilityHUD hud, Ability ability)
    {
        if (hud == null) return;

        if (ability != null)
        {
            Debug.Log("ability successfully shown");
            hud.gameObject.SetActive(true); // Show the UI container
            hud.icon.sprite = ability.sprite;
        }
        else
        {
            Debug.Log("ability Failed");
            hud.gameObject.SetActive(false); // Hide the entire UI element if ability is null
        }
    }
}