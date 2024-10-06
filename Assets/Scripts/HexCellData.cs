[System.Serializable]
public struct HexCellData
{
    public HexFlags m_flags;
    public HexValues m_values;

    public HexCoordinates m_coordinates;

    public readonly int Elevation => m_values.Elevation;

    public readonly int WaterLevel => m_values.WaterLevel;

    public readonly int TerrainTypeIndex => m_values.TerrainTypeIndex;

    public readonly int UrbanLevel => m_values.UrbanLevel;

    public readonly int FarmLevel => m_values.FarmLevel;

    public readonly int PlantLevel => m_values.PlantLevel;

    public readonly int SpecialIndex => m_values.SpecialIndex;

    public readonly bool Walled => m_flags.HasAny(HexFlags.Walled);

    public readonly bool HasRoads => m_flags.HasAny(HexFlags.Roads);

    public readonly bool IsExplored => m_flags.HasAll(HexFlags.Explored | HexFlags.Explorable);

    public readonly bool IsSpecial => SpecialIndex > 0;

    public readonly bool IsUnderwater => WaterLevel > Elevation;

    public readonly bool HasIncomingRiver => m_flags.HasAny(HexFlags.RiverIn);

    public readonly bool HasOutgoingRiver => m_flags.HasAny(HexFlags.RiverOut);

    public readonly bool HasRiver => m_flags.HasAny(HexFlags.River);

    public readonly bool HasRiverBeginOrEnd => HasIncomingRiver != HasOutgoingRiver;

    public readonly HexDirection IncomingRiver => m_flags.RiverInDirection();

    public readonly HexDirection OutgoingRiver => m_flags.RiverOutDirection();

    public readonly float StreamBedY => (m_values.Elevation + HexMetrics.StreamBedElevationOffset) * HexMetrics.ElevationStep;

    public readonly float RiverSurfaceY => (m_values.Elevation + HexMetrics.WaterElevationOffset) * HexMetrics.ElevationStep;

    public readonly float WaterSurfaceY => (m_values.WaterLevel + HexMetrics.WaterElevationOffset) * HexMetrics.ElevationStep;

    public readonly int ViewElevation => Elevation >= WaterLevel ? Elevation : WaterLevel;

    public readonly HexEdgeType GetEdgeType(HexCellData otherCell)
    {
        return HexMetrics.GetEdgeType(m_values.Elevation, otherCell.m_values.Elevation);
    }

    public readonly bool HasIncomingRiverThroughEdge(HexDirection direction)
    {
        return m_flags.HasRiverIn(direction);
    }

    public readonly bool HasRiverThroughEdge(HexDirection direction)
    {
        return m_flags.HasRiverIn(direction) || m_flags.HasRiverOut(direction);
    }

    public readonly bool HasRoadThroughEdge(HexDirection direction)
    {
        return m_flags.HasRoad(direction);
    }
}