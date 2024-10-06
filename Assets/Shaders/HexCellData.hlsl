#include "HexMetrics.hlsl"

#define HEX_ANGLED_EDGE_VECTOR float2(1, sqrt(3))

TEXTURE2D(_HexCellData);
SAMPLER(sampler_HexCellData);
float4 _HexCellData_TexelSize;
float4 _CellHighlighting;

float4 FilterCellData(float4 data, bool editMode)
{
    if (editMode)
    {
        data.xy = 1;
    }

    return data;
}

float4 GetCellData(float3 uv2, int index, bool editMode)
{
    float2 uv;
    uv.x = (uv2[index] + 0.5) * _HexCellData_TexelSize.x;

    float row = floor(uv.x);

    uv.x -= row;

    uv.y = (row + 0.5) * _HexCellData_TexelSize.y;

    float4 data = SAMPLE_TEXTURE2D_LOD(_HexCellData, sampler_HexCellData, uv, 0);
    data.w *= 255;

    return FilterCellData(data, editMode);
}

float GetCellData(float2 cellDataCoordinates, bool editMode)
{
    float2 uv = cellDataCoordinates + 0.5;
    uv.x *= _HexCellData_TexelSize.x;
    uv.y *= _HexCellData_TexelSize.y;

    return FilterCellData(SAMPLE_TEXTURE2D_LOD(_HexCellData, sampler_HexCellData, uv, 0), editMode);
}

struct HexGridData
{
    float2 cellCenter;
    float2 cellOffsetCoordinates;
    float2 cellUV;
    float distanceToCenter;
    float distanceSmoothing;

    float Smoothstep01(float threshold)
    {
        return smoothstep(threshold - distanceSmoothing,
            threshold + distanceSmoothing,
            distanceToCenter);
    }

    float Smoothstep10(float threshold)
    {
        return smoothstep(threshold + distanceSmoothing,
            threshold - distanceSmoothing,
            distanceToCenter);
    }

    float SmoothstepRange(float innerThreshold, float outerThreshold)
    {
        return Smoothstep01(innerThreshold) * Smoothstep10(outerThreshold);
    }

    bool IsHighlighted()
    {
        float2 cellToHighlight = abs(_CellHighlighting.xy - cellCenter);

        if (cellToHighlight.x > _CellHighlighting.w * 0.5)
        {
            cellToHighlight.x -= _CellHighlighting.w;
        }

        return dot(cellToHighlight, cellToHighlight) < _CellHighlighting.z;
    }
};

float HexagonalCenterToEdgeDistance(float2 p)
{
    p = abs(p);

    float d = dot(p, normalize(HEX_ANGLED_EDGE_VECTOR));

    d = max(d, p.x);

    return 2 * d;
}

float2 HexModulo(float2 p)
{
    return p - HEX_ANGLED_EDGE_VECTOR * floor(p / HEX_ANGLED_EDGE_VECTOR);
}

HexGridData GetHexGridData(float2 worldPositionXZ)
{
    float2 p = WorldToHexSpace(worldPositionXZ);

    float2 gridOffset = HEX_ANGLED_EDGE_VECTOR * 0.5;

    float2 a = HexModulo(p) - gridOffset;
    float2 b = HexModulo(p - gridOffset) - gridOffset;

    bool aIsNearest = dot(a, a) < dot(b, b);

    float2 vectorFromCenterToPosition = aIsNearest ? a : b;

    HexGridData d;
    d.cellCenter = p - vectorFromCenterToPosition;
    d.cellOffsetCoordinates.x = d.cellCenter.x - (aIsNearest ? 0.5 : 0.0);
    d.cellOffsetCoordinates.y = d.cellCenter.y / OUTER_TO_INNER;
    d.cellUV = vectorFromCenterToPosition + 0.5;
    d.distanceToCenter = HexagonalCenterToEdgeDistance(vectorFromCenterToPosition);
    d.distanceSmoothing = fwidth(d.distanceToCenter);

    return d;
}

float3 ApplyGrid(float3 baseColor, HexGridData h)
{
    return baseColor * (0.2 + 0.8 * h.Smoothstep10(0.965));
}