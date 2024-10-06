using System.Collections.Generic;
using UnityEngine;

public class HexMapGenerator : MonoBehaviour
{
    private struct MapRegion
    {
        public int m_xMin;
        public int m_xMax;
        public int m_zMin;
        public int m_zMax;
    }

    private struct ClimateData
    {
        public float m_clouds;
        public float m_moisture;
    }

    private struct Biome
    {
        public int m_terrain;
        public int m_plant;

        public Biome(int terrain, int plant)
        {
            m_terrain = terrain;
            m_plant = plant;
        }
    }

    public enum HemisphereMode
    {
        Both,
        North,
        South
    }

    private static float[] s_temperatureBands = { 0.1f, 0.3f, 0.6f };

    private static float[] s_moistureBands = { 0.12f, 0.28f, 0.85f };

    private static Biome[] s_biomes =
    {
        new Biome(0, 0), new Biome(4, 0), new Biome(4, 0), new Biome(4, 0),
        new Biome(0, 0), new Biome(2, 0), new Biome(2, 1), new Biome(2, 2),
        new Biome(0, 0), new Biome(1, 0), new Biome(1, 1), new Biome(1, 2),
        new Biome(0, 0), new Biome(1, 1), new Biome(1, 2), new Biome(1, 3)
    };
    
    [SerializeField] private HexGrid m_grid;

    [SerializeField] private bool m_useFixedSeed;
    [SerializeField] private int m_seed;
    [SerializeField] [Range(0.0f, 0.5f)] private float m_jitterProbability = 0.25f;
    [SerializeField] [Range(20, 200)] private int m_chunkSizeMin = 30;
    [SerializeField] [Range(20, 200)] private int m_chunkSizeMax = 100;
    [SerializeField] [Range(0.0f, 1.0f)] private float m_highRiseProbability = 0.25f;
    [SerializeField] [Range(0.0f, 0.4f)] private float m_sinkProbability = 0.2f;
    [SerializeField] [Range(5, 95)] private int m_landPercentage = 50;
    [SerializeField] [Range(1, 5)] private int m_waterLevel = 3;
    [SerializeField] [Range(-4, 0)] private int m_elevationMinimum = -2;
    [SerializeField] [Range(-6, 10)] private int m_elevationMaximum = 8;
    [SerializeField] [Range(0, 10)] private int m_mapBorderX = 5;
    [SerializeField] [Range(0, 10)] private int m_mapBorderZ = 5;
    [SerializeField] [Range(0, 10)] private int m_regionBorder = 5;
    [SerializeField] [Range(1, 4)] private int m_regionCount = 1;
    [SerializeField] [Range(0, 100)] private int m_erosionPercentage = 50;
    [SerializeField] [Range(0.0f, 1.0f)] private float m_startingMoisture = 0.1f;
    [SerializeField] [Range(0.0f, 1.0f)] private float m_evaporationFactor = 0.5f;
    [SerializeField] [Range(0.0f, 1.0f)] private float m_precipitationFactor = 0.25f;
    [SerializeField] [Range(0.0f, 1.0f)] private float m_runoffFactor = 0.25f;
    [SerializeField] [Range(0.0f, 1.0f)] private float m_seepageFactor = 0.125f;
    [SerializeField] private HexDirection m_windDirection = HexDirection.NW;
    [SerializeField] [Range(1.0f, 10.0f)] private float m_windStrength = 4.0f;
    [SerializeField] [Range(0, 20)] private int m_riverPercentage = 10;
    [SerializeField] [Range(0.0f, 1.0f)] private float m_extraLakeProbability = 0.25f;
    [SerializeField] [Range(0.0f, 1.0f)] private float m_lowTemperature;
    [SerializeField] [Range(0.0f, 1.0f)] private float m_highTemperature = 1.0f;
    [SerializeField] private HemisphereMode m_hemisphere;
    [SerializeField] [Range(0.0f, 1.0f)] private float m_temperatureJitter = 0.1f;

    private HexCellPriorityQueue m_searchFrontier;

    private List<MapRegion> m_regions;

    private List<ClimateData> m_climate = new List<ClimateData>();
    private List<ClimateData> m_nextClimate = new List<ClimateData>();

