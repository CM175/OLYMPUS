using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public int currentUltimateCharge = 0;
    public int maxUltimateCharge = 100;
    public int chargePerHit = 5;

    public void AddCharge(int amount)
    {
        currentUltimateCharge = Mathf.Clamp(currentUltimateCharge + amount, 0, maxUltimateCharge);
        Debug.Log($"Ultimate Charge: {currentUltimateCharge}%");
    }

    public bool ConsumeCharge(int amount)
    {
        if (currentUltimateCharge >= amount)
        {
            currentUltimateCharge -= amount;
            return true;
        }
        return false;
    }
}