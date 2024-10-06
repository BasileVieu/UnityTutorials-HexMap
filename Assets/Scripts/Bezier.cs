using UnityEngine;

public static class Bezier
{
    public static Vector3 GetPoint(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float r = 1.0f - t;

        return r * r * a + 2.0f * r * t * b + t * t * c;
    }

    public static Vector3 GetDerivative(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        return 2.0f * ((1.0f - t) * (b - a) + t * (c - b));
    }
}