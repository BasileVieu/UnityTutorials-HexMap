using UnityEngine;

public enum HexEdgeType
{
	Flat,
	Slope,
	Cliff
}

public static class HexMetrics
{
	public static Texture2D NoiseSource;
	
	public const float OuterToInner = 0.866025404f;

	public const float InnerToOuter = 1.0f / OuterToInner;
	
    public const float OuterRadius = 10.0f;

    public const float InnerRadius = OuterRadius * OuterToInner;

    public const float InnerDiameter = InnerRadius * 2.0f;
    
    public const float SolidFactor = 0.8f;

    public const float BlendFactor = 1.0f - SolidFactor;

    public const float ElevationStep = 3.0f;

    public const int TerracesPerSlope = 2;

    public const int TerraceSteps = TerracesPerSlope * 2 + 1;

    public const float HorizontalTerraceStepSize = 1.0f / TerraceSteps;

    public const float VerticalTerraceStepSize = 1.0f / (TerracesPerSlope + 1);

    public const float CellPerturbStrength = 4.0f;

    public const float ElevationPerturbStrength = 1.5f;

    public const float NoiseScale = 0.003f;

    public const int ChunkSizeX = 5;

    public const int ChunkSizeZ = 5;

    public const float StreamBedElevationOffset = -1.75f;

    public const float WaterElevationOffset = -0.5f;

    public const float WaterFactor = 0.6f;

    public const float WaterBlendFactor = 1.0f - WaterFactor;

    public const int HashGridSize = 256;

    public const float HashGridScale = 0.25f;

	public const float WallHeight = 4.0f;

	public const float WallYOffset = -1.0f;

	public const float WallThickness = 0.75f;

	public const float WallElevationOffset = VerticalTerraceStepSize;

	public const float WallTowerThreshold = 0.5f;

	public const float BridgeDesignLength = 7.0f;

	public static int WrapSize;

	public static bool Wrapping => WrapSize > 0;

    private static Vector3[] s_corners =
    {
	    new Vector3(0.0f, 0.0f, OuterRadius),
	    new Vector3(InnerRadius, 0.0f, 0.5f * OuterRadius),
	    new Vector3(InnerRadius, 0.0f, -0.5f * OuterRadius),
	    new Vector3(0.0f, 0.0f, -OuterRadius),
	    new Vector3(-InnerRadius, 0.0f, -0.5f * OuterRadius),
	    new Vector3(-InnerRadius, 0.0f, 0.5f * OuterRadius),
	    new Vector3(0.0f, 0.0f, OuterRadius)
    };

	private static HexHash[] s_hashGrid;

	private static float[][] s_featureThresholds =
	{
		new []
		{
			0.0f, 0.0f, 0.4f
		},
		new []
		{
			0.0f, 0.4f, 0.6f
		},
		new []
		{
			0.4f, 0.6f, 0.8f
		}
	};

    public static Vector3 GetFirstCorner(HexDirection direction)
    {
	    return s_corners[(int)direction];
    }

    public static Vector3 GetSecondCorner(HexDirection direction)
    {
	    return s_corners[(int)direction + 1];
    }

    public static Vector3 GetFirstSolidCorner(HexDirection direction)
    {
	    return s_corners[(int)direction] * SolidFactor;
    }

    public static Vector3 GetSecondSolidCorner(HexDirection direction)
    {
	    return s_corners[(int)direction + 1] * SolidFactor;
    }

    public static Vector3 GetSolidEdgeMiddle(HexDirection direction)
    {
	    return (s_corners[(int)direction] + s_corners[(int)direction + 1]) * (0.5f * SolidFactor);
    }

    public static Vector3 GetBridge(HexDirection direction)
    {
	    return (s_corners[(int)direction] + s_corners[(int)direction + 1]) * BlendFactor;
    }

    public static Vector3 GetFirstWaterCorner(HexDirection direction)
    {
	    return s_corners[(int)direction] * WaterFactor;
    }

