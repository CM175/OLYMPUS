using System.Collections;
using UnityEngine;

public class AmbushInvisibility : Ability
{
    public float duration = 10f;

    protected override void Activate()
    {
        StartCoroutine(InvisibilityRoutine());
    }

    private IEnumerator InvisibilityRoutine()
    {
        // Toggle invisibility logic (e.g., changing layers or shaders)
        SetInvisibility(true);
        //Debug.Log("[AmbushInvisibility] Hidden.");

        yield return new WaitForSeconds(duration);

        SetInvisibility(false);
        //Debug.Log("[AmbushInvisibility] Revealed.");
        StartCooldown();
    }

    private void SetInvisibility(bool state)
    {
        // Example: Disable renderers on the hero model
        // Note: In a full implementation, you'd use a transparent shader
        MeshRenderer[] renderers = transform.root.GetComponentsInChildren<MeshRenderer>();
        foreach (var r in renderers) 
        {
            r.enabled = !state;
        }
    }
}