/// <summary>
/// Implemented by any ability that needs access to the player root
/// on hero load (e.g. passives that modify PlayerHealth or PlayerStats).
/// HeroManager calls Initialize() on any ability implementing this,
/// so HeroManager never needs to know the concrete passive type.
/// </summary>
public interface IInitializable
{
    void Initialize(UnityEngine.GameObject playerRoot);
}