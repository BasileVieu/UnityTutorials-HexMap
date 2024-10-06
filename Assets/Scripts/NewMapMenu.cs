using UnityEngine;

public class NewMapMenu : MonoBehaviour
{
    [SerializeField] private HexGrid m_hexGrid;

    [SerializeField] private HexMapGenerator m_mapGenerator;

    private bool m_generateMaps = true;
    private bool m_wrapping = true;

    public void Open()
    {
        gameObject.SetActive(true);

        HexMapCamera.Locked = true;
    }

    public void Close()
    {
        gameObject.SetActive(false);

        HexMapCamera.Locked = false;
    }

    private void CreateMap(int x, int z)
    {
        if (m_generateMaps)
        {
            m_mapGenerator.GenerateMap(x, z, m_wrapping);
        }
        else
        {
            m_hexGrid.CreateMap(x, z, m_wrapping);
        }

        HexMapCamera.ValidatePosition();

        Close();
    }

    public void CreateSmallMap()
    {
        CreateMap(20, 15);
    }

    public void CreateMediumMap()
    {
        CreateMap(40, 30);
    }

    public void CreateLargeMap()
    {
        CreateMap(80, 60);
    }

    public void ToggleMapGeneration(bool toggle)
    {
        m_generateMaps = toggle;
    }

    public void ToggleWrapping(bool toggle)
    {
        m_wrapping = toggle;
    }
}