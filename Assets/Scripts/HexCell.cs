using UnityEngine;

[System.Serializable]
public struct HexCell
{
#pragma warning disable IDE0044
    private int m_index;

    private HexGrid m_grid;
#pragma warning restore IDE0044

    public HexCell(int index, HexGrid grid)
    {
        m_index = index;
        m_grid = grid;
    }

    public readonly int Index => m_index;
    
    public HexCoordinates Coordinates => m_grid.CellData[m_index].m_coordinates;

    public HexFlags Flags
    {
        get => m_grid.CellData[m_index].m_flags;
        set => m_grid.CellData[m_index].m_flags = value;
    }

    public HexValues Values
    {
        get => m_grid.CellData[m_index].m_values;
        set => m_grid.CellData[m_index].m_values = value;
    }

    public Vector3 Position => m_grid.CellPositions[m_index];

    public HexUnit Unit
    {
        get => m_grid.CellUnits[m_index];
        set => m_grid.CellUnits[m_index] = value;
    }

    public static implicit operator bool(HexCell cell) => cell.m_grid != null;

    public static bool operator ==(HexCell a, HexCell b) => a.m_index == b.m_index && a.m_grid == b.m_grid;

    public static bool operator !=(HexCell a, HexCell b) => a.m_index != b.m_index || a.m_grid != b.m_grid;

    public readonly override bool Equals(object obj) => obj is HexCell cell && this == cell;

    public readonly override int GetHashCode() => m_grid != null ? m_index.GetHashCode() ^ m_grid.GetHashCode() : 0;

