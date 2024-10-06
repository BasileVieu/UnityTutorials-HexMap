using System.Collections.Generic;
using UnityEngine;

public class HexCellShaderData : MonoBehaviour
{
    private Texture2D m_cellTexture;

    private Color32[] m_cellTextureData;

    private List<int> m_transitioningCellIndices = new List<int>();

    private bool m_needsVisibilityReset;
    private bool[] m_visibilityTransitions;

    private const float m_transitionSpeed = 255.0f;
    
    private static readonly int HexCellData = Shader.PropertyToID("_HexCellData");
    private static readonly int HexCellDataTexelSize = Shader.PropertyToID("_HexCellData_TexelSize");

    public HexGrid Grid
    {
        get;
        set;
    }

    public bool ImmediateMode
    {
        get;
        set;
    }

    private void LateUpdate()
    {
        if (m_needsVisibilityReset)
        {
            m_needsVisibilityReset = false;

            Grid.ResetVisibility();
        }
        
        int delta = (int)(Time.deltaTime * m_transitionSpeed);

        if (delta == 0)
        {
            delta = 1;
        }

        for (int i = 0; i < m_transitioningCellIndices.Count; i++)
        {
            if (!UpdateCellData(m_transitioningCellIndices[i], delta))
            {
                m_transitioningCellIndices[i--] = m_transitioningCellIndices[^1];
                
                m_transitioningCellIndices.RemoveAt(m_transitioningCellIndices.Count - 1);
            }
        }
        
        m_cellTexture.SetPixels32(m_cellTextureData);
        m_cellTexture.Apply();

        enabled = m_transitioningCellIndices.Count > 0;
    }

    public void Initialize(int x, int z)
    {
        if (m_cellTexture)
        {
            m_cellTexture.Reinitialize(x, z);
        }
        else
        {
            m_cellTexture = new Texture2D(x, z, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp
            };

            Shader.SetGlobalTexture(HexCellData, m_cellTexture);
        }
        
        Shader.SetGlobalVector(HexCellDataTexelSize, new Vector4(1.0f / x, 1.0f / z, x, z));

        if (m_cellTextureData == null
            || m_cellTextureData.Length != x * z)
        {
            m_cellTextureData = new Color32[x * z];

            m_visibilityTransitions = new bool[x * z];
        }
        else
        {
            for (int i = 0; i < m_cellTextureData.Length; i++)
            {
                m_cellTextureData[i] = new Color32(0, 0, 0, 0);

                m_visibilityTransitions[i] = false;
            }
        }
        
        m_transitioningCellIndices.Clear();

        enabled = true;
    }

    private bool UpdateCellData(int index, int delta)
    {
        Color32 data = m_cellTextureData[index];
        
        bool stillUpdating = false;

        if (Grid.CellData[index].IsExplored
            && data.g < 255)
        {
            stillUpdating = true;

            int t = data.g + delta;

            data.g = t >= 255 ? (byte)255 : (byte)t;
        }

        if (Grid.IsCellVisible(index))
        {
            if (data.r < 255)
            {
                stillUpdating = true;

                int t = data.r + delta;

                data.r = t >= 255 ? (byte)255 : (byte)t;
            }
        }
        else if (data.r > 0)
        {
            stillUpdating = true;

            int t = data.r - delta;

            data.r = t < 0 ? (byte)0 : (byte)t;
        }

        if (!stillUpdating)
        {
            m_visibilityTransitions[index] = false;
        }

        m_cellTextureData[index] = data;

        return stillUpdating;
    }

    public void RefreshTerrain(int cellIndex)
    {
        HexCellData cell = Grid.CellData[cellIndex];
        
        Color32 data = m_cellTextureData[cellIndex];
        data.b = cell.IsUnderwater ? (byte)(cell.WaterSurfaceY * (255.0f / 30.0f)) : (byte)0;
        data.a = (byte)cell.TerrainTypeIndex;
        
        m_cellTextureData[cellIndex] = data;

        enabled = true;
    }

    public void RefreshVisibility(int cellIndex)
    {
        if (ImmediateMode)
        {
            m_cellTextureData[cellIndex].r = Grid.IsCellVisible(cellIndex) ? (byte)255 : (byte)0;
            m_cellTextureData[cellIndex].g = Grid.CellData[cellIndex].IsExplored ? (byte)255 : (byte)0;
        }
        else if (!m_visibilityTransitions[cellIndex])
        {
            m_visibilityTransitions[cellIndex] = true;
            
            m_transitioningCellIndices.Add(cellIndex);
        }

        enabled = true;
    }

    public void ViewElevationChanged(int cellIndex)
    {
        HexCellData cell = Grid.CellData[cellIndex];
        
        m_cellTextureData[cellIndex].b = cell.IsUnderwater ? (byte)(cell.WaterSurfaceY * (255.0f / 30.0f)) : (byte)0;
        
        m_needsVisibilityReset = true;

        enabled = true;
    }
}