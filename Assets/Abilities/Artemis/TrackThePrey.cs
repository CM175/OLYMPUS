using UnityEngine;

public class TrackThePrey : PassiveAbility
{
    // This logic usually hooks into an 'OnDamageDealt' event in the PlayerStats or Health system

    protected override void Activate()
    {
        // Passive abilities often don't need logic here if they 
        // are purely event-based or constant.
    }

    public void OnEnemyHit(GameObject enemy)
    {
        // Highlight logic (e.g., adding an outline component or changing layer)
        Debug.Log($"[TrackThePrey] Marking target: {enemy.name}");
    }
}