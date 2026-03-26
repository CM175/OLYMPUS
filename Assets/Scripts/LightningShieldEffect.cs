using UnityEngine;

public class LightningShieldEffect : MonoBehaviour
{
    private MeshFilter meshFilter;
    private Vector3[] originalVertices;
    private Vector3[] displacedVertices;

    public float jitterStrength = 0.15f;
    public float updateInterval = 0.05f; // How fast it flickers

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        
        // Clone explicitly so we're intentional about it
        var clonedMesh = Instantiate(meshFilter.sharedMesh);
        meshFilter.mesh = clonedMesh;
        
        originalVertices  = clonedMesh.vertices;
        displacedVertices = new Vector3[originalVertices.Length];
        
        InvokeRepeating(nameof(JitterMesh), 0, updateInterval);
    }

    void OnDestroy()
    {
        // Clean up the cloned mesh to prevent leaks
        if (meshFilter.mesh != null)
            Destroy(meshFilter.mesh);
        
        CancelInvoke(nameof(JitterMesh));
    }

    void JitterMesh()
    {
        for (int i = 0; i < originalVertices.Length; i++)
        {
            // Move each vertex randomly outward/inward to create jagged edges
            float noise = Random.Range(-jitterStrength, jitterStrength);
            displacedVertices[i] = originalVertices[i] + (originalVertices[i].normalized * noise);
        }
        meshFilter.mesh.vertices = displacedVertices;
        meshFilter.mesh.RecalculateBounds();
    }
}