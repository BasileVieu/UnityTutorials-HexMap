using UnityEngine;

public struct HexHash
{
    public float m_a;
    public float m_b;
    public float m_c;
    public float m_d;
    public float m_e;

    public static HexHash Create()
    {
        HexHash hash;
        hash.m_a = Random.value * 0.999f;
        hash.m_b = Random.value * 0.999f;
        hash.m_c = Random.value * 0.999f;
        hash.m_d = Random.value * 0.999f;
        hash.m_e = Random.value * 0.999f;

        return hash;
    }
}