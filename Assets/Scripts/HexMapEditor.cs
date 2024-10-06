using UnityEngine;
using UnityEngine.EventSystems;

public class HexMapEditor : MonoBehaviour
{
	private enum OptionalToggle
	{
		Ignore,
		Yes,
		No
	}

	[SerializeField] private HexGrid m_hexGrid;

	[SerializeField] private Material m_terrainMaterial;

	private Camera m_mainCamera;

	private HexDirection m_dragDirection;

	private int m_previousCellIndex = -1;

	private int m_activeElevation;
	private int m_activeTerrainTypeIndex;
	private int m_activeWaterLevel;
	private int m_activeUrbanLevel;
	private int m_activeFarmLevel;
	private int m_activePlantLevel;
	private int m_activeSpecialIndex;
	private int m_brushSize;

	private bool m_applyElevation = true;
	private bool m_applyWaterLevel = true;
	private bool m_applyUrbanLevel;
	private bool m_applyFarmLevel;
	private bool m_applyPlantLevel;
	private bool m_applySpecialIndex;
	private bool m_isDrag;

	private OptionalToggle m_riverMode;
	private OptionalToggle m_roadMode;
	private OptionalToggle m_walledMode;

	private static int s_cellHighlightingId = Shader.PropertyToID("_CellHighlighting");

	private void Awake()
	{
		m_mainCamera = Camera.main;
		
		m_terrainMaterial.DisableKeyword("_SHOW_GRID");
		
		Shader.EnableKeyword("_HEX_MAP_EDIT_MODE");

		SetEditMode(true);
	}

	private void Update()
	{
		if (!EventSystem.current.IsPointerOverGameObject())
		{
			if (Input.GetMouseButton(0))
			{
				HandleInput();

				return;
			}
			else
			{
				UpdateCellHighlightData(GetCellUnderCursor());
			}

			if (Input.GetKeyDown(KeyCode.U))
			{
				if (Input.GetKey(KeyCode.LeftShift))
				{
					DestroyUnit();
				}
				else
				{
					CreateUnit();
				}

				return;
			}
		}
		else
		{
			ClearCellHighlightData();
		}

		m_previousCellIndex = -1;
	}

	private HexCell GetCellUnderCursor()
	{
		return m_hexGrid.GetCell(m_mainCamera.ScreenPointToRay(Input.mousePosition));
	}

	private void HandleInput()
	{
		HexCell currentCell = GetCellUnderCursor();

		if (currentCell)
		{
			if (m_previousCellIndex >= 0
			    && m_previousCellIndex != currentCell.Index)
			{
				ValidateDrag(currentCell);
			}
			else
			{
				m_isDrag = false;
			}

			EditCells(currentCell);

			m_previousCellIndex = currentCell.Index;
		}
		else
		{
			m_previousCellIndex = -1;
		}
		
		UpdateCellHighlightData(currentCell);
	}

	private void EditCells(HexCell center)
	{
		int centerX = center.Coordinates.X;
		int centerZ = center.Coordinates.Z;

		for (int r = 0, z = centerZ - m_brushSize; z <= centerZ; z++, r++)
		{
			for (int x = centerX - r; x <= centerX + m_brushSize; x++)
			{
				EditCell(m_hexGrid.GetCell(new HexCoordinates(x, z)));
			}
		}

		for (int r = 0, z = centerZ + m_brushSize; z > centerZ; z--, r++)
		{
			for (int x = centerX - m_brushSize; x <= centerX + r; x++)
			{
				EditCell(m_hexGrid.GetCell(new HexCoordinates(x, z)));
			}
		}
	}

	private void EditCell(HexCell cell)
	{
		if (cell)
		{
			if (m_activeTerrainTypeIndex >= 0)
			{
				cell.SetTerrainTypeIndex(m_activeTerrainTypeIndex);
			}

			if (m_applyElevation)
			{
				cell.SetElevation(m_activeElevation);
			}

			if (m_applyWaterLevel)
			{
				cell.SetWaterLevel(m_activeWaterLevel);
			}

			if (m_applySpecialIndex)
			{
				cell.SetSpecialIndex(m_activeSpecialIndex);
			}

			if (m_applyUrbanLevel)
			{
				cell.SetUrbanLevel(m_activeUrbanLevel);
			}

			if (m_applyFarmLevel)
			{
				cell.SetFarmLevel(m_activeFarmLevel);
			}

			if (m_applyPlantLevel)
			{
				cell.SetPlantLevel(m_activePlantLevel);
			}

			if (m_riverMode == OptionalToggle.No)
			{
				cell.RemoveRiver();
			}

			if (m_roadMode == OptionalToggle.No)
			{
				cell.RemoveRoads();
			}

			if (m_walledMode != OptionalToggle.Ignore)
			{
				cell.SetWalled(m_walledMode == OptionalToggle.Yes);
			}

			if (m_isDrag
			    && cell.TryGetNeighbor(m_dragDirection.Opposite(), out HexCell otherCell))
			{
				if (m_riverMode == OptionalToggle.Yes)
				{
					otherCell.SetOutgoingRiver(m_dragDirection);
				}

				if (m_roadMode == OptionalToggle.Yes)
				{
					otherCell.AddRoad(m_dragDirection);
				}
			}
		}
	}

