using System;
using System.IO;
using TMPro;
using UnityEngine;

public class SaveLoadMenu : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI m_menuLabel;
    [SerializeField] private TextMeshProUGUI m_actionButtonLabel;

    [SerializeField] private TMP_InputField m_nameInput;

    [SerializeField] private RectTransform m_listContent;

    [SerializeField] private SaveLoadItem m_itemPrefab;
    
    [SerializeField] private HexGrid m_hexGrid;

    private const int m_mapFileVersion = 5;

    private bool m_saveMode;

    public void Open(bool saveMode)
    {
        m_saveMode = saveMode;

        if (m_saveMode)
        {
            m_menuLabel.text = "Save Map";

            m_actionButtonLabel.text = "Save";
        }
        else
        {
            m_menuLabel.text = "Load Map";

            m_actionButtonLabel.text = "Load";
        }

        FillList();
        
        gameObject.SetActive(true);

        HexMapCamera.Locked = true;
    }

    public void Close()
    {
        gameObject.SetActive(false);

        HexMapCamera.Locked = false;
    }

    public void Action()
    {
        string path = GetSelectedPath();

        if (path == null)
        {
            return;
        }

        if (m_saveMode)
        {
            Save(path);
        }
        else
        {
            Load(path);
        }

        Close();
    }

    public void Delete()
    {
        string path = GetSelectedPath();

        if (path == null)
        {
            return;
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        m_nameInput.text = "";

        FillList();
    }

    private void Save(string path)
    {
        using BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create));

        writer.Write(m_mapFileVersion);

        m_hexGrid.Save(writer);
    }

    private void Load(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError("File does not exist " + path);

            return;
        }

        using BinaryReader reader = new BinaryReader(File.OpenRead(path));

        int header = reader.ReadInt32();

        if (header <= m_mapFileVersion)
        {
            m_hexGrid.Load(reader, header);

            HexMapCamera.ValidatePosition();
        }
        else
        {
            Debug.LogWarning("Unknown map format " + header);
        }
    }

    public void SelectItem(string itemName)
    {
        m_nameInput.text = itemName;
    }

    private void FillList()
    {
        for (int i = 0; i < m_listContent.childCount; i++)
        {
            Destroy(m_listContent.GetChild(i).gameObject);
        }
        
        string[] paths = Directory.GetFiles(Application.persistentDataPath, "*.map");
        
        Array.Sort(paths);

        for (int i = 0; i < paths.Length; i++)
        {
            SaveLoadItem item = Instantiate(m_itemPrefab, m_listContent, false);
            item.Menu = this;
            item.MapName = Path.GetFileNameWithoutExtension(paths[i]);
        }
    }

    private string GetSelectedPath()
    {
        string mapName = m_nameInput.text;

        if (mapName.Length == 0)
        {
            return null;
        }

        return Path.Combine(Application.persistentDataPath, mapName + ".map");
    }
}