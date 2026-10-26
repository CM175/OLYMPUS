using UnityEngine;

public class TidalSurge : Ability, IInitializable
{
    [Header("Wave Setup")]
    public GameObject waveProjectilePrefab;
    public float forwardSpawnOffset = 1.5f;

    private Transform _rootTransform;

    public void Initialize(GameObject playerRoot)
    {
        _rootTransform = playerRoot.transform;
    }

    protected override void Activate()
    {
        if (waveProjectilePrefab == null)
        {
            StartCooldown();
            return;
        }

        Vector3 spawnPos = _rootTransform.position + (_rootTransform.forward * forwardSpawnOffset) + Vector3.up * 0.2f;
        Quaternion spawnRot = Quaternion.LookRotation(_rootTransform.forward);

        GameObject wave = Instantiate(waveProjectilePrefab, spawnPos, spawnRot);
        TidalSurgeProjectile waveScript = wave.GetComponent<TidalSurgeProjectile>();
        if (waveScript != null)
        {
            waveScript.SetAttacker(_rootTransform.gameObject);
        }

        StartCooldown();
    }
}