#define OUTER_TO_INNER 0.866025404
#define OUTER_RADIUS 10
#define CHUNK_SIZE_X 5
#define TILING_SCALE (1 / (CHUNK_SIZE_X * 2 * OUTER_RADIUS / OUTER_TO_INNER))

float2 WorldToHexSpace(float2 p)
{
    return p * (1.0 / (2.0 * OUTER_RADIUS * OUTER_TO_INNER));
}