    private List<HexDirection> m_flowDirections = new List<HexDirection>();

    private int m_cellCount;
    private int m_landCells;
    private int m_searchFrontierPhase;
    private int m_temperatureJitterChannel;
    
    public void GenerateMap(int x, int z, bool wrapping)
    {
        Random.State originalRandomState = Random.state;

        if (!m_useFixedSeed)
        {
            m_seed = Random.Range(0, int.MaxValue);
            m_seed ^= (int)System.DateTime.Now.Ticks;
            m_seed ^= (int)Time.unscaledTime;
            m_seed &= int.MaxValue;
        }

        Random.InitState(m_seed);
        
        m_cellCount = x * z;

        m_grid.CreateMap(x, z, wrapping);

        m_searchFrontier ??= new HexCellPriorityQueue(m_grid);

        for (int i = 0; i < m_cellCount; i++)
        {
            m_grid.CellData[i].m_values = m_grid.CellData[i].m_values.WithWaterLevel(m_waterLevel);
        }

        CreateRegions();
        
        CreateLand();
        
        ErodeLand();

        CreateClimate();

        CreateRivers();

        SetTerrainType();

        m_grid.RefreshAllCells();

        Random.state = originalRandomState;
    }

    private void CreateLand()
    {
        int landBudget = Mathf.RoundToInt(m_cellCount * m_landPercentage * 0.01f);

        m_landCells = landBudget;

        for (int guard = 0; guard < 10000; guard++)
        {
            bool sink = Random.value < m_sinkProbability;
            
            for (int i = 0; i < m_regions.Count; i++)
            {
                MapRegion region = m_regions[i];

                int chunkSize = Random.Range(m_chunkSizeMin, m_chunkSizeMax + 1);

                if (sink)
                {
                    landBudget = SinkTerrain(chunkSize, landBudget, region);
                }
                else
                {
                    landBudget = RaiseTerrain(chunkSize, landBudget, region);

                    if (landBudget == 0)
                    {
                        return;
                    }
                }
            }
        }

        if (landBudget > 0)
        {
            Debug.LogWarning("Failed to use up " + landBudget + " land budget.");

            m_landCells -= landBudget;
        }
    }

    private void CreateRegions()
    {
        if (m_regions == null)
        {
            m_regions = new List<MapRegion>();
        }
        else
        {
            m_regions.Clear();
        }

        int borderX = m_grid.Wrapping ? m_regionBorder : m_mapBorderX;

        MapRegion region;

        switch (m_regionCount)
        {
            default:
                if (m_grid.Wrapping)
                {
                    borderX = 0;
                }
                
                region.m_xMin = borderX;
                region.m_xMax = m_grid.CellCountX - borderX;
                region.m_zMin = m_mapBorderZ;
                region.m_zMax = m_grid.CellCountZ - m_mapBorderZ;

                m_regions.Add(region);

                break;

            case 2:
                if (Random.value < 0.5f)
                {
                    region.m_xMin = borderX;
                    region.m_xMax = m_grid.CellCountX / 2 - m_regionBorder;
                    region.m_zMin = m_mapBorderZ;
                    region.m_zMax = m_grid.CellCountZ - m_mapBorderZ;

                    m_regions.Add(region);

                    region.m_xMin = m_grid.CellCountX / 2 + m_regionBorder;
                    region.m_xMax = m_grid.CellCountX - borderX;

                    m_regions.Add(region);
                }
                else
                {
                    if (m_grid.Wrapping)
                    {
                        borderX = 0;
                    }
                    
                    region.m_xMin = borderX;
                    region.m_xMax = m_grid.CellCountX - borderX;
                    region.m_zMin = m_mapBorderZ;
                    region.m_zMax = m_grid.CellCountZ / 2 - m_regionBorder;

                    m_regions.Add(region);

                    region.m_zMin = m_grid.CellCountZ / 2 + m_regionBorder;
                    region.m_zMax = m_grid.CellCountZ - m_mapBorderZ;

                    m_regions.Add(region);
                }

                break;
            
            case 3:
                region.m_xMin = borderX;
                region.m_xMax = m_grid.CellCountX / 3 - m_regionBorder;
                region.m_zMin = m_mapBorderZ;
                region.m_zMax = m_grid.CellCountZ - m_mapBorderZ;

                m_regions.Add(region);

                region.m_xMin = m_grid.CellCountX / 3 + m_regionBorder;
                region.m_xMax = m_grid.CellCountX * 2 / 3 - m_regionBorder;

                m_regions.Add(region);

                region.m_xMin = m_grid.CellCountX * 2 / 3 + m_regionBorder;
                region.m_xMax = m_grid.CellCountX - borderX;

                m_regions.Add(region);

                break;
            
            case 4:
                region.m_xMin = borderX;
                region.m_xMax = m_grid.CellCountX / 2 - m_regionBorder;
                region.m_zMin = m_mapBorderZ;
                region.m_zMax = m_grid.CellCountZ / 2 - m_regionBorder;

                m_regions.Add(region);

                region.m_xMin = m_grid.CellCountX / 2 + m_regionBorder;
                region.m_xMax = m_grid.CellCountX - borderX;

                m_regions.Add(region);

                region.m_zMin = m_grid.CellCountZ / 2 + m_regionBorder;
                region.m_zMax = m_grid.CellCountZ - m_mapBorderZ;

                m_regions.Add(region);

                region.m_xMin = borderX;
                region.m_xMax = m_grid.CellCountX / 2 - m_regionBorder;

                m_regions.Add(region);

                break;
        }
    }

