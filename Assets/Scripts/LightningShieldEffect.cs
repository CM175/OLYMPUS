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
        originalVertices = meshFilter.mesh.vertices;
        displacedVertices = new Vector3[originalVertices.Length];
        InvokeRepeating(nameof(JitterMesh), 0, updateInterval);
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