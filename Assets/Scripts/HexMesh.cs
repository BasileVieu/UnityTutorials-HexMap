using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HexMesh : MonoBehaviour
{
    [SerializeField] private bool m_useCollider;
    [SerializeField] private bool m_useCellData;
    [SerializeField] private bool m_useUVCoordinates;
    [SerializeField] private bool m_useUV2Coordinates;
    
    [NonSerialized] private List<Vector3> m_vertices;
    [NonSerialized] private List<Vector3> m_cellIndices;
    [NonSerialized] private List<Color> m_cellWeights;
    [NonSerialized] private List<int> m_triangles;
    [NonSerialized] private List<Vector2> m_uvs;
    [NonSerialized] private List<Vector2> m_uv2s;

    private Mesh m_hexMesh;
    
    private MeshCollider m_meshCollider;

    private void Awake()
    {
        GetComponent<MeshFilter>().mesh = m_hexMesh = new Mesh();

        if (m_useCollider)
        {
            m_meshCollider = gameObject.AddComponent<MeshCollider>();
        }
        
        m_hexMesh.name = "Hex Mesh";
    }

    public void Clear()
    {
        m_hexMesh.Clear();
        
        m_vertices = ListPool<Vector3>.Get();

        if (m_useCellData)
        {
            m_cellWeights = ListPool<Color>.Get();
            m_cellIndices = ListPool<Vector3>.Get();
        }

        if (m_useUVCoordinates)
        {
            m_uvs = ListPool<Vector2>.Get();
        }

        if (m_useUV2Coordinates)
        {
            m_uv2s = ListPool<Vector2>.Get();
        }
        
        m_triangles = ListPool<int>.Get();
    }

    public void Apply()
    {
        m_hexMesh.SetVertices(m_vertices);

        ListPool<Vector3>.Add(m_vertices);

        if (m_useCellData)
        {
            m_hexMesh.SetColors(m_cellWeights);

            ListPool<Color>.Add(m_cellWeights);
            
            m_hexMesh.SetUVs(2, m_cellIndices);
            
            ListPool<Vector3>.Add(m_cellIndices);
        }

        if (m_useUVCoordinates)
        {
            m_hexMesh.SetUVs(0, m_uvs);

            ListPool<Vector2>.Add(m_uvs);
        }

        if (m_useUV2Coordinates)
        {
            m_hexMesh.SetUVs(1, m_uv2s);

            ListPool<Vector2>.Add(m_uv2s);
        }

        m_hexMesh.SetTriangles(m_triangles, 0);

        ListPool<int>.Add(m_triangles);
        
        m_hexMesh.RecalculateNormals();

        if (m_useCollider)
        {
            m_meshCollider.sharedMesh = m_hexMesh;
        }
    }

    public void AddTriangle(Vector3 v1, Vector3 v2, Vector3 v3)
    {
        int vertexIndex = m_vertices.Count;
        
        m_vertices.Add(HexMetrics.Perturb(v1));
        m_vertices.Add(HexMetrics.Perturb(v2));
        m_vertices.Add(HexMetrics.Perturb(v3));
        
        m_triangles.Add(vertexIndex);
        m_triangles.Add(vertexIndex + 1);
        m_triangles.Add(vertexIndex + 2);
    }

    public void AddTriangleUnperturbed(Vector3 v1, Vector3 v2, Vector3 v3)
    {
        int vertexIndex = m_vertices.Count;
        
        m_vertices.Add(v1);
        m_vertices.Add(v2);
        m_vertices.Add(v3);
        
        m_triangles.Add(vertexIndex);
        m_triangles.Add(vertexIndex + 1);
        m_triangles.Add(vertexIndex + 2);
    }

    public void AddTriangleCellData(Vector3 indices, Color weights1, Color weights2, Color weights3)
    {
        m_cellIndices.Add(indices);
        m_cellIndices.Add(indices);
        m_cellIndices.Add(indices);
        
        m_cellWeights.Add(weights1);
        m_cellWeights.Add(weights2);
        m_cellWeights.Add(weights3);
    }

    public void AddTriangleCellData(Vector3 indices, Color weights)
    {
        AddTriangleCellData(indices, weights, weights, weights);
    }

    public void AddTriangleUV(Vector2 uv1, Vector2 uv2, Vector2 uv3)
    {
        m_uvs.Add(uv1);
        m_uvs.Add(uv2);
        m_uvs.Add(uv3);
    }

    public void AddTriangleUV2(Vector2 uv1, Vector2 uv2, Vector2 uv3)
    {
        m_uv2s.Add(uv1);
        m_uv2s.Add(uv2);
        m_uv2s.Add(uv3);
    }

    public void AddQuad(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4)
    {
        int vertexIndex = m_vertices.Count;

        m_vertices.Add(HexMetrics.Perturb(v1));
        m_vertices.Add(HexMetrics.Perturb(v2));
        m_vertices.Add(HexMetrics.Perturb(v3));
        m_vertices.Add(HexMetrics.Perturb(v4));
        
        m_triangles.Add(vertexIndex);
        m_triangles.Add(vertexIndex + 2);
        m_triangles.Add(vertexIndex + 1);
        m_triangles.Add(vertexIndex + 1);
        m_triangles.Add(vertexIndex + 2);
        m_triangles.Add(vertexIndex + 3);
    }

    public void AddQuadUnperturbed(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4)
    {
        int vertexIndex = m_vertices.Count;

        m_vertices.Add(v1);
        m_vertices.Add(v2);
        m_vertices.Add(v3);
        m_vertices.Add(v4);
        
        m_triangles.Add(vertexIndex);
        m_triangles.Add(vertexIndex + 2);
        m_triangles.Add(vertexIndex + 1);
        m_triangles.Add(vertexIndex + 1);
        m_triangles.Add(vertexIndex + 2);
        m_triangles.Add(vertexIndex + 3);
    }

    public void AddQuadCellData(Vector3 indices, Color weights1, Color weights2, Color weights3, Color weights4)
    {
        m_cellIndices.Add(indices);
        m_cellIndices.Add(indices);
        m_cellIndices.Add(indices);
        m_cellIndices.Add(indices);
        
        m_cellWeights.Add(weights1);
        m_cellWeights.Add(weights2);
        m_cellWeights.Add(weights3);
        m_cellWeights.Add(weights4);
    }

    public void AddQuadCellData(Vector3 indices, Color weights1, Color weights2)
    {
        AddQuadCellData(indices, weights1, weights1, weights2, weights2);
    }

    public void AddQuadCellData(Vector3 indices, Color weights)
    {
        AddQuadCellData(indices, weights, weights, weights, weights);
    }

    public void AddQuadUV(Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector2 uv4)
    {
        m_uvs.Add(uv1);
        m_uvs.Add(uv2);
        m_uvs.Add(uv3);
        m_uvs.Add(uv4);
    }

    public void AddQuadUV2(Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector2 uv4)
    {
        m_uv2s.Add(uv1);
        m_uv2s.Add(uv2);
        m_uv2s.Add(uv3);
        m_uv2s.Add(uv4);
    }

    public void AddQuadUV(float uMin, float uMax, float vMin, float vMax)
    {
        m_uvs.Add(new Vector2(uMin, vMin));
        m_uvs.Add(new Vector2(uMax, vMin));
        m_uvs.Add(new Vector2(uMin, vMax));
        m_uvs.Add(new Vector2(uMax, vMax));
    }

    public void AddQuadUV2(float uMin, float uMax, float vMin, float vMax)
    {
        m_uv2s.Add(new Vector2(uMin, vMin));
        m_uv2s.Add(new Vector2(uMax, vMin));
        m_uv2s.Add(new Vector2(uMin, vMax));
        m_uv2s.Add(new Vector2(uMax, vMax));
    }
}