    private void CreateClimate()
    {
        m_climate.Clear();
        m_nextClimate.Clear();

        ClimateData initialData = new ClimateData
        {
            m_moisture = m_startingMoisture
        };

        ClimateData clearData = new ClimateData();

        for (int i = 0; i < m_cellCount; i++)
        {
            m_climate.Add(initialData);
            m_nextClimate.Add(clearData);
        }

        for (int cycle = 0; cycle < 40; cycle++)
        {
            for (int i = 0; i < m_cellCount; i++)
            {
                EvolveClimate(i);
            }

            (m_climate, m_nextClimate) = (m_nextClimate, m_climate);
        }
    }

    private void CreateRivers()
    {
        List<int> riverOrigins = ListPool<int>.Get();

        for (int i = 0; i < m_cellCount; i++)
        {
            HexCellData cell = m_grid.CellData[i];

            if (cell.IsUnderwater)
            {
                continue;
            }

            ClimateData data = m_climate[i];

            float weight = data.m_moisture * (cell.Elevation - m_waterLevel) / (m_elevationMaximum - m_waterLevel);

            if (weight > 0.75f)
            {
                riverOrigins.Add(i);
                riverOrigins.Add(i);
            }

            if (weight > 0.5f)
            {
                riverOrigins.Add(i);
            }

            if (weight > 0.25f)
            {
                riverOrigins.Add(i);
            }
        }

        int riverBudget = Mathf.RoundToInt(m_landCells * m_riverPercentage * 0.01f);

        while (riverBudget > 0
               && riverOrigins.Count > 0)
        {
            int index = Random.Range(0, riverOrigins.Count);
            int lastIndex = riverOrigins.Count - 1;
            int originIndex = riverOrigins[index];

            HexCellData origin = m_grid.CellData[originIndex];

            riverOrigins[index] = riverOrigins[lastIndex];

            riverOrigins.RemoveAt(lastIndex);

            if (!origin.HasRiver)
            {
                bool isValidOrigin = true;

                for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                {
                    if (m_grid.TryGetCellIndex(origin.m_coordinates.Step(d), out int neighborIndex)
                        && (m_grid.CellData[neighborIndex].HasRiver
                            || m_grid.CellData[neighborIndex].IsUnderwater))
                    {
                        isValidOrigin = false;

                        break;
                    }
                }

                if (isValidOrigin)
                {
                    riverBudget -= CreateRiver(originIndex);
                }
            }
        }

        if (riverBudget > 0)
        {
            Debug.LogWarning("Failed to use up river budget.");
        }

        ListPool<int>.Add(riverOrigins);
    }

