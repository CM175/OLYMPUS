// Assets/Abilities/Zeus/LightningBarrier.cs (Ability 1)
using System.Collections;
using UnityEngine;

public class LightningBarrier : Ability
{
    public GameObject barrierPrefab;
    public float duration = 4f;

    protected override void Activate()
    {
        if (barrierPrefab == null) return;

        Vector3 spawnPos = transform.position + transform.forward * 2f;
        GameObject barrier = Instantiate(barrierPrefab, spawnPos, transform.rotation);
        Destroy(barrier, duration);

        Debug.Log("Lightning Barrier activated");
    }
}