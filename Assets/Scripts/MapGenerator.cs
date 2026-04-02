using UnityEngine;
using UnityEngine.AI;

public class MapGenerator : MonoBehaviour
{
    [Header("Map Settings")]
    public float mapSize = 100f;
    public LayerMask groundLayer;

    [Header("Prefabs (Use Prototyping Cubes/Cylinders)")]
    public GameObject platformPrefab;
    public GameObject templePrefab;
    public GameObject bridgePrefab;

    private void Start()
    {
        GenerateLayout();
    }

    public void GenerateLayout()
    {
        // 1. Create Central Sacred Temple (Capture Point)
        // GDD: "Floating temples in the clouds"
        GameObject temple = Instantiate(templePrefab, Vector3.zero, Quaternion.identity);
        temple.name = "Sacred_Temple_Objective";
        temple.transform.localScale = new Vector3(20, 5, 20);

        // 2. Create Team Spawns (North and South)
        // GDD: "Matches are played between two teams of 5"
        CreatePlatform(new Vector3(0, 0, 45), "Team_Spawn_North");
        CreatePlatform(new Vector3(0, 0, -45), "Team_Spawn_South");

        // 3. Create Flank Routes (East and West)
        // GDD: "Vertical map with bridges and towers"
        CreatePlatform(new Vector3(35, 10, 0), "High_Flank_East"); // Verticality +10
        CreatePlatform(new Vector3(-35, -5, 0), "Low_Flank_West"); // Sunken Path -5

        // 4. Connect with Bridges
        ConnectPlatforms(Vector3.zero, new Vector3(0, 0, 45));
        ConnectPlatforms(Vector3.zero, new Vector3(0, 0, -45));
    }

    void CreatePlatform(Vector3 position, string name)
    {
        GameObject plat = Instantiate(platformPrefab, position, Quaternion.identity);
        plat.name = name;
        plat.transform.localScale = new Vector3(15, 2, 15);
        plat.layer = LayerMask.NameToLayer("Ground"); // For PlayerController groundMask
    }

    void ConnectPlatforms(Vector3 start, Vector3 end)
    {
        GameObject bridge = Instantiate(bridgePrefab);
        bridge.transform.position = (start + end) / 2;
        bridge.transform.LookAt(end);
        float dist = Vector3.Distance(start, end);
        bridge.transform.localScale = new Vector3(4, 1, dist);
    }
}