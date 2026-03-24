using UnityEngine;

public class Hero : MonoBehaviour
{
    public string heroName;
    public string role;
    public int health;

    public Ability       primaryAbility;
    public Ability       secondaryAbility;
    public Ability       mobilityAbility;
    public Ultimate      ultimateAbility;
    public PassiveAbility passiveAbility;   // Optional — leave null for heroes without a passive
}