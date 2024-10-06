[System.Serializable]
public struct HexCellSearchData
{
    public int m_distance;
    public int m_nextWithSamePriority;
    public int m_pathFrom;
    public int m_heuristic;
    public int m_searchPhase;

    public readonly int SearchPriority => m_distance + m_heuristic;
}