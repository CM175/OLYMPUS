using UnityEngine;

public class SlayerOfTroy : PassiveAbility
{
    public float killSpeedBoost = 1.5f;
    public float duration = 3f;
    private float _timer = 0f;
    private IMovementController _movementController; // OPTIMIZED: Swapped from PlayerController

    void Start()
    {
        _movementController = GetComponentInParent<IMovementController>();
    }

    public void RegisterEnemy(EnemyHealth enemy)
    {
        enemy.OnDeathWithKiller.AddListener(CheckIfIKilledIt);
    }

    private void CheckIfIKilledIt(GameObject killer)
    {
        if (killer == transform.root.gameObject)
        {
            _timer = duration;
        }
    }

    void Update()
    {
        if (_movementController == null) return;

        if (_timer > 0)
        {
            _timer -= Time.deltaTime;
            _movementController.SpeedMultiplier = killSpeedBoost;
        }
        else
        {
            _movementController.SpeedMultiplier = 1f;
        }
    }

    protected override void Activate() { }
}