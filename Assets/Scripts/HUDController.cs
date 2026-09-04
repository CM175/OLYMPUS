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
    public AbilityHUD primaryHUD;
    public AbilityHUD secondaryHUD;
    public AbilityHUD mobilityHUD;
    public AbilityHUD ultimateHUD;

    [Header("Ult Ready")]
    public GameObject ultReadyIndicator;

    [Header("Death & Respawn Overlay")]
    public GameObject deathScreenPanel;
    public TextMeshProUGUI respawnTimerText;

    public void Initialize(GameObject player)
    {
        // Unsubscribe old HeroManager if present
        if (heroManager != null)
            heroManager.OnHeroLoaded -= OnHeroLoaded;

        playerHealth = player.GetComponent<PlayerHealth>();
        abilityManager = player.GetComponent<AbilityManager>();
        playerStats = player.GetComponent<PlayerStats>();
        heroManager = player.GetComponent<HeroManager>();

        if (heroManager != null)
            heroManager.OnHeroLoaded += OnHeroLoaded;

        HideDeathScreen();
    }

    public void ShowDeathScreen()
    {
        // Force health display to zero upon death
        if (healthBarFill != null) healthBarFill.fillAmount = 0f;
        if (healthText != null && playerHealth != null)
            healthText.text = $"0 / {playerHealth.maxHealth}";

        if (deathScreenPanel != null)
            deathScreenPanel.SetActive(true);
    }

    public void HideDeathScreen()
    {
        if (deathScreenPanel != null)
            deathScreenPanel.SetActive(false);
    }

    public void UpdateRespawnTimer(float remainingTime)
    {
        if (respawnTimerText != null)
            respawnTimerText.text = $"RESPAWNING IN {Mathf.CeilToInt(remainingTime)}...";
    }

    private void OnHeroLoaded(Hero hero)
    {
        if (heroNameText != null) heroNameText.text = hero.heroName;
        if (heroRoleText != null) heroRoleText.text = hero.role;
        SetupAbilityIcons();
    }

    private void Update()
    {
        
        if (playerHealth == null) return;
        
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
        ultimateHUD.UpdateCharge(playerStats.currentUltimateCharge, playerStats.maxUltimateCharge);
        bool isReady = playerStats.currentUltimateCharge >= playerStats.maxUltimateCharge;
        
        ultimateHUD.icon.color = isReady ? Color.yellow : Color.gray;
        if (ultReadyIndicator != null)
            ultReadyIndicator.SetActive(isReady);
    }

    private void UpdateCooldowns()
    {
        if (abilityManager == null) return;
        if (primaryHUD != null) primaryHUD.UpdateStatus(abilityManager.primaryAbility);
        if (secondaryHUD != null) secondaryHUD.UpdateStatus(abilityManager.secondaryAbility);
        if (mobilityHUD != null) mobilityHUD.UpdateStatus(abilityManager.mobilityAbility);
        if (ultimateHUD != null) ultimateHUD.UpdateStatus(abilityManager.ultimateAbility);
    }

    private void SetupAbilityIcons()
    {
        if (abilityManager == null) return;
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
            hud.gameObject.SetActive(true);
            hud.icon.sprite = ability.sprite;
        }
        else
        {
            hud.gameObject.SetActive(false);
        }
    }
}