    private int CreateRiver(int originIndex)
    {
        int length = 1;
        int cellIndex = originIndex;

        HexCellData cell = m_grid.CellData[cellIndex];

        HexDirection direction = HexDirection.NE;

        while (!cell.IsUnderwater)
        {
            int minNeighborElevation = int.MaxValue;
            
            m_flowDirections.Clear();

            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                if (!m_grid.TryGetCellIndex(cell.m_coordinates.Step(d), out int neighborIndex))
                {
                    continue;
                }

                HexCellData neighbor = m_grid.CellData[neighborIndex];

                if (neighbor.Elevation < minNeighborElevation)
                {
                    minNeighborElevation = neighbor.Elevation;
                }
                
                if (neighborIndex == originIndex
                    || neighbor.HasIncomingRiver)
                {
                    continue;
                }

                int delta = neighbor.Elevation - cell.Elevation;

                if (delta > 0)
                {
                    continue;
                }

                if (neighbor.HasOutgoingRiver)
                {
                    m_grid.CellData[cellIndex].m_flags = cell.m_flags.WithRiverOut(d);
                    m_grid.CellData[neighborIndex].m_flags = neighbor.m_flags.WithRiverIn(d.Opposite());

                    return length;
                }

                if (delta < 0)
                {
                    m_flowDirections.Add(d);
                    m_flowDirections.Add(d);
                    m_flowDirections.Add(d);
                }

                if (length == 1
                    || (d != direction.Next2()
                        && d != direction.Previous2()))
                {
                    m_flowDirections.Add(d);
                }

                m_flowDirections.Add(d);
            }

            if (m_flowDirections.Count == 0)
            {
                if (length == 1)
                {
                    return 0;
                }

                if (minNeighborElevation >= cell.Elevation)
                {
                    cell.m_values = cell.m_values.WithWaterLevel(minNeighborElevation);

                    if (minNeighborElevation == cell.Elevation)
                    {
                        cell.m_values = cell.m_values.WithElevation(minNeighborElevation - 1);
                    }

                    m_grid.CellData[cellIndex].m_values = cell.m_values;
                }

                break;
            }
            
            direction = m_flowDirections[Random.Range(0, m_flowDirections.Count)];

            cell.m_flags = cell.m_flags.WithRiverOut(direction);

            m_grid.TryGetCellIndex(cell.m_coordinates.Step(direction), out int outIndex);

            m_grid.CellData[outIndex].m_flags = m_grid.CellData[outIndex].m_flags.WithRiverIn(direction.Opposite());

            length += 1;

            if (minNeighborElevation >= cell.Elevation
                && Random.value < m_extraLakeProbability)
            {
                cell.m_values = cell.m_values.WithWaterLevel(cell.Elevation);
                cell.m_values = cell.m_values.WithElevation(cell.Elevation - 1);
            }

            m_grid.CellData[cellIndex] = cell;

            cellIndex = outIndex;

            cell = m_grid.CellData[cellIndex];
        }