    public static Vector3 GetSecondWaterCorner(HexDirection direction)
    {
	    return s_corners[(int)direction + 1] * WaterFactor;
    }

    public static Vector3 GetWaterBridge(HexDirection direction)
    {
	    return (s_corners[(int)direction] + s_corners[(int)direction + 1]) * WaterBlendFactor;
    }

    public static Vector3 TerraceLerp(Vector3 a, Vector3 b, int step)
    {
	    float h = step * HorizontalTerraceStepSize;

	    a.x += (b.x - a.x) * h;
	    a.z += (b.z - a.z) * h;

	    float v = (step + 1) / 2 * VerticalTerraceStepSize;

	    a.y += (b.y - a.y) * v;
	    
	    return a;
    }

    public static Color TerraceLerp(Color a, Color b, int step)
    {
	    float h = step * HorizontalTerraceStepSize;

	    return Color.Lerp(a, b, h);
    }

    public static HexEdgeType GetEdgeType(int elevation1, int elevation2)
    {
	    if (elevation1 == elevation2)
	    {
		    return HexEdgeType.Flat;
	    }

	    int delta = elevation2 - elevation1;

	    if (delta is 1 or -1)
	    {
		    return HexEdgeType.Slope;
	    }

	    return HexEdgeType.Cliff;
    }

    public static Vector4 SampleNoise(Vector3 position)
    {
	    Vector4 sample = NoiseSource.GetPixelBilinear(position.x * NoiseScale, position.y * NoiseScale);

	    if (Wrapping
	        && position.x < InnerDiameter * 1.5f)
	    {
		    Vector4 sample2 = NoiseSource.GetPixelBilinear((position.x + WrapSize * InnerDiameter) * NoiseScale, position.y * NoiseScale);

		    sample = Vector4.Lerp(sample2, sample, position.x * (1.0f / InnerDiameter) - 0.5f);
	    }

	    return sample;
    }

    public static Vector3 Perturb(Vector3 position)
    {
	    Vector4 sample = SampleNoise(position);

	    position.x += (sample.x * 2.0f - 1.0f) * CellPerturbStrength;
	    position.z += (sample.z * 2.0f - 1.0f) * CellPerturbStrength;

	    return position;
    }

    public static void InitializeHashGrid(int seed)
    {
	    s_hashGrid = new HexHash[HashGridSize * HashGridSize];

	    Random.State currentState = Random.state;
	    
	    Random.InitState(seed);

	    for (int i = 0; i < s_hashGrid.Length; i++)
	    {
		    s_hashGrid[i] = HexHash.Create();
	    }

	    Random.state = currentState;
    }

    public static HexHash SampleHashGrid(Vector3 position)
    {
	    int x = (int)(position.x * HashGridScale) % HashGridSize;

	    if (x < 0)
	    {
		    x += HashGridSize;
	    }
	    
	    int z = (int)(position.z * HashGridScale) % HashGridSize;

	    if (z < 0)
	    {
		    z += HashGridSize;
	    }

	    return s_hashGrid[x + z * HashGridSize];
    }

    public static float[] GetFeatureThresholds(int level)
    {
	    return s_featureThresholds[level];
    }

    public static Vector3 WallThicknessOffset(Vector3 near, Vector3 far)
    {
	    Vector3 offset;
	    offset.x = far.x - near.x;
	    offset.y = 0.0f;
	    offset.z = far.z - near.z;

	    return offset.normalized * (WallThickness * 0.5f);
    }

    public static Vector3 WallLerp(Vector3 near, Vector3 far)
    {
	    near.x += (far.x - near.x) * 0.5f;
	    near.z += (far.z - near.z) * 0.5f;

	    float v = near.y < far.y ? WallElevationOffset : 1.0f - WallElevationOffset;

	    near.y += (far.y - near.y) * v + WallYOffset;

	    return near;
    }
}