    public void SetElevation(int elevation)
    {
        if (Values.Elevation != elevation)
        {
            Values = Values.WithElevation(elevation);

            m_grid.ShaderData.ViewElevationChanged(m_index);
            m_grid.RefreshCellPosition(m_index);

            ValidateRivers();

            HexFlags flags = Flags;

            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                if (flags.HasRoad(d))
                {
                    HexCell neighbor = GetNeighbor(d);

                    if (Mathf.Abs(elevation - neighbor.Values.Elevation) > 1)
                    {
                        RemoveRoad(d);
                    }
                }
            }

            m_grid.RefreshCellWithDependents(m_index);
        }
    }

    public void SetWaterLevel(int waterLevel)
    {
        if (Values.WaterLevel != waterLevel)
        {
            Values = Values.WithWaterLevel(waterLevel);

            m_grid.ShaderData.ViewElevationChanged(m_index);

            ValidateRivers();

            m_grid.RefreshCellWithDependents(m_index);
        }
    }

    public void SetUrbanLevel(int urbanLevel)
    {
        if (Values.UrbanLevel != urbanLevel)
        {
            Values = Values.WithUrbanLevel(urbanLevel);

            Refresh();
        }
    }

    public void SetFarmLevel(int farmLevel)
    {
        if (Values.FarmLevel != farmLevel)
        {
            Values = Values.WithFarmLevel(farmLevel);

            Refresh();
        }
    }

    public void SetPlantLevel(int plantLevel)
    {
        if (Values.PlantLevel != plantLevel)
        {
            Values = Values.WithPlantLevel(plantLevel);

            Refresh();
        }
    }

    public void SetSpecialIndex(int specialIndex)
    {
        if (Values.SpecialIndex != specialIndex
            && Flags.HasNone(HexFlags.River))
        {
            Values = Values.WithSpecialIndex(specialIndex);

            RemoveRoads();

            Refresh();
        }
    }

    public void SetWalled(bool walled)
    {
        HexFlags flags = Flags;

        HexFlags newFlags = walled ? flags.With(HexFlags.Walled) : flags.Without(HexFlags.Walled);

        if (flags != newFlags)
        {
            Flags = newFlags;

            m_grid.RefreshCellWithDependents(m_index);
        }
    }

    public void SetTerrainTypeIndex(int terrainTypeIndex)
    {
        if (Values.TerrainTypeIndex != terrainTypeIndex)
        {
            Values = Values.WithTerrainTypeIndex(terrainTypeIndex);

            m_grid.ShaderData.RefreshTerrain(m_index);
        }
    }

    public HexCell GetNeighbor(HexDirection direction)
    {
        return m_grid.GetCell(Coordinates.Step(direction));
    }

    public bool TryGetNeighbor(HexDirection direction, out HexCell cell)
    {
        return m_grid.TryGetCell(Coordinates.Step(direction), out cell);
    }

    private void Refresh()
    {
        m_grid.RefreshCell(m_index);
    }

    private static bool CanRiverFlow(HexValues from, HexValues to)
    {
        return from.Elevation >= to.Elevation
               || from.WaterLevel == to.Elevation;
    }

    private void RemoveOutgoingRiver()
    {
        if (Flags.HasAny(HexFlags.RiverOut))
        {
            HexCell neighbor = GetNeighbor(Flags.RiverOutDirection());

            Flags = Flags.Without(HexFlags.RiverOut);

            neighbor.Flags = neighbor.Flags.Without(HexFlags.RiverIn);
            neighbor.Refresh();

            Refresh();
        }
    }

    private void RemoveIncomingRiver()
    {
        if (Flags.HasAny(HexFlags.RiverIn))
        {
            HexCell neighbor = GetNeighbor(Flags.RiverInDirection());

            Flags = Flags.Without(HexFlags.RiverIn);

            neighbor.Flags = neighbor.Flags.Without(HexFlags.RiverOut);
            neighbor.Refresh();

            Refresh();
        }
    }

    public void RemoveRiver()
    {
        RemoveOutgoingRiver();
        RemoveIncomingRiver();
    }

    public void SetOutgoingRiver(HexDirection direction)
    {
        if (Flags.HasRiverOut(direction))
        {
            return;
        }

        HexCell neighbor = GetNeighbor(direction);

        if (!CanRiverFlow(Values, neighbor.Values))
        {
            return;
        }
        
        RemoveOutgoingRiver();

        if (Flags.HasRiverIn(direction))
        {
            RemoveIncomingRiver();
        }
        
        Flags = Flags.WithRiverOut(direction);

        Values = Values.WithSpecialIndex(0);
        
        neighbor.RemoveIncomingRiver();

        neighbor.Flags = neighbor.Flags.WithRiverIn(direction.Opposite());
        
        neighbor.Values = neighbor.Values.WithSpecialIndex(0);

        RemoveRoad(direction);
    }

    public void AddRoad(HexDirection direction)
    {
        HexFlags flags = Flags;

        HexCell neighbor = GetNeighbor(direction);
        
        if (!flags.HasRoad(direction)
            && !flags.HasRiver(direction)
            && Values.SpecialIndex == 0
            && neighbor.Values.SpecialIndex == 0
            && Mathf.Abs(Values.Elevation - neighbor.Values.Elevation) <= 1)
        {
            Flags = flags.WithRoad(direction);

            neighbor.Flags = neighbor.Flags.WithRoad(direction.Opposite());
            neighbor.Refresh();
        
            Refresh();
        }
    }

    public void RemoveRoads()
    {
        HexFlags flags = Flags;
        
        for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
        {
            if (flags.HasRoad(d))
            {
                RemoveRoad(d);
            }
        }
    }

    private void RemoveRoad(HexDirection direction)
    {
        Flags = Flags.WithoutRoad(direction);

        HexCell neighbor = GetNeighbor(direction);
        neighbor.Flags = neighbor.Flags.WithoutRoad(direction.Opposite());
        neighbor.Refresh();
        
        Refresh();
    }

    private void ValidateRivers()
    {
        HexFlags flags = Flags;
        
        if (flags.HasAny(HexFlags.RiverOut)
            && !CanRiverFlow(Values, GetNeighbor(flags.RiverOutDirection()).Values))
        {
            RemoveOutgoingRiver();
        }

        if (flags.HasAny(HexFlags.RiverIn)
            && !CanRiverFlow(GetNeighbor(flags.RiverInDirection()).Values, Values))
        {
            RemoveIncomingRiver();
        }
    }
}