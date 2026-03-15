using UnityEngine;

public class AbilityManager : MonoBehaviour
{
    public Ability primaryAbility;
    public Ability secondaryAbility;
    public Ability mobilityAbility;
    public Ability ultimateAbility;

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && primaryAbility != null)
        {
            
            primaryAbility.Use();
        }

        if (Input.GetKeyDown(KeyCode.Q) && secondaryAbility != null)
        {
            secondaryAbility.Use();
        }

        if (Input.GetKeyDown(KeyCode.E) && mobilityAbility != null)
        {
            mobilityAbility.Use();
        }

        if (Input.GetKeyDown(KeyCode.F) && ultimateAbility != null)
        {
            ultimateAbility.Use();
        }
    }
}