using UnityEngine;
using System.Collections;

[CreateAssetMenu(fileName = "MoonlightChase", menuName = "Olympus/Abilities/Moonlight Chase")]
public class MoonlightChase : Ultimate, IInitializable
{
    [Header("Moonlight Chase Settings")]
    public float duration = 8f;
    public float speedBuffMultiplier = 1.3f; // +30% Speed

    private PlayerController _playerController;
    private CrescentBow bow;

    public void Initialize(GameObject playerRoot)
    {
        _playerController = playerRoot.GetComponent<PlayerController>();
        bow = playerRoot.GetComponentInChildren<CrescentBow>();
    }

    protected override void Activate()
    {
        base.Activate();
        StartCoroutine(ChaseRoutine());
    }
    private IEnumerator ChaseRoutine()
    {
        // 1. Play Voice Lines & FX
        Debug.Log("Artemis: 'The hunt begins!'");

        // 2. Apply Speed Buff via PlayerController's SpeedMultiplier
        float originalMultiplier = _playerController.SpeedMultiplier;
        _playerController.SpeedMultiplier *= speedBuffMultiplier;

        // 3. Enable Infinite Ammo / Arrow Piercing / Wall-Hacks
        SetHuntState(true);

        // 4. Run the duration timer countdown loop
        yield return new WaitForSeconds(duration);

        // 5. Cleanup & Reset when ultimate expires
        _playerController.SpeedMultiplier = originalMultiplier;
        SetHuntState(false);
        
        StartCooldown();
    }

    private void SetHuntState(bool active)
    {
        // Communicate flags to Artemis' weapon or stats handler
        if (bow != null)
        {
            bow.SetUltimateModifications(active);
        }

        // Toggle global outline handlers or UI indicators
        ToggleEnemyMoonlightAuras(active);
    }

    private void ToggleEnemyMoonlightAuras(bool enable)
    {
        // Finds targets to attach or toggle a glowing silver outline component
        var enemies = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            // Toggle visibility of a moonlight aura child visual object on enemies
            Transform aura = enemy.transform.Find("MoonlightAuraEffect");
            if (aura != null) aura.gameObject.SetActive(enable);
        }
    }
}