using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class AbilityHUD : MonoBehaviour
{
    public Image icon;
    public Image overlay;
    
    // This script only manages one icon's cooldown
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
}