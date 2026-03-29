using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AbilityHUD : MonoBehaviour
{
    public Image icon;
    public Image overlay;
    public TextMeshProUGUI percentText; // Optional: To show "85%" on the icon

    // Existing time-based logic for standard abilities
    public void UpdateStatus(Ability ability)
    {
        if (ability == null) return;
        bool onCooldown = ability.IsOnCooldown();
        overlay.gameObject.SetActive(onCooldown);
        if (onCooldown) {
            float remaining = 1f - ((Time.time - ability.lastUsedTime) / ability.cooldown);
            overlay.fillAmount = remaining;
        }
    }

    // New charge-based logic for the Ultimate
    public void UpdateCharge(float current, float max)
    {
        float fraction = Mathf.Clamp01(current / max);
        
        // Invert fill: the overlay represents the "missing" charge
        overlay.gameObject.SetActive(fraction < 1.0f);
        overlay.fillAmount = 1f - fraction;

        if (percentText != null)
            percentText.text = fraction >= 1.0f ? "READY" : $"{(int)(fraction * 100)}%";
    }
}