        return length;
    }

    private int RaiseTerrain(int chunkSize, int budget, MapRegion region)
    {
        m_searchFrontierPhase += 1;

        int firstCellIndex = GetRandomCellIndex(region);

        m_grid.SearchData[firstCellIndex] = new HexCellSearchData
        {
            m_searchPhase = m_searchFrontierPhase
        };
        
        m_searchFrontier.Enqueue(firstCellIndex);

        HexCoordinates center = m_grid.CellData[firstCellIndex].m_coordinates;

        int rise = Random.value < m_highRiseProbability ? 2 : 1;

        int size = 0;

        while (size < chunkSize
               && m_searchFrontier.TryDequeue(out int index))
        {
            HexCellData current = m_grid.CellData[index];

            int originalElevation = current.Elevation;
            
            int newElevation = originalElevation + rise;

            if (newElevation > m_elevationMaximum)
            {
                continue;
            }

            m_grid.CellData[index].m_values = current.m_values.WithElevation(newElevation);

            if (originalElevation < m_waterLevel
                && newElevation >= m_waterLevel
                && --budget == 0)
            {
                break;
            }

            size += 1;

            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                if (m_grid.TryGetCellIndex(current.m_coordinates.Step(d), out int neighborIndex)
                    && m_grid.SearchData[neighborIndex].m_searchPhase < m_searchFrontierPhase)
                {
                    m_grid.SearchData[neighborIndex] = new HexCellSearchData
                    {
                        m_searchPhase = m_searchFrontierPhase,
                        m_distance = m_grid.CellData[neighborIndex].m_coordinates.DistanceTo(center),
                        m_heuristic = Random.value < m_jitterProbability ? 1 : 0
                    };
                    
                    m_searchFrontier.Enqueue(neighborIndex);
                }
            }
        }

        m_searchFrontier.Clear();

        return budget;
    }
    
    private int SinkTerrain(int chunkSize, int budget, MapRegion region)
    {
        m_searchFrontierPhase += 1;

        int firstCellIndex = GetRandomCellIndex(region);

        m_grid.SearchData[firstCellIndex] = new HexCellSearchData
        {
            m_searchPhase = m_searchFrontierPhase
        };
        
        m_searchFrontier.Enqueue(firstCellIndex);

        HexCoordinates center = m_grid.CellData[firstCellIndex].m_coordinates;

        int sink = Random.value < m_highRiseProbability ? 2 : 1;

        int size = 0;

        while (size < chunkSize
               && m_searchFrontier.TryDequeue(out int index))
        {
            HexCellData current = m_grid.CellData[index];

            int originalElevation = current.Elevation;
            
            int newElevation = current.Elevation - sink;

            if (newElevation < m_elevationMinimum)
            {
                continue;
            }

            m_grid.CellData[index].m_values = current.m_values.WithElevation(newElevation);

            if (originalElevation >= m_waterLevel
                && newElevation < m_waterLevel)
            {
                budget += 1;
            }

            size += 1;

            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                if (m_grid.TryGetCellIndex(current.m_coordinates.Step(d), out int neighborIndex)
                    && m_grid.SearchData[neighborIndex].m_searchPhase < m_searchFrontierPhase)
                {
                    m_grid.SearchData[neighborIndex] = new HexCellSearchData
                    {
                        m_searchPhase = m_searchFrontierPhase,
                        m_distance = m_grid.CellData[neighborIndex].m_coordinates.DistanceTo(center),
                        m_heuristic = Random.value < m_jitterProbability ? 1 : 0
                    };
                    
                    m_searchFrontier.Enqueue(neighborIndex);
                }
            }
        }

        m_searchFrontier.Clear();

        return budget;
    }

    private void ErodeLand()
    {
        List<int> erodibleIndices = ListPool<int>.Get();

        for (int i = 0; i < m_cellCount; i++)
        {
            if (IsErodible(i, m_grid.CellData[i].Elevation))
            {
                erodibleIndices.Add(i);
            }
        }

        int targetErodibleCount = (int)(erodibleIndices.Count * (100 - m_erosionPercentage) * 0.01f);

        while (erodibleIndices.Count > targetErodibleCount)
        {
            int index = Random.Range(0, erodibleIndices.Count);
            int cellIndex = erodibleIndices[index];

            HexCellData cell = m_grid.CellData[cellIndex];
            
            int targetCellIndex = GetErosionTarget(cellIndex, cell.Elevation);

            m_grid.CellData[cellIndex].m_values = cell.m_values = cell.m_values.WithElevation(cell.Elevation - 1);

            HexCellData targetCell = m_grid.CellData[targetCellIndex];

            m_grid.CellData[targetCellIndex].m_values = targetCell.m_values = targetCell.m_values.WithElevation(targetCell.Elevation + 1);

            if (!IsErodible(cellIndex, cell.Elevation))
            {
                int lastIndex = erodibleIndices.Count - 1;
                
                erodibleIndices[index] = erodibleIndices[lastIndex];

                erodibleIndices.RemoveAt(lastIndex);
            }

            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                if (m_grid.TryGetCellIndex(cell.m_coordinates.Step(d), out int neighborIndex)
                    && m_grid.CellData[neighborIndex].Elevation == cell.Elevation + 2
                    && !erodibleIndices.Contains(neighborIndex))
                {
                    erodibleIndices.Add(neighborIndex);
                }
            }

            if (IsErodible(targetCellIndex, targetCell.Elevation)
                && !erodibleIndices.Contains(targetCellIndex))
            {
                erodibleIndices.Add(targetCellIndex);
            }

            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                if (m_grid.TryGetCellIndex(targetCell.m_coordinates.Step(d), out int neighborIndex)
                    && neighborIndex != cellIndex
                    && m_grid.CellData[neighborIndex].Elevation == targetCell.Elevation + 1
                    && !IsErodible(neighborIndex, m_grid.CellData[neighborIndex].Elevation))
                {
                    erodibleIndices.Remove(neighborIndex);
                }
            }
        }

        ListPool<int>.Add(erodibleIndices);
    }

    private void EvolveClimate(int cellIndex)
    {
        HexCellData cell = m_grid.CellData[cellIndex];

        ClimateData cellClimate = m_climate[cellIndex];

        if (cell.IsUnderwater)
        {
            cellClimate.m_moisture = 1.0f;
            cellClimate.m_clouds += m_evaporationFactor;
        }
        else
        {
            float evaporation = cellClimate.m_moisture * m_evaporationFactor;

            cellClimate.m_moisture -= evaporation;
            cellClimate.m_clouds += evaporation;
        }

        float precipitation = cellClimate.m_clouds * m_precipitationFactor;
        cellClimate.m_clouds -= precipitation;
        cellClimate.m_moisture += precipitation;

        float cloudMaximum = 1.0f - cell.ViewElevation / (m_elevationMaximum + 1.0f);

        if (cellClimate.m_clouds > cloudMaximum)
        {
            cellClimate.m_moisture += cellClimate.m_clouds - cloudMaximum;
            cellClimate.m_clouds = cloudMaximum;
        }

        HexDirection mainDispersalDirection = m_windDirection.Opposite();

        float cloudDispersal = cellClimate.m_clouds * (1.0f / (5.0f + m_windStrength));

        float runoff = cellClimate.m_moisture * m_runoffFactor * (1.0f / 6.0f);

        float seepage = cellClimate.m_moisture * m_seepageFactor * (1.0f / 6.0f);

        for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
        {
            if (!m_grid.TryGetCellIndex(cell.m_coordinates.Step(d), out int neighborIndex))
            {
                continue;
            }

            ClimateData neighborClimate = m_nextClimate[neighborIndex];

            if (d == mainDispersalDirection)
            {
                neighborClimate.m_clouds += cloudDispersal * m_windStrength;
            }
            else
            {
                neighborClimate.m_clouds += cloudDispersal;
            }

            int elevationDelta = m_grid.CellData[neighborIndex].ViewElevation - cell.ViewElevation;

            if (elevationDelta < 0)
            {
                cellClimate.m_moisture -= runoff;
                
                neighborClimate.m_moisture += runoff;
            }
            else if (elevationDelta == 0)
            {
                cellClimate.m_moisture -= seepage;
                
                neighborClimate.m_moisture += seepage;
            }

            m_nextClimate[neighborIndex] = neighborClimate;
        }

        ClimateData nextCellClimate = m_nextClimate[cellIndex];
        nextCellClimate.m_moisture += cellClimate.m_moisture;

        if (nextCellClimate.m_moisture > 1.0f)
        {
            nextCellClimate.m_moisture = 1.0f;
        }

        m_nextClimate[cellIndex] = nextCellClimate;

        m_climate[cellIndex] = new ClimateData();
    }

    private bool IsErodible(int cellIndex, int cellElevation)
    {
        int erodibleElevation = cellElevation - 2;

        HexCoordinates coordinates = m_grid.CellData[cellIndex].m_coordinates;

        for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
        {
            if (m_grid.TryGetCellIndex(coordinates.Step(d), out int neighborIndex)
                && m_grid.CellData[neighborIndex].Elevation <= erodibleElevation)
            {
                return true;
            }
        }

        return false;
    }

    private int GetErosionTarget(int cellIndex, int cellElevation)
    {
        List<int> candidates = ListPool<int>.Get();

        int erodibleElevation = cellElevation - 2;

        HexCoordinates coordinates = m_grid.CellData[cellIndex].m_coordinates;

        for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
        {
            if (m_grid.TryGetCellIndex(coordinates.Step(d), out int neighborIndex)
                && m_grid.CellData[neighborIndex].Elevation <= erodibleElevation)
            {
                candidates.Add(neighborIndex);
            }
        }

        int target = candidates[Random.Range(0, candidates.Count)];

        ListPool<int>.Add(candidates);

        return target;
    }

    private float DetermineTemperature(int cellIndex, HexCellData cell)
    {
        float latitude = (float)cell.m_coordinates.Z / m_grid.CellCountZ;

        if (m_hemisphere == HemisphereMode.Both)
        {
            latitude *= 2.0f;

            if (latitude > 1.0f)
            {
                latitude = 2.0f - latitude;
            }
        }
        else if (m_hemisphere == HemisphereMode.North)
        {
            latitude = 1.0f - latitude;
        }

        float temperature = Mathf.LerpUnclamped(m_lowTemperature, m_highTemperature, latitude);

        temperature *= 1.0f - (cell.ViewElevation - m_waterLevel) / (m_elevationMaximum - m_waterLevel + 1.0f);

        float jitter = HexMetrics.SampleNoise(m_grid.CellPositions[cellIndex] * 0.1f)[m_temperatureJitterChannel];

        temperature += (jitter * 2.0f - 1.0f) * m_temperatureJitter;

        return temperature;
    }

    private void SetTerrainType()
    {
        m_temperatureJitterChannel = Random.Range(0, 4);

        int rockDesertElevation = m_elevationMaximum - (m_elevationMaximum - m_waterLevel) / 2;
        
        for (int i = 0; i < m_cellCount; i++)
        {
            HexCellData cell = m_grid.CellData[i];

            float temperature = DetermineTemperature(i, cell);

            float moisture = m_climate[i].m_moisture;

            if (!cell.IsUnderwater)
            {
                int t = 0;

                for (; t < s_temperatureBands.Length; t++)
                {
                    if (temperature < s_temperatureBands[t])
                    {
                        break;
                    }
                }

                int m = 0;
                
                for (; m < s_moistureBands.Length; m++)
                {
                    if (moisture < s_moistureBands[m])
                    {
                        break;
                    }
                }

                Biome cellBiome = s_biomes[t * 4 + m];

                if (cellBiome.m_terrain == 0)
                {
                    if (cell.Elevation >= rockDesertElevation)
                    {
                        cellBiome.m_terrain = 3;
                    }
                }
                else if (cell.Elevation == m_elevationMaximum)
                {
                    cellBiome.m_terrain = 4;
                }

                if (cellBiome.m_terrain == 4)
                {
                    cellBiome.m_plant = 0;
                }
                else if (cellBiome.m_plant < 3
                         && cell.HasRiver)
                {
                    cellBiome.m_plant += 1;
                }

                m_grid.CellData[i].m_values = cell.m_values.WithTerrainTypeIndex(cellBiome.m_terrain).WithPlantLevel(cellBiome.m_plant);
            }
            else
            {
                int terrain;

                if (cell.Elevation == m_waterLevel - 1)
                {
                    int cliffs = 0;
                    int slopes = 0;

                    for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                    {
                        if (!m_grid.TryGetCellIndex(cell.m_coordinates.Step(d), out int neighborIndex))
                        {
                            continue;
                        }

                        int delta = m_grid.CellData[neighborIndex].Elevation - cell.WaterLevel;

                        if (delta == 0)
                        {
                            slopes += 1;
                        }
                        else if (delta > 0)
                        {
                            cliffs += 1;
                        }
                    }

                    if (cliffs + slopes > 3)
                    {
                        terrain = 1;
                    }
                    else if (cliffs > 0)
                    {
                        terrain = 3;
                    }
                    else if (slopes > 0)
                    {
                        terrain = 0;
                    }
                    else
                    {
                        terrain = 1;
                    }
                }
                else if (cell.Elevation >= m_waterLevel)
                {
                    terrain = 1;
                }
                else if (cell.Elevation < 0)
                {
                    terrain = 3;
                }
                else
                {
                    terrain = 2;
                }

                if (terrain == 1
                    && temperature < s_temperatureBands[0])
                {
                    terrain = 2;
                }
                
                m_grid.CellData[i].m_values = cell.m_values.WithTerrainTypeIndex(terrain);
            }
        }
    }

    private int GetRandomCellIndex(MapRegion region)
    {
        return m_grid.GetCellIndex(Random.Range(region.m_xMin, region.m_xMax), Random.Range(region.m_zMin, region.m_zMax));
    }
}