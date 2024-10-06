using UnityEngine;

public struct EdgeVertices
{
    public Vector3 m_v1;
    public Vector3 m_v2;
    public Vector3 m_v3;
    public Vector3 m_v4;
    public Vector3 m_v5;

    public EdgeVertices(Vector3 corner1, Vector3 corner2)
    {
        m_v1 = corner1;
        m_v2 = Vector3.Lerp(corner1, corner2, 0.25f);
        m_v3 = Vector3.Lerp(corner1, corner2, 0.5f);
        m_v4 = Vector3.Lerp(corner1, corner2, 0.75f);
        m_v5 = corner2;
    }

    public EdgeVertices(Vector3 corner1, Vector3 corner2, float outerStep)
    {
        m_v1 = corner1;
        m_v2 = Vector3.Lerp(corner1, corner2, outerStep);
        m_v3 = Vector3.Lerp(corner1, corner2, 0.5f);
        m_v4 = Vector3.Lerp(corner1, corner2, 1.0f - outerStep);
        m_v5 = corner2;
    }

    public static EdgeVertices TerraceLerp(EdgeVertices a, EdgeVertices b, int step)
    {
        EdgeVertices result;
        result.m_v1 = HexMetrics.TerraceLerp(a.m_v1, b.m_v1, step);
        result.m_v2 = HexMetrics.TerraceLerp(a.m_v2, b.m_v2, step);
        result.m_v3 = HexMetrics.TerraceLerp(a.m_v3, b.m_v3, step);
        result.m_v4 = HexMetrics.TerraceLerp(a.m_v4, b.m_v4, step);
        result.m_v5 = HexMetrics.TerraceLerp(a.m_v5, b.m_v5, step);

        return result;
    }
}