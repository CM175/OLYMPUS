/// <summary>
/// Implemented by anything that can receive damage.
/// Decouples attackers (EnemyAttack, LightningBolt, ShieldBash, BarrierDamage)
/// from concrete health components (PlayerHealth, EnemyHealth).
/// </summary>
public interface IDamageable
{
    bool IsInvincible { get; set; }
    void TakeDamage(int damage);
    void ApplyHeal(int heal);
}