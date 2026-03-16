using UnityEngine;

public class AbilityManager : MonoBehaviour
{
    public Ability primaryAbility;
    public Ability secondaryAbility;
    public Ability mobilityAbility;
    public Ultimate ultimateAbility;

    public PlayerStats playerStats;

    void awake()
    {
        playerStats= GetComponent<PlayerStats>();
    }

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
            if (ultimateAbility.IsReady(playerStats.currentUltimateCharge))
            {
                ultimateAbility.Use();
                playerStats.ConsumeCharge(ultimateAbility.chargeCost);
            }
        }
    }
}