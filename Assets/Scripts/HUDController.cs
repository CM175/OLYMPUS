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

    [Header("Ult Charge")]
    public TextMeshProUGUI ultChargeText;

    [Header("Hero Info")]
    public TextMeshProUGUI heroNameText;
    public TextMeshProUGUI heroRoleText;

    [Header("Ability Icons")]
    public Image secondaryIcon;
    public Image mobilityIcon;
    
    public Image ultimateIcon;

    [Header("Cooldown Overlays")]
    public Image ultimateCooldownOverlay;
    public Image secondaryOverlay;
    public Image mobilityCooldownOverlay;

    [Header("Ult Ready")]
    public GameObject ultReadyIndicator; // A flashing or coloured object shown when ult is ready


    public void OnHeroLoaded(Hero hero)
    {
        if (heroNameText != null) heroNameText.text = hero.heroName;
        if (heroRoleText != null) heroRoleText.text = hero.role;
        SetupAbilityIcons();
    }

    void Update()
    {
        UpdateHealth();
        UpdateUltCharge();
        UpdateCooldowns();
    }

    void UpdateHealth()
    {
        if (playerHealth == null || playerHealth.maxHealth == 0) return;

        float fraction = (float)playerHealth.currentHealth / playerHealth.maxHealth;
        healthBarFill.fillAmount = fraction;

        if (healthText != null)
            healthText.text = $"{playerHealth.currentHealth} / {playerHealth.maxHealth}";

        // Colour shifts red as health drops
        healthBarFill.color = Color.Lerp(Color.red, Color.green, fraction);
    }

    void UpdateUltCharge()
    {
        if (playerStats == null) return;

        float fraction = playerStats.ultCharge();

        if (ultChargeText != null)
            ultChargeText.text = $"{fraction}%";

        bool isReady = playerStats.currentUltimateCharge >= playerStats.maxUltimateCharge;
        ultimateIcon.color = isReady ? Color.yellow : Color.gray;

        if (ultReadyIndicator != null)
            ultReadyIndicator.SetActive(isReady);
    }

    void UpdateCooldowns()
    {
        if (abilityManager == null) return;

        UpdateAbilityOverlay(abilityManager.secondaryAbility, secondaryOverlay);
        UpdateAbilityOverlay(abilityManager.mobilityAbility,  mobilityCooldownOverlay);
    }

    void UpdateAbilityOverlay(Ability ability, Image overlay)
    {
        if (ability == null || overlay == null) return;

        if (ability.IsOnCooldown())
        {
            // How far through the cooldown are we? (1 = just used, 0 = ready)
            float elapsed = Time.time - ability.lastUsedTime;
            float remaining = Mathf.Clamp01(1f - (elapsed / ability.cooldown));
            overlay.fillAmount = remaining;
            overlay.gameObject.SetActive(true);
        }
        else
        {
            overlay.gameObject.SetActive(false);
        }
    }

    public void SetupAbilityIcons()
    {
        if (abilityManager == null) return;

        if (abilityManager.secondaryAbility != null && secondaryIcon != null)
        {
            secondaryIcon.sprite = abilityManager.secondaryAbility.sprite;
        }

        if (abilityManager.mobilityAbility != null && mobilityIcon != null)
        {
            mobilityIcon.sprite = abilityManager.mobilityAbility.sprite;
        }
        
        if (abilityManager.ultimateAbility != null && ultimateCooldownOverlay != null)
        {
            ultimateIcon.sprite = abilityManager.ultimateAbility.sprite;
        }
    }
}