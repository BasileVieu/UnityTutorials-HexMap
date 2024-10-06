using TMPro;
using UnityEngine;

public class SaveLoadItem : MonoBehaviour
{
    public SaveLoadMenu Menu
    {
        get;
        set;
    }

    private string m_mapName;

    public string MapName
    {
        get => m_mapName;
        set
        {
            m_mapName = value;

            transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = value;
        }
    }

    public void Select()
    {
        Menu.SelectItem(m_mapName);
    }
}
