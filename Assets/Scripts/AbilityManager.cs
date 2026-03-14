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

        if (Input.GetButtonDown("Fire2"))
            secondaryAbility.Use();

        if (Input.GetKeyDown(KeyCode.LeftShift))
            mobilityAbility.Use();

        if (Input.GetKeyDown(KeyCode.Q))
            ultimateAbility.Use();
    }
}