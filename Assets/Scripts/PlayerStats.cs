using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public float currentUltimateCharge = 0f;
    public float maxUltimateCharge;
    public int chargePerHit = 5;

    [Header("Passive Charge")]
    public float pointsPerSecond = 5f;
    private float chargeAccumulator = 0f;

    public void AddCharge(int points)
    {
        currentUltimateCharge = Mathf.Clamp(currentUltimateCharge + points, 0, maxUltimateCharge);// Calculate the percentage first, then cast the whole result to an int
        int percentage = ultCharge();
        
        Debug.Log($"Ultimate Charge: {percentage}%");
    }
    public void Update()
    {
        if (currentUltimateCharge >= maxUltimateCharge) return;

        chargeAccumulator += pointsPerSecond * Time.deltaTime;

        if (chargeAccumulator >= 1f)
        {
            int toAdd = Mathf.FloorToInt(chargeAccumulator);
            chargeAccumulator -= toAdd;
            AddCharge(toAdd);
        }
        
    }
    public bool ConsumeCharge(int points)
    {
        if (currentUltimateCharge >= points)
        {
            currentUltimateCharge -= points;
            return true;
        }
        return false;
    }

    public int ultCharge()
    {
        return Mathf.RoundToInt(currentUltimateCharge / maxUltimateCharge * 100);
    }
}