	public void SetApplyElevation(bool toggle)
	{
		m_applyElevation = toggle;
	}

	public void SetElevation(float elevation)
	{
		m_activeElevation = (int)elevation;
	}

	public void SetTerrainTypeIndex(int index)
	{
		m_activeTerrainTypeIndex = index;
	}

	public void SetApplyWaterLevel(bool toggle)
	{
		m_applyWaterLevel = toggle;
	}

	public void SetWaterLevel(float level)
	{
		m_activeWaterLevel = (int)level;
	}

	public void SetApplyUrbanLevel(bool toggle)
	{
		m_applyUrbanLevel = toggle;
	}

	public void SetUrbanLevel(float level)
	{
		m_activeUrbanLevel = (int)level;
	}

	public void SetApplyFarmLevel(bool toggle)
	{
		m_applyFarmLevel = toggle;
	}

	public void SetFarmLevel(float level)
	{
		m_activeFarmLevel = (int)level;
	}

	public void SetApplyPlantLevel(bool toggle)
	{
		m_applyPlantLevel = toggle;
	}

	public void SetPlantLevel(float level)
	{
		m_activePlantLevel = (int)level;
	}

	public void SetApplySpecialIndex(bool toggle)
	{
		m_applySpecialIndex = toggle;
	}

	public void SetSpecialIndex(float index)
	{
		m_activeSpecialIndex = (int)index;
	}

	public void SetBrushSize(float size)
	{
		m_brushSize = (int)size;
	}

	public void SetRiverMode(int mode)
	{
		m_riverMode = (OptionalToggle)mode;
	}

	public void SetRoadMode(int mode)
	{
		m_roadMode = (OptionalToggle)mode;
	}

	public void SetWalledMode(int mode)
	{
		m_walledMode = (OptionalToggle)mode;
	}

	public void ShowGrid(bool visible)
	{
		if (visible)
		{
			m_terrainMaterial.EnableKeyword("_SHOW_GRID");
		}
		else
		{
			m_terrainMaterial.DisableKeyword("_SHOW_GRID");
		}
	}

	public void SetEditMode(bool toggle)
	{
		enabled = toggle;
	}

	private void ValidateDrag(HexCell currentCell)
	{
		for (m_dragDirection = HexDirection.NE; m_dragDirection <= HexDirection.NW; m_dragDirection++)
		{
			if (m_hexGrid.GetCell(m_previousCellIndex).GetNeighbor(m_dragDirection) == currentCell)
			{
				m_isDrag = true;

				return;
			}
		}

		m_isDrag = false;
	}

	private void CreateUnit()
	{
		HexCell cell = GetCellUnderCursor();

		if (cell
		    && !cell.Unit)
		{
			m_hexGrid.AddUnit(Instantiate(HexUnit.m_unitPrefab), cell, Random.Range(0.0f, 360.0f));
		}
	}

	private void DestroyUnit()
	{
		HexCell cell = GetCellUnderCursor();

		if (cell
		    && cell.Unit)
		{
			m_hexGrid.RemoveUnit(cell.Unit);
		}
	}

	private void UpdateCellHighlightData(HexCell cell)
	{
		if (!cell)
		{
			ClearCellHighlightData();

			return;
		}
		
		Shader.SetGlobalVector(s_cellHighlightingId, new Vector4(cell.Coordinates.HexX,
			cell.Coordinates.HexZ,
			m_brushSize * m_brushSize + 0.5f,
			HexMetrics.WrapSize));
	}

	private void ClearCellHighlightData()
	{
		Shader.SetGlobalVector(s_cellHighlightingId, new Vector4(0.0f, 0.0f, -0.1f, 0.0f));
	}
}