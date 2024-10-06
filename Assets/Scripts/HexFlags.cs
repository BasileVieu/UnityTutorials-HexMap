using System.IO;

[System.Flags]
public enum HexFlags
{
    Empty = 0,
    
    RoadNE = 0b000001,
    RoadE = 0b000010,
    RoadSE = 0b000100,
    RoadSW = 0b001000,
    RoadW = 0b010000,
    RoadNW = 0b100000,
    Roads = 0b111111,
    
    RiverInNE = 0b000001_000000,
    RiverInE = 0b000010_000000,
    RiverInSE = 0b000100_000000,
    RiverInSW = 0b001000_000000,
    RiverInW = 0b010000_000000,
    RiverInNW = 0b100000_000000,
    RiverIn = 0b000001_000000,
    
    RiverOutNE = 0b000001_000000_000000,
    RiverOutE = 0b000010_000000_000000,
    RiverOutSE = 0b000100_000000_000000,
    RiverOutSW = 0b001000_000000_000000,
    RiverOutW = 0b010000_000000_000000,
    RiverOutNW = 0b100000_000000_000000,
    RiverOut = 0b000001_000000_000000,
    
    River = 0b111111_111111_000000,
    
    Walled = 0b1_000000_000000_000000,
    
    Explored = 0b010_000000_000000_000000,
    Explorable = 0b100_000000_000000_000000
}

public static class HexFlagsExtensions
{
    public static bool HasAny(this HexFlags flags, HexFlags mask) => (flags & mask) != 0;

    public static bool HasAll(this HexFlags flags, HexFlags mask) => (flags & mask) == mask;

    public static bool HasNone(this HexFlags flags, HexFlags mask) => (flags & mask) == 0;

    public static HexFlags With(this HexFlags flags, HexFlags mask) => flags | mask;

    public static HexFlags Without(this HexFlags flags, HexFlags mask) => flags & ~mask;

    private static bool Has(this HexFlags flags, HexFlags start, HexDirection direction) =>
        ((int)flags & ((int)start << (int)direction)) != 0;

    private static HexFlags With(this HexFlags flags, HexFlags start, HexDirection direction) =>
        flags | (HexFlags)((int)start << (int)direction);

    private static HexFlags Without(this HexFlags flags, HexFlags start, HexDirection direction) =>
        flags & ~(HexFlags)((int)start << (int)direction);
    
    public static bool HasRoad(this HexFlags flags, HexDirection direction) => flags.Has(HexFlags.RoadNE, direction);
    
    public static HexFlags WithRoad(this HexFlags flags, HexDirection direction) => flags.With(HexFlags.RoadNE, direction);
    
    public static HexFlags WithoutRoad(this HexFlags flags, HexDirection direction) => flags.Without(HexFlags.RoadNE, direction);

    public static bool HasRiver(this HexFlags flags, HexDirection direction) => flags.HasRiver(direction) || flags.HasRiverOut(direction);
    
    public static bool HasRiverIn(this HexFlags flags, HexDirection direction) => flags.Has(HexFlags.RiverInNE, direction);
    
    public static HexFlags WithRiverIn(this HexFlags flags, HexDirection direction) => flags.With(HexFlags.RiverInNE, direction);
    
    public static HexFlags WithoutRiverIn(this HexFlags flags, HexDirection direction) => flags.Without(HexFlags.RiverInNE, direction);
    
    public static bool HasRiverOut(this HexFlags flags, HexDirection direction) => flags.Has(HexFlags.RiverOutNE, direction);
    
    public static HexFlags WithRiverOut(this HexFlags flags, HexDirection direction) => flags.With(HexFlags.RiverOutNE, direction);
    
    public static HexFlags WithoutRiverOut(this HexFlags flags, HexDirection direction) => flags.Without(HexFlags.RiverOutNE, direction);

    private static HexDirection ToDirection(this HexFlags flags, int shift)
    {
        return (((int)flags >> shift) & 0b111111) switch
        {
            0b000001 => HexDirection.NE,
            0b000010 => HexDirection.E,
            0b000100 => HexDirection.SE,
            0b001000 => HexDirection.SW,
            0b010000 => HexDirection.W,
            _ => HexDirection.NW
        };
    }

    public static HexDirection RiverInDirection(this HexFlags flags)
    {
        return flags.ToDirection(6);
    }

    public static HexDirection RiverOutDirection(this HexFlags flags)
    {
        return flags.ToDirection(12);
    }

    public static void Save(this HexFlags flags, BinaryWriter writer)
    {
        writer.Write(flags.HasAny(HexFlags.Walled));

        if (flags.HasAny(HexFlags.RiverIn))
        {
            writer.Write((byte)(flags.RiverInDirection() + 128));
        }
        else
        {
            writer.Write((byte)0);
        }
        
        if (flags.HasAny(HexFlags.RiverOut))
        {
            writer.Write((byte)(flags.RiverOutDirection() + 128));
        }
        else
        {
            writer.Write((byte)0);
        }
        
        writer.Write((byte)(flags & HexFlags.Roads));
        writer.Write(flags.HasAll(HexFlags.Explored | HexFlags.Explorable));
    }

    public static HexFlags Load(this HexFlags basis, BinaryReader reader, int header)
    {
        HexFlags flags = basis & HexFlags.Explorable;

        if (reader.ReadBoolean())
        {
            flags = flags.With(HexFlags.Walled);
        }

        byte riverData = reader.ReadByte();

        if (riverData >= 128)
        {
            flags = flags.WithRiverIn((HexDirection)(riverData - 128));
        }

        riverData = reader.ReadByte();

        if (riverData >= 128)
        {
            flags = flags.WithRiverOut((HexDirection)(riverData - 128));
        }

        flags |= (HexFlags)reader.ReadByte();

        if (header >= 3
            && reader.ReadBoolean())
        {
            flags = flags.With(HexFlags.Explored);
        }

        return flags;
    }
}