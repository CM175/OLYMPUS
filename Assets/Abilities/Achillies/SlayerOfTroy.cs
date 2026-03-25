using UnityEngine;

public class SlayerOfTroy : PassiveAbility
{
    public float killSpeedBoost = 1.5f;
    public float duration = 3f;
    private float _timer = 0f;
    private PlayerController _controller;

    void Start()
    {
        _controller = GetComponentInParent<PlayerController>();
    }

    // Call this whenever Achilles hits an enemy
    public void RegisterEnemy(EnemyHealth enemy)
    {
        // Subscribe to the death event: "Tell me when you die"
        enemy.OnDeathWithKiller.AddListener(CheckIfIKilledIt);
    }

    private void CheckIfIKilledIt(GameObject killer)
    {
        // Only trigger if the person who dealt the final blow is THIS player
        if (killer == transform.root.gameObject)
        {
            _timer = duration;
        }
    }

    void Update()
    {
        if (_controller == null) return;

        if (_timer > 0)
        {
            _timer -= Time.deltaTime;
            _controller.SpeedMultiplier = killSpeedBoost;
        }
        else
        {
            _controller.SpeedMultiplier = 1f;
        }
    }

    protected override void Activate() { }
}