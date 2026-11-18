using UnityEngine;

public class TidalWave : Ability, IInitializable
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

        AbilityVfx.Burst(spawnPos, AbilityVfx.Azure, 1.5f, 30);
        AbilityVfx.Ring(spawnPos, AbilityVfx.Azure, 3f);

        GameObject wave = Instantiate(waveProjectilePrefab, spawnPos, spawnRot);
        TidalWaveProjectile waveScript = wave.GetComponent<TidalWaveProjectile>();
        if (waveScript != null)
        {
            waveScript.SetAttacker(_rootTransform.gameObject);
        }

        StartCooldown();
    }
}