using UnityEngine;
using System.IO;

[System.Serializable]
public struct HexValues
{
#pragma warning disable IDE0044
    private int m_values;
#pragma warning restore IDE0044

    private readonly int Get(int mask, int shift) => (int)((uint)m_values >> shift) & mask;

    private readonly HexValues With(int value, int mask, int shift)
    {
        return new HexValues
        {
            m_values = (m_values & ~(mask << shift)) | ((value & mask) << shift)
        };
    }

    public readonly int Elevation => Get(31, 0) - 15;

    public readonly HexValues WithElevation(int value)
    {
        return With(value + 15, 31, 0);
    }

    public readonly int WaterLevel => Get(31, 5);

    public readonly HexValues WithWaterLevel(int value)
    {
        return With(value, 31, 5);
    }

    public readonly int ViewElevation => Mathf.Max(Elevation, WaterLevel);

    public readonly bool IsUnderwater => WaterLevel > Elevation;

    public readonly int UrbanLevel => Get(3, 10);

    public readonly HexValues WithUrbanLevel(int value)
    {
        return With(value, 3, 10);
    }

    public readonly int FarmLevel => Get(3, 12);

    public readonly HexValues WithFarmLevel(int value)
    {
        return With(value, 3, 12);
    }

    public readonly int PlantLevel => Get(3, 14);

    public readonly HexValues WithPlantLevel(int value)
    {
        return With(value, 3, 14);
    }

    public readonly int SpecialIndex => Get(255, 16);

    public readonly HexValues WithSpecialIndex(int index)
    {
        return With(index, 255, 16);
    }

    public readonly int TerrainTypeIndex => Get(255, 24);

    public readonly HexValues WithTerrainTypeIndex(int index)
    {
        return With(index, 255, 24);
    }

    public readonly void Save(BinaryWriter writer)
    {
        writer.Write((byte)TerrainTypeIndex);
        writer.Write((byte)(Elevation + 127));
        writer.Write((byte)WaterLevel);
        writer.Write((byte)UrbanLevel);
        writer.Write((byte)FarmLevel);
        writer.Write((byte)PlantLevel);
        writer.Write((byte)SpecialIndex);
    }

    public static HexValues Load(BinaryReader reader, int header)
    {
        HexValues values = default;
        values = values.WithTerrainTypeIndex(reader.ReadByte());

        int elevation = reader.ReadByte();

        if (header >= 4)
        {
            elevation -= 127;
        }

        values = values.WithElevation(elevation);
        values = values.WithWaterLevel(reader.ReadByte());
        values = values.WithUrbanLevel(reader.ReadByte());
        values = values.WithFarmLevel(reader.ReadByte());
        values = values.WithPlantLevel(reader.ReadByte());

        return values.WithSpecialIndex(reader.ReadByte());
    }
}