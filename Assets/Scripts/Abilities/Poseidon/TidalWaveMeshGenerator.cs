using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a volumetric, stylised water wave inspired by Wuyang's Guardian Wave.
/// The mesh is a closed wave cross-section (concave face, overhanging lip, rounded back)
/// extruded across the width, with a jagged crest. Origin is the base of the front face;
/// the lip leads toward +Z. Field names match the previous version so existing prefabs keep working.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[ExecuteAlways]
public class TidalWaveMeshGenerator : MonoBehaviour
{
    [Header("Overall Size")]
    [Min(0.1f)] public float width = 6.5f;
    [Min(0.1f)] public float height = 3.2f;
    [Tooltip("Base depth scale: how far the lip overhangs forward and how long the body is.")]
    [Min(0.1f)] public float length = 2.0f;

    [Header("Mesh Resolution")]
    [Range(16, 128)] public int widthSegments = 64;
    [Range(8, 64)] public int heightSegments = 32;

    [Header("Wave Silhouette")]
    [Tooltip("How much the lip pushes forward at the splash peaks (jagged fingers).")]
    [Range(0f, 1f)] public float crestVariation = 0.85f;
    [Tooltip("Shrinks the wave depth and height toward the left/right ends.")]
    [Range(0f, 1f)] public float sideTaper = 0.45f;
    [Tooltip("How far the lip overhangs the face.")]
    [Range(0f, 1f)] public float crestCurl = 0.7f;

    [Header("Water Volume")]
    [Tooltip("Back slope length = length * (1 + thickness). Bigger = a fatter, heavier body.")]
    [Min(0.02f)] public float thickness = 0.3f;
    [Range(0f, 1f)] public float faceBulge = 0.25f;
    [Range(0f, 1f)] public float surfaceUndulation = 0.12f;

    private Mesh generatedMesh;

    private void OnEnable() => BuildWaveMesh();
    private void OnValidate() => BuildWaveMesh();

    public void BuildWaveMesh()
    {
        try { BuildWaveMeshInternal(); }
        catch (System.Exception e)
        {
            Debug.LogError("TidalWaveMeshGenerator: mesh build failed: " + e, this);
        }
    }

    private void BuildWaveMeshInternal()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (!mf) { Debug.LogError("TidalWaveMeshGenerator: no MeshFilter on this GameObject.", this); return; }

        int ws = Mathf.Max(16, widthSegments);
        int hs = Mathf.Max(8, heightSegments);
        int cols = ws + 1;
        int prof = hs * 2 + 1; // profile points: front face (base->tip) then back slope (tip->back base)

        var verts = new List<Vector3>(cols * prof + 2 * (prof + 1));
        var uvs = new List<Vector2>(verts.Capacity);
        var tris = new List<int>(ws * (prof - 1) * 6 + prof * 6);

        for (int c = 0; c < cols; c++)
        {
            float u = c / (float)ws;
            float x = (u - 0.5f) * 2f;
            float ax = Mathf.Abs(x);

            // Broad body with five splash peaks and lower valleys.
            float p0 = Gaussian(x, -0.82f, 0.105f) * 0.52f;
            float p1 = Gaussian(x, -0.43f, 0.16f) * 0.88f;
            float p2 = Gaussian(x, -0.02f, 0.12f) * 0.62f;
            float p3 = Gaussian(x,  0.39f, 0.15f) * 1.00f;
            float p4 = Gaussian(x,  0.79f, 0.10f) * 0.58f;
            float peaks = p0 + p1 + p2 + p3 + p4;

            float rolling = 0.5f + 0.5f * Mathf.Sin((x * 2.6f + 0.2f) * Mathf.PI);
            float crest = Mathf.Clamp(0.48f + peaks * 0.48f + rolling * 0.08f, 0.38f, 1.0f);
            float edgeDrop = Mathf.Lerp(1f, 0.62f, Mathf.Pow(ax, 1.7f) * sideTaper);
            float H = height * crest * edgeDrop;

            float depthScale = Mathf.Lerp(1f, 0.55f, Mathf.Pow(ax, 1.7f) * sideTaper);
            float pk = Mathf.Clamp01(peaks);
            float lip = length * (0.3f + 0.9f * crestCurl)
                        * (1f + crestVariation * (pk - 0.5f) * 0.6f) * depthScale;
            float back = length * (1f + thickness) * depthScale;
            float px = x * width * 0.5f;

            for (int k = 0; k < prof; k++)
            {
                float y, z, v;
                if (k <= hs)
                {
                    // Concave face: vertical at the base, curving forward into the lip.
                    float s = k / (float)hs;
                    float th = s * Mathf.PI * 0.5f;
                    y = H * Mathf.Sin(th);
                    z = lip * (1f - Mathf.Cos(th));
                    float bow = Mathf.Sin(s * Mathf.PI);
                    z += (1f - x * x) * faceBulge * bow;
                    z += Mathf.Sin(x * 9f + s * 6f) * bow * surfaceUndulation;
                    v = s;
                }
                else
                {
                    // Rounded back: flat-ish behind the lip, sloping down to the ground.
                    float q = (k - hs) / (float)hs;
                    y = H * (1f - q * q);
                    z = Mathf.Lerp(lip, -back, q);
                    z += Mathf.Sin(x * 7f + q * 5f) * Mathf.Sin(q * Mathf.PI) * surfaceUndulation * 0.5f;
                    v = 1f - q; // v = closeness to the crest on both sides
                }

                verts.Add(new Vector3(px, y, z));
                uvs.Add(new Vector2(u, v));
            }
        }

        // Body surface.
        for (int c = 0; c < ws; c++)
        for (int k = 0; k < prof - 1; k++)
        {
            int a = c * prof + k;
            int b = (c + 1) * prof + k;
            int d = a + 1;
            int e = b + 1;
            tris.Add(a); tris.Add(b); tris.Add(d);
            tris.Add(b); tris.Add(e); tris.Add(d);
        }

        // End caps (own vertices so normals stay crisp).
        AddCap(verts, uvs, tris, 0, prof, false);
        AddCap(verts, uvs, tris, ws, prof, true);

        Mesh mesh = new Mesh { name = "GuardianWave_WaterWall" };
        if (verts.Count > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
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

    private static void AddCap(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
        int col, int prof, bool flip)
    {
        int first = col * prof;
        Vector3 center = Vector3.zero;
        for (int k = 0; k < prof; k++) center += verts[first + k];
        center /= prof;

        int baseIdx = verts.Count;
        for (int k = 0; k < prof; k++)
        {
            verts.Add(verts[first + k]);
            uvs.Add(uvs[first + k]);
        }
        verts.Add(center);
        uvs.Add(new Vector2(uvs[first].x, 0.5f));
        int ci = baseIdx + prof;

        for (int k = 0; k < prof - 1; k++)
        {
            if (!flip) { tris.Add(ci); tris.Add(baseIdx + k);     tris.Add(baseIdx + k + 1); }
            else       { tris.Add(ci); tris.Add(baseIdx + k + 1); tris.Add(baseIdx + k);     }
        }
    }

    private static float Gaussian(float x, float centre, float width)
    {
        float d = (x - centre) / width;
        return Mathf.Exp(-0.5f * d * d);
    }
}