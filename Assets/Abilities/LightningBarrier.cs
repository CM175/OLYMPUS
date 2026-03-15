using UnityEngine;

public class LightningBarrier : Ability
{
    public GameObject barrierPrefab;
    public float duration = 4f;
    Transform playerTransform;

    protected override void Activate()
    {
        if (barrierPrefab == null) return;

        playerTransform = GameObject.FindWithTag("Player").transform;
        Vector3 spawnPos = playerTransform.position + playerTransform.forward * 2f;
        GameObject barrier = Instantiate(barrierPrefab, spawnPos, playerTransform.rotation);
        Destroy(barrier, duration);

        Debug.Log("Lightning Barrier activated");
    }
}