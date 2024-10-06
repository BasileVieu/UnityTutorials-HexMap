using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HexGrid : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI m_cellLabelPrefab;

    [SerializeField] private HexGridChunk m_chunkPrefab;

    [SerializeField] private HexUnit m_unitPrefab;

    [SerializeField] private Texture2D m_noiseSource;

    [SerializeField] private int m_seed;

    private HexGridChunk[] m_chunks;
    private HexGridChunk[] m_cellGridChunks;

    private HexCellSearchData[] m_searchData;

    private HexCellPriorityQueue m_searchFrontier;

    private Transform[] m_columns;

    private RectTransform[] m_cellUIRects;

    private List<HexUnit> m_units = new List<HexUnit>();

    private HexCellShaderData m_cellShaderData;

    private int[] m_cellVisibility;

    private int m_chunkCountX;
    private int m_chunkCountZ;
    private int m_searchFrontierPhase;
    private int m_currentCenterColumnIndex = -1;
    private int m_currentPathFromIndex = -1;
    private int m_currentPathToIndex = -1;

    private bool m_currentPathExists;

    public HexCellShaderData ShaderData => m_cellShaderData;

    public int CellCountX
    {
        get;
        private set;
    }

    public int CellCountZ
    {
        get;
        private set;
    }

    public bool Wrapping
    {
        get;
        private set;
    }

    public HexCellData[] CellData
    {
        get;
        private set;
    }

    public Vector3[] CellPositions
    {
        get;
        private set;
    }

    public HexUnit[] CellUnits
    {
        get;
        private set;
    }

    public bool HasPath => m_currentPathExists;

    public HexCellSearchData[] SearchData => m_searchData;

    private void Awake()
    {
        CellCountX = 20;
        CellCountZ = 15;
        
        HexMetrics.NoiseSource = m_noiseSource;
        HexMetrics.InitializeHashGrid(m_seed);

        HexUnit.m_unitPrefab = m_unitPrefab;

        m_cellShaderData = gameObject.AddComponent<HexCellShaderData>();
        m_cellShaderData.Grid = this;

        CreateMap(CellCountX, CellCountZ, Wrapping);
    }

    private void OnEnable()
    {
        if (!HexMetrics.NoiseSource)
        {
            HexMetrics.NoiseSource = m_noiseSource;
            HexMetrics.InitializeHashGrid(m_seed);

            HexUnit.m_unitPrefab = m_unitPrefab;
            
            HexMetrics.WrapSize = Wrapping ? CellCountX : 0;

            ResetVisibility();
        }

        for (int i = 0; i < m_chunks.Length; i++)
        {
            m_chunks[i].Refresh();
        }
    }

    public bool CreateMap(int x, int z, bool wrapping)
    {
        if (x <= 0
            || x % HexMetrics.ChunkSizeX != 0
            || z <= 0
            || z % HexMetrics.ChunkSizeZ != 0)
        {
            Debug.LogError("Unsupported map size.");

            return false;
        }

        ClearPath();
        ClearUnits();

        if (m_columns != null)
        {
            for (int i = 0; i < m_columns.Length; i++)
            {
                Destroy(m_columns[i].gameObject);
            }
        }

        CellCountX = x;
        CellCountZ = z;
        Wrapping = wrapping;
        m_currentCenterColumnIndex = -1;

        HexMetrics.WrapSize = Wrapping ? CellCountX : 0;

        m_chunkCountX = CellCountX / HexMetrics.ChunkSizeX;
        m_chunkCountZ = CellCountZ / HexMetrics.ChunkSizeZ;

        m_cellShaderData.Initialize(CellCountX, CellCountZ);

        CreateChunks();
        CreateCells();

        return true;
    }

    private void CreateChunks()
    {
        m_columns = new Transform[m_chunkCountX];

        for (int x = 0; x < m_chunkCountX; x++)
        {
            m_columns[x] = new GameObject("Column").transform;
            m_columns[x].SetParent(transform, false);
        }
        
        m_chunks = new HexGridChunk[m_chunkCountX * m_chunkCountZ];

        for (int z = 0, i = 0; z < m_chunkCountZ; z++)
        {
            for (int x = 0; x < m_chunkCountX; x++)
            {
                HexGridChunk chunk = m_chunks[i++] = Instantiate(m_chunkPrefab);
                chunk.transform.SetParent(m_columns[x], false);
                chunk.Grid = this;
            }
        }
    }

    private void CreateCells()
    {
        CellData = new HexCellData[CellCountZ * CellCountX];
        
        CellPositions = new Vector3[CellData.Length];

        m_cellGridChunks = new HexGridChunk[CellData.Length];

        m_cellUIRects = new RectTransform[CellData.Length];

        CellUnits = new HexUnit[CellData.Length];

        m_searchData = new HexCellSearchData[CellData.Length];

        m_cellVisibility = new int[CellData.Length];

        for (int z = 0, i = 0; z < CellCountZ; z++)
        {
            for (int x = 0; x < CellCountX; x++)
            {
                CreateCell(x, z, i++);
            }
        }
    }

    private void CreateCell(int x, int z, int i)
    {
        Vector3 position;
        position.x = (x + z * 0.5f - z / 2) * HexMetrics.InnerDiameter;
        position.y = 0.0f;
        position.z = z * (HexMetrics.OuterRadius * 1.5f);

        HexCell cell = new HexCell(i, this);
        CellPositions[i] = position;
        CellData[i].m_coordinates = HexCoordinates.FromOffsetCoordinates(x, z);

        bool explorable = Wrapping
            ? z > 0 && z < CellCountZ - 1
            : x > 0 && z > 0 && x < CellCountX - 1 && z < CellCountZ - 1;

        cell.Flags = explorable ? cell.Flags.With(HexFlags.Explorable) : cell.Flags.Without(HexFlags.Explorable);

        TextMeshProUGUI label = Instantiate(m_cellLabelPrefab);
        label.rectTransform.anchoredPosition = new Vector2(position.x, position.z);
        
        RectTransform rect = m_cellUIRects[i] = label.rectTransform;

        cell.Values = cell.Values.WithElevation(0);

        RefreshCellPosition(i);

        int chunkX = x / HexMetrics.ChunkSizeX;
        int chunkZ = z / HexMetrics.ChunkSizeZ;

        HexGridChunk chunk = m_chunks[chunkX + chunkZ * m_chunkCountX];

        int localX = x - chunkX * HexMetrics.ChunkSizeX;
        int localZ = z - chunkZ * HexMetrics.ChunkSizeZ;

        m_cellGridChunks[i] = chunk;

        chunk.AddCell(localX + localZ * HexMetrics.ChunkSizeX, i, rect);
    }

    public void RefreshCellPosition(int cellIndex)
    {
        Vector3 position = CellPositions[cellIndex];
        position.y = CellData[cellIndex].Elevation * HexMetrics.ElevationStep;
        position.y += (HexMetrics.SampleNoise(position).y * 2.0f - 1.0f) * HexMetrics.ElevationPerturbStrength;

        CellPositions[cellIndex] = position;

        RectTransform rectTransform = m_cellUIRects[cellIndex];

        Vector3 uiPosition = rectTransform.localPosition;
        uiPosition.z = -position.y;

        rectTransform.localPosition = uiPosition;
    }

    public void RefreshCell(int cellIndex)
    {
        m_cellGridChunks[cellIndex].Refresh();
    }

    public void RefreshCellWithDependents(int cellIndex)
    {
        HexGridChunk chunk = m_cellGridChunks[cellIndex];
        chunk.Refresh();

        HexCoordinates coordinates = CellData[cellIndex].m_coordinates;

        for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
        {
            if (TryGetCellIndex(coordinates.Step(d), out int neighborIndex))
            {
                HexGridChunk neighborChunk = m_cellGridChunks[neighborIndex];

                if (chunk != neighborChunk)
                {
                    neighborChunk.Refresh();
                }
            }
        }

        HexUnit unit = CellUnits[cellIndex];

        if (unit)
        {
            unit.ValidateLocation();
        }
    }

    public void RefreshAllCells()
    {
        for (int i = 0; i < CellData.Length; i++)
        {
            SearchData[i].m_searchPhase = 0;
            
            RefreshCellPosition(i);
            
            ShaderData.RefreshTerrain(i);
            ShaderData.RefreshVisibility(i);
        }
    }

    private HexCell GetCell(Vector3 position)
    {
        position = transform.InverseTransformPoint(position);

        HexCoordinates coordinates = HexCoordinates.FromPosition(position);

        return GetCell(coordinates);
    }

    public HexCell GetCell(HexCoordinates coordinates)
    {
        int z = coordinates.Z;

        int x = coordinates.X + z / 2;

        if (z < 0
            || z >= CellCountZ
            || x < 0
            || x >= CellCountX)
        {
            return default;
        }

        return new HexCell(x + z * CellCountX,this);
    }

    public HexCell GetCell(Ray ray)
    {
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            return GetCell(hit.point);
        }

        return default;
    }

    public int GetCellIndex(int xOffset, int zOffset)
    {
        return xOffset + zOffset * CellCountX;
    }

    public HexCell GetCell(int index)
    {
        return new HexCell(index, this);
    }

    public bool TryGetCell(HexCoordinates coordinates, out HexCell cell)
    {
        int z = coordinates.Z;
        int x = coordinates.X + z / 2;
        
        if (z < 0
            || z >= CellCountZ
            || x < 0
            || x >= CellCountX)
        {
            cell = default;
            
            return false;
        }

        cell = new HexCell(x + z * CellCountX, this);

        return true;
    }

    public bool TryGetCellIndex(HexCoordinates coordinates, out int cellIndex)
    {
        int z = coordinates.Z;
        int x = coordinates.X + z / 2;
        
        if (z < 0
            || z >= CellCountZ
            || x < 0
            || x >= CellCountX)
        {
            cellIndex = -1;
            
            return false;
        }

        cellIndex = x + z * CellCountX;

        return true;
    }

    public void ShowUI(bool visible)
    {
        for (int i = 0; i < m_chunks.Length; i++)
        {
            m_chunks[i].ShowUI(visible);
        }
    }

    public void Save(BinaryWriter writer)
    {
        writer.Write(CellCountX);
        writer.Write(CellCountZ);
        writer.Write(Wrapping);

        for (int i = 0; i < CellData.Length; i++)
        {
            HexCellData data = CellData[i];
            
            data.m_values.Save(writer);
            data.m_flags.Save(writer);
        }
        
        writer.Write(m_units.Count);

        for (int i = 0; i < m_units.Count; i++)
        {
            m_units[i].Save(writer);
        }
    }

    public void Load(BinaryReader reader, int header)
    {
        ClearPath();
        ClearUnits();
        
        int x = 20;
        int z = 15;

        if (header >= 1)
        {
            x = reader.ReadInt32();
            z = reader.ReadInt32();
        }

        bool wrapping = header >= 5 && reader.ReadBoolean();

        if (x != CellCountX
            || z != CellCountZ
            || Wrapping != wrapping)
        {
            if (!CreateMap(x, z, Wrapping))
            {
                return;
            }
        }

        bool originalImmediateMode = m_cellShaderData.ImmediateMode;

        m_cellShaderData.ImmediateMode = true;

        for (int i = 0; i < CellData.Length; i++)
        {
            HexCellData data = CellData[i];

            data.m_values = HexValues.Load(reader, header);
            data.m_flags = data.m_flags.Load(reader, header);

            CellData[i] = data;

            RefreshCellPosition(i);

            ShaderData.RefreshTerrain(i);
            ShaderData.RefreshVisibility(i);
        }

        for (int i = 0; i < m_chunks.Length; i++)
        {
            m_chunks[i].Refresh();
        }

        if (header >= 2)
        {
            int unitCount = reader.ReadInt32();

            for (int i = 0; i < unitCount; i++)
            {
                HexUnit.Load(reader, this);
            }
        }

        m_cellShaderData.ImmediateMode = originalImmediateMode;
    }

    public List<int> GetPath()
    {
        if (!m_currentPathExists)
        {
            return null;
        }

        List<int> path = ListPool<int>.Get();

        for (int i = m_currentPathToIndex; i != m_currentPathFromIndex; i = m_searchData[i].m_pathFrom)
        {
            path.Add(i);
        }
        
        path.Add(m_currentPathFromIndex);

        path.Reverse();

        return path;
    }

    private void ShowPath(int speed)
    {
        if (m_currentPathExists)
        {
            int currentIndex = m_currentPathToIndex;

            while (currentIndex != m_currentPathFromIndex)
            {
                int turn = (m_searchData[currentIndex].m_distance - 1) / speed;
                
                SetLabel(currentIndex, turn.ToString());
                EnableHighlight(currentIndex, Color.white);
                
                currentIndex = m_searchData[currentIndex].m_pathFrom;
            }
        }

        EnableHighlight(m_currentPathFromIndex, Color.blue);
        EnableHighlight(m_currentPathToIndex, Color.red);
    }

    public void ClearPath()
    {
        if (m_currentPathExists)
        {
            int currentIndex = m_currentPathToIndex;

            while (currentIndex != m_currentPathFromIndex)
            {
                SetLabel(currentIndex, null);
                
                DisableHighlight(currentIndex);
                
                currentIndex = m_searchData[currentIndex].m_pathFrom;
            }
            
            DisableHighlight(currentIndex);
            
            m_currentPathExists = false;
        }
        else if (m_currentPathFromIndex >= 0)
        {
            DisableHighlight(m_currentPathFromIndex);
            DisableHighlight(m_currentPathToIndex);
        }

        m_currentPathFromIndex = m_currentPathToIndex = -1;
    }

    public void FindPath(HexCell fromCell, HexCell toCell, HexUnit unit)
    {
        ClearPath();
        
        m_currentPathFromIndex = fromCell.Index;

        m_currentPathToIndex = toCell.Index;

        m_currentPathExists = Search(fromCell, toCell, unit);

        ShowPath(unit.Speed);
    }

    private bool Search(HexCell fromCell, HexCell toCell, HexUnit unit)
    {
        int speed = unit.Speed;
        
        m_searchFrontierPhase += 2;
        
        m_searchFrontier ??= new HexCellPriorityQueue(this);
        
        m_searchFrontier.Clear();

        m_searchData[fromCell.Index] = new HexCellSearchData
        {
            m_searchPhase = m_searchFrontierPhase
        };

        m_searchFrontier.Enqueue(fromCell.Index);

        while (m_searchFrontier.TryDequeue(out int currentIndex))
        {
            HexCell current = new HexCell(currentIndex, this);

            int currentDistance = m_searchData[currentIndex].m_distance;
            
            m_searchData[currentIndex].m_searchPhase += 1;

            if (current == toCell)
            {
                return true;
            }

            int currentTurn = (currentDistance - 1) / speed;

            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                if (!current.TryGetNeighbor(d, out HexCell neighbor))
                {
                    continue;
                }

                HexCellSearchData neighborData = m_searchData[neighbor.Index];

                if (neighborData.m_searchPhase > m_searchFrontierPhase
                    || !unit.IsValidDestination(neighbor))
                {
                    continue;
                }

                int moveCost = unit.GetMoveCost(current, neighbor, d);

                if (moveCost < 0)
                {
                    continue;
                }

                int distance = currentDistance + moveCost;

                int turn = (distance - 1) / speed;

                if (turn > currentTurn)
                {
                    distance = turn * speed + moveCost;
                }

                if (neighborData.m_searchPhase < m_searchFrontierPhase)
                {
                    m_searchData[neighbor.Index] = new HexCellSearchData
                    {
                        m_searchPhase = m_searchFrontierPhase,
                        m_distance = distance,
                        m_pathFrom = current.Index,
                        m_heuristic = neighbor.Coordinates.DistanceTo(toCell.Coordinates)
                    };

                    m_searchFrontier.Enqueue(neighbor.Index);
                }
                else if (distance < neighborData.m_distance)
                {
                    m_searchData[neighbor.Index].m_distance = distance;
                    m_searchData[neighbor.Index].m_pathFrom = currentIndex;
                    
                    m_searchFrontier.Change(neighbor.Index, neighborData.SearchPriority);
                }
            }
        }

        return false;
    }

    public void AddUnit(HexUnit unit, HexCell location, float orientation)
    {
        m_units.Add(unit);

        unit.Grid = this;
        unit.Location = location;
        unit.Orientation = orientation;
    }

    public void RemoveUnit(HexUnit unit)
    {
        m_units.Remove(unit);

        unit.Die();
    }

    private void ClearUnits()
    {
        for (int i = 0; i < m_units.Count; i++)
        {
            m_units[i].Die();
        }

        m_units.Clear();
    }

    private List<HexCell> GetVisibleCells(HexCell fromCell, int range)
    {
        List<HexCell> visibleCells = ListPool<HexCell>.Get();

        m_searchFrontierPhase += 2;
        
        m_searchFrontier ??= new HexCellPriorityQueue(this);
        
        m_searchFrontier.Clear();
        
        range += fromCell.Values.ViewElevation;
        
        m_searchData[fromCell.Index] = new HexCellSearchData
        {
            m_searchPhase = m_searchFrontierPhase,
            m_pathFrom = m_searchData[fromCell.Index].m_pathFrom
        };
        
        m_searchFrontier.Enqueue(fromCell.Index);
        
        HexCoordinates fromCoordinates = fromCell.Coordinates;
        
        while (m_searchFrontier.TryDequeue(out int currentIndex))
        {
            HexCell current = new HexCell(currentIndex, this);
            
            m_searchData[currentIndex].m_searchPhase += 1;
            
            visibleCells.Add(current);

            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                if (!current.TryGetNeighbor(d, out HexCell neighbor))
                {
                    continue;
                }
                
                HexCellSearchData neighborData = m_searchData[neighbor.Index];
                
                if (neighborData.m_searchPhase > m_searchFrontierPhase
                    || neighbor.Flags.HasNone(HexFlags.Explorable))
                {
                    continue;
                }

                int distance = m_searchData[currentIndex].m_distance + 1;
                
                if (distance + neighbor.Values.ViewElevation > range
                    || distance > fromCoordinates.DistanceTo(neighbor.Coordinates))
                {
                    continue;
                }

                if (neighborData.m_searchPhase < m_searchFrontierPhase)
                {
                    m_searchData[neighbor.Index] = new HexCellSearchData
                    {
                        m_searchPhase = m_searchFrontierPhase,
                        m_distance = distance,
                        m_pathFrom = neighborData.m_pathFrom
                    };
                    
                    m_searchFrontier.Enqueue(neighbor.Index);
                }
                else if (distance < m_searchData[neighbor.Index].m_distance)
                {
                    m_searchData[neighbor.Index].m_distance = distance;
                    
                    m_searchFrontier.Change(neighbor.Index, neighborData.SearchPriority);
                }
            }
        }
        return visibleCells;
    }

    public void IncreaseVisibility(HexCell fromCell, int range)
    {
        List<HexCell> cells = GetVisibleCells(fromCell, range);

        for (int i = 0; i < cells.Count; i++)
        {
            int cellIndex = cells[i].Index;
            
            if (++m_cellVisibility[cellIndex] == 1)
            {
                HexCell c = cells[i];

                c.Flags = c.Flags.With(HexFlags.Explored);
                
                m_cellShaderData.RefreshVisibility(cellIndex);
            }
        }

        ListPool<HexCell>.Add(cells);
    }

    public void DecreaseVisibility(HexCell fromCell, int range)
    {
        List<HexCell> cells = GetVisibleCells(fromCell, range);

        for (int i = 0; i < cells.Count; i++)
        {
            int cellIndex = cells[i].Index;
            
            if (--m_cellVisibility[cellIndex] == 0)
            {
                m_cellShaderData.RefreshVisibility(cellIndex);
            }
        }

        ListPool<HexCell>.Add(cells);
    }

    public void ResetVisibility()
    {
        for (int i = 0; i < m_cellVisibility.Length; i++)
        {
            if (m_cellVisibility[i] > 0)
            {
                m_cellVisibility[i] = 0;
                
                m_cellShaderData.RefreshVisibility(i);
            }
        }

        for (int i = 0; i < m_units.Count; i++)
        {
            HexUnit unit = m_units[i];
            
            IncreaseVisibility(unit.Location, unit.VisionRange);
        }
    }

    public void CenterMap(float xPosition)
    {
        int centerColumnIndex = (int)(xPosition / (HexMetrics.InnerDiameter * HexMetrics.ChunkSizeX));

        if (centerColumnIndex == m_currentCenterColumnIndex)
        {
            return;
        }

        m_currentCenterColumnIndex = centerColumnIndex;

        int minColumnIndex = centerColumnIndex - m_chunkCountX / 2;
        int maxColumnIndex = centerColumnIndex + m_chunkCountX / 2;

        Vector3 position;
        position.y = position.z = 0.0f;

        if (m_columns == null)
        {
            return;
        }

        for (int i = 0; i < m_columns.Length; i++)
        {
            if (i < minColumnIndex)
            {
                position.x = m_chunkCountX * (HexMetrics.InnerDiameter * HexMetrics.ChunkSizeX);
            }
            else if (i > maxColumnIndex)
            {
                position.x = m_chunkCountX * -(HexMetrics.InnerDiameter * HexMetrics.ChunkSizeX);
            }
            else
            {
                position.x = 0.0f;
            }

            m_columns[i].localPosition = position;
        }
    }

    public void MakeChildOfColumn(Transform child, int columnIndex)
    {
        child.SetParent(m_columns[columnIndex], false);
    }

    public bool IsCellVisible(int cellIndex)
    {
        return m_cellVisibility[cellIndex] > 0;
    }
    
    private void SetLabel(int cellIndex, string text)
    {
        m_cellUIRects[cellIndex].GetComponent<TextMeshProUGUI>().text = text;
    }

    private void DisableHighlight(int cellIndex)
    {
        m_cellUIRects[cellIndex].GetChild(0).GetComponent<Image>().enabled = false;
    }

    private void EnableHighlight(int cellIndex, Color color)
    {
        Image highlight = m_cellUIRects[cellIndex].GetChild(0).GetComponent<Image>();
        highlight.color = color;
        highlight.enabled = true;
    }
}