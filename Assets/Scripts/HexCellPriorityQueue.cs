using System.Collections.Generic;

public class HexCellPriorityQueue
{
    private readonly List<int> m_list = new List<int>();

    private readonly HexGrid m_grid;

    private int m_minimum = int.MaxValue;

    public HexCellPriorityQueue(HexGrid grid)
    {
        m_grid = grid;
    }

    public void Enqueue(int cellIndex)
    {
        int priority = m_grid.SearchData[cellIndex].SearchPriority;

        if (priority < m_minimum)
        {
            m_minimum = priority;
        }

        while (priority >= m_list.Count)
        {
            m_list.Add(-1);
        }

        m_grid.SearchData[cellIndex].m_nextWithSamePriority = m_list[priority];

        m_list[priority] = cellIndex;
    }

    public bool TryDequeue(out int cellIndex)
    {
        for (; m_minimum < m_list.Count; m_minimum++)
        {
            cellIndex = m_list[m_minimum];

            if (cellIndex >= 0)
            {
                m_list[m_minimum] = m_grid.SearchData[cellIndex].m_nextWithSamePriority;
                
                return true;
            }
        }

        cellIndex = -1;
        
        return false;
    }

    public void Change(int cellIndex, int oldPriority)
    {
        int current = m_list[oldPriority];
        int next = m_grid.SearchData[cellIndex].m_nextWithSamePriority;

        if (current == cellIndex)
        {
            m_list[oldPriority] = next;
        }
        else
        {
            while (next != cellIndex)
            {
                current = next;

                next = m_grid.SearchData[cellIndex].m_nextWithSamePriority;
            }

            m_grid.SearchData[cellIndex].m_nextWithSamePriority = m_grid.SearchData[cellIndex].m_nextWithSamePriority;
        }

        Enqueue(cellIndex);
    }

    public void Clear()
    {
        m_list.Clear();

        m_minimum = int.MaxValue;
    }
}