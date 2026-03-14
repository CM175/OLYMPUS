using UnityEngine;

public class AbilityManager : MonoBehaviour
{
    public Ability primaryAbility;
    public Ability secondaryAbility;
    public Ability mobilityAbility;
    public Ability ultimateAbility;

    void Update()
    {
        if (Input.GetButtonDown("Fire1"))
            primaryAbility.Use();

        if (Input.GetKeyDown(KeyCode.E))
            secondaryAbility.Use();

        if (Input.GetKeyDown(KeyCode.Q))
            mobilityAbility.Use();

        if (Input.GetKeyDown(KeyCode.F))
            ultimateAbility.Use();
    }
}