using UnityEngine;
using UnityEngine.EventSystems;

public class HexGameUI : MonoBehaviour
{
    [SerializeField] private HexGrid m_grid;
    
    private Camera m_mainCamera;
    
    private int m_currentCellIndex = -1;
    
    private HexUnit m_selectedUnit;

    private void Awake()
    {
        m_mainCamera = Camera.main;
    }

    private void Update()
    {
        if (!EventSystem.current.IsPointerOverGameObject())
        {
            if (Input.GetMouseButtonDown(0))
            {
                DoSelection();
            }
            else if (m_selectedUnit)
            {
                if (Input.GetMouseButtonDown(1))
                {
                    DoMove();
                }
                else
                {
                    DoPathfinding();
                }
            }
        }
    }

    public void SetEditMode(bool toggle)
    {
        enabled = !toggle;

        m_grid.ShowUI(!toggle);

        m_grid.ClearPath();

        if (toggle)
        {
            Shader.EnableKeyword("_HEX_MAP_EDIT_MODE");
        }
        else
        {
            Shader.DisableKeyword("_HEX_MAP_EDIT_MODE");
        }
    }

    private bool UpdateCurrentCell()
    {
        HexCell cell = m_grid.GetCell(m_mainCamera.ScreenPointToRay(Input.mousePosition));

        int index = cell ? cell.Index : -1;

        if (index != m_currentCellIndex)
        {
            m_currentCellIndex = index;

            return true;
        }

        return false;
    }

    private void DoSelection()
    {
        m_grid.ClearPath();
        
        UpdateCurrentCell();

        if (m_currentCellIndex >= 0)
        {
            m_selectedUnit = m_grid.GetCell(m_currentCellIndex).Unit;
        }
    }

    private void DoPathfinding()
    {
        if (UpdateCurrentCell())
        {
            if (m_currentCellIndex >= 0
                && m_selectedUnit.IsValidDestination(m_grid.GetCell(m_currentCellIndex)))
            {
                m_grid.FindPath(m_selectedUnit.Location, m_grid.GetCell(m_currentCellIndex), m_selectedUnit);
            }
            else
            {
                m_grid.ClearPath();
            }
        }
    }

    private void DoMove()
    {
        if (m_grid.HasPath)
        {
            m_selectedUnit.Travel(m_grid.GetPath());
            
            m_grid.ClearPath();
        }
    }
}