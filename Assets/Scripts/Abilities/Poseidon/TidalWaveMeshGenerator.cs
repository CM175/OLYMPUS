using UnityEngine;

/// <summary>
/// Builds a stylised, jagged water wall inspired by Wuyang's Guardian Wave.
/// The top edge is deliberately irregular: this is not a stretched cube.
/// Attach to the same GameObject as the MeshFilter and MeshRenderer.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[ExecuteAlways]
public class TidalWaveMeshGenerator : MonoBehaviour
{
    [Header("Overall Size")]
    [Min(0.1f)] public float width = 6.5f;
    [Min(0.1f)] public float height = 3.2f;
    [Min(0.1f)] public float length = 2.0f;

    [Header("Mesh Resolution")]
    [Range(16, 128)] public int widthSegments = 64;
    [Range(8, 64)] public int heightSegments = 32;

    [Header("Wave Silhouette")]
    [Range(0f, 1f)] public float crestVariation = 0.85f;
    [Range(0f, 1f)] public float sideTaper = 0.45f;
    [Range(0f, 1f)] public float crestCurl = 0.7f;

    [Header("Water Volume")]
    [Min(0.02f)] public float thickness = 0.3f;
    [Range(0f, 1f)] public float faceBulge = 0.25f;
    [Range(0f, 1f)] public float surfaceUndulation = 0.12f;

    private Mesh generatedMesh;

    private void OnEnable() => BuildWaveMesh();
    private void OnValidate() => BuildWaveMesh();

    public void BuildWaveMesh()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (!mf) return;

        int ws = Mathf.Max(16, widthSegments);
        int hs = Mathf.Max(8, heightSegments);
        int cols = ws + 1;
        int rows = hs + 1;
        int count = cols * rows;

        Vector3[] vertices = new Vector3[count * 2];
        Vector2[] uvs = new Vector2[vertices.Length];

        // Each front/back grid has ws*hs quads. Four perimeter strips close the volume.
        int[] triangles = new int[ws * hs * 12 + (ws + hs) * 24];
        int tri = 0;

        for (int r = 0; r <= hs; r++)
        {
            float v = r / (float)hs;

            for (int c = 0; c <= ws; c++)
            {
                float u = c / (float)ws;
                float x = (u - 0.5f) * 2f;
                float ax = Mathf.Abs(x);

                // A broad body with five distinct splash peaks and lower valleys.
                float p0 = Gaussian(x, -0.82f, 0.105f) * 0.52f;
                float p1 = Gaussian(x, -0.43f, 0.16f) * 0.88f;
                float p2 = Gaussian(x, -0.02f, 0.12f) * 0.62f;
                float p3 = Gaussian(x,  0.39f, 0.15f) * 1.00f;
                float p4 = Gaussian(x,  0.79f, 0.10f) * 0.58f;
                float peaks = p0 + p1 + p2 + p3 + p4;

                float rolling = 0.5f + 0.5f * Mathf.Sin((x * 2.6f + 0.2f) * Mathf.PI);
                float crest = 0.48f + peaks * 0.48f + rolling * 0.08f;
                crest = Mathf.Clamp(crest, 0.38f, 1.0f);

                // The top is high in the middle and breaks into separate splash fingers.
                float edgeDrop = Mathf.Lerp(1f, 0.62f, Mathf.Pow(ax, 1.7f) * sideTaper);
                float topY = height * crest * edgeDrop;

                // Slightly pinch the lower corners so the wave grows out of the ground.
                float widthAtHeight = Mathf.Lerp(0.84f, 1f, Mathf.SmoothStep(0f, 0.45f, v));
                float px = x * width * 0.5f * widthAtHeight;

                // Make the wall roll forward near the top instead of remaining planar.
                float bulge = (1f - x * x) * Mathf.Sin(v * Mathf.PI);
                float forwardRoll = Mathf.Pow(v, 2.0f) * crestCurl;
                float ripples = Mathf.Sin(x * 9f + v * 5f) *
                                Mathf.Sin(v * Mathf.PI) * surfaceUndulation;
                float z = (v - 0.35f) * length + forwardRoll +
                          bulge * faceBulge + ripples;

                // Push individual splash peaks slightly forward for a broken water edge.
                z += peaks * Mathf.Pow(v, 3f) * 0.22f;

                int i = r * cols + c;
                Vector3 front = new Vector3(px, v * topY, z + thickness * 0.5f);
                Vector3 back  = new Vector3(px, v * topY, z - thickness * 0.5f);

                vertices[i] = front;
                vertices[count + i] = back;
                uvs[i] = new Vector2(u, v);
                uvs[count + i] = new Vector2(u, v);
            }
        }

        // Front face (+Z).
        for (int r = 0; r < hs; r++)
        for (int c = 0; c < ws; c++)
        {
            int a = r * cols + c;
            int b = a + 1;
            int d = a + cols;
            int e = d + 1;
            triangles[tri++] = a; triangles[tri++] = b; triangles[tri++] = d;
            triangles[tri++] = b; triangles[tri++] = e; triangles[tri++] = d;
        }

        // Back face (-Z).
        for (int r = 0; r < hs; r++)
        for (int c = 0; c < ws; c++)
        {
            int a = count + r * cols + c;
            int b = a + 1;
            int d = a + cols;
            int e = d + 1;
            triangles[tri++] = a; triangles[tri++] = d; triangles[tri++] = b;
            triangles[tri++] = b; triangles[tri++] = d; triangles[tri++] = e;
        }

        // Top and bottom boundary strips.
        for (int c = 0; c < ws; c++)
        {
            AddQuad(triangles, ref tri, c, c + 1, count + c, count + c + 1);
            int a = hs * cols + c;
            int b = a + 1;
            AddQuad(triangles, ref tri, a, b, count + a, count + b);
        }

        // Left and right boundary strips.
        for (int r = 0; r < hs; r++)
        {
            int leftA = r * cols;
            int leftB = leftA + cols;
            AddQuad(triangles, ref tri, leftA, leftB, count + leftA, count + leftB);

            int rightA = r * cols + ws;
            int rightB = rightA + cols;
            AddQuad(triangles, ref tri, rightA, rightB, count + rightA, count + rightB);
        }

        if (tri != triangles.Length)
            System.Array.Resize(ref triangles, tri);

        Mesh mesh = new Mesh { name = "GuardianWave_WaterWall" };
        if (vertices.Length > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        if (generatedMesh != null)
        {
            if (Application.isPlaying) Destroy(generatedMesh);
            else DestroyImmediate(generatedMesh);
        }

        generatedMesh = mesh;
        mf.sharedMesh = generatedMesh;
    }

    private static float Gaussian(float x, float centre, float width)
    {
        float d = (x - centre) / width;
        return Mathf.Exp(-0.5f * d * d);
    }

    private static void AddQuad(int[] tris, ref int t,
        int frontA, int frontB, int backA, int backB)
    {
        tris[t++] = frontA; tris[t++] = frontB; tris[t++] = backA;
        tris[t++] = frontB; tris[t++] = backB; tris[t++] = backA;
    }
}
