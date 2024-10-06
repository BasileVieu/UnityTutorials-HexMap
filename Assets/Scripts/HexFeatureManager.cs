using UnityEngine;

public class HexFeatureManager : MonoBehaviour
{
    [System.Serializable]
    public struct HexFeatureCollection
    {
        public Transform[] m_prefabs;

        public Transform Pick(float choice)
        {
            return m_prefabs[(int)(choice * m_prefabs.Length)];
        }
    }
    
    [SerializeField] private HexFeatureCollection[] m_urbanCollections;
    [SerializeField] private HexFeatureCollection[] m_farmCollections;
    [SerializeField] private HexFeatureCollection[] m_plantCollections;

    [SerializeField] private HexMesh m_walls;

    [SerializeField] private Transform m_wallTower;
    [SerializeField] private Transform m_bridge;

    [SerializeField] private Transform[] m_special;

    private Transform m_container;
    
    public void Clear()
    {
        if (m_container)
        {
            Destroy(m_container.gameObject);
        }

        m_container = new GameObject("Features Container").transform;
        m_container.SetParent(transform, false);

        m_walls.Clear();
    }

    public void Apply()
    {
        m_walls.Apply();
    }

    public void AddFeature(HexCellData cell, Vector3 position)
    {
        if (cell.IsSpecial)
        {
            return;
        }
        
        HexHash hash = HexMetrics.SampleHashGrid(position);

        Transform prefab = PickPrefab(m_urbanCollections, cell.UrbanLevel, hash.m_a, hash.m_d);

        Transform otherPrefab = PickPrefab(m_farmCollections, cell.FarmLevel, hash.m_b, hash.m_d);

        float usedHash = hash.m_a;

        if (prefab)
        {
            if (otherPrefab
                && hash.m_b < hash.m_a)
            {
                prefab = otherPrefab;

                usedHash = hash.m_b;
            }
        }
        else if (otherPrefab)
        {
            prefab = otherPrefab;

            usedHash = hash.m_b;
        }

        otherPrefab = PickPrefab(m_plantCollections, cell.PlantLevel, hash.m_c, hash.m_d);

        if (prefab)
        {
            if (otherPrefab
                && hash.m_c < usedHash)
            {
                prefab = otherPrefab;
            }
        }
        else if (otherPrefab)
        {
            prefab = otherPrefab;
        }
        else
        {
            return;
        }
        
        Transform instance = Instantiate(prefab, m_container, false);
        position.y += instance.localScale.y * 0.5f;
        instance.SetLocalPositionAndRotation(HexMetrics.Perturb(position), Quaternion.Euler(0.0f, 360.0f * hash.m_e, 0.0f));
    }

    private Transform PickPrefab(HexFeatureCollection[] collection, int level, float hash, float choice)
    {
        if (level > 0)
        {
            float[] thresholds = HexMetrics.GetFeatureThresholds(level - 1);

            for (int i = 0; i < thresholds.Length; i++)
            {
                if (hash < thresholds[i])
                {
                    return collection[i].Pick(choice);
                }
            }
        }

        return null;
    }

    private void AddWallSegment(Vector3 nearLeft, Vector3 farLeft, Vector3 nearRight, Vector3 farRight, bool addTower = false)
    {
        nearLeft = HexMetrics.Perturb(nearLeft);
        farLeft = HexMetrics.Perturb(farLeft);
        nearRight = HexMetrics.Perturb(nearRight);
        farRight = HexMetrics.Perturb(farRight);
        
        Vector3 left = HexMetrics.WallLerp(nearLeft, farLeft);
        Vector3 right = HexMetrics.WallLerp(nearRight, farRight);

        Vector3 leftThicknessOffset = HexMetrics.WallThicknessOffset(nearLeft, farLeft);
        Vector3 rightThicknessOffset = HexMetrics.WallThicknessOffset(nearRight, farRight);

        float leftTop = left.y + HexMetrics.WallHeight;
        float rightTop = right.y + HexMetrics.WallHeight;

        Vector3 v1;
        Vector3 v2;
        Vector3 v3;
        Vector3 v4;

        v1 = v3 = left - leftThicknessOffset;
        v2 = v4 = right - rightThicknessOffset;
        v3.y = leftTop;
        v4.y = rightTop;
        
        m_walls.AddQuadUnperturbed(v1, v2, v3, v4);

        Vector3 t1 = v3;
        Vector3 t2 = v4;

        v1 = v3 = left + leftThicknessOffset;
        v2 = v4 = right + rightThicknessOffset;
        v3.y = leftTop;
        v4.y = rightTop;
        
        m_walls.AddQuadUnperturbed(v2, v1, v4, v3);

        m_walls.AddQuadUnperturbed(t1, t2, v3, v4);

        if (addTower)
        {
            Transform towerInstance = Instantiate(m_wallTower, m_container, false);
            towerInstance.transform.localPosition = (left + right) * 0.5f;

            Vector3 rightDirection = right - left;
            rightDirection.y = 0.0f;

            towerInstance.transform.right = rightDirection;
        }
    }

    private void AddWallSegment(Vector3 pivot, HexCellData pivotCell,
        Vector3 left, HexCellData leftCell,
        Vector3 right, HexCellData rightCell)
    {
        if (pivotCell.IsUnderwater)
        {
            return;
        }

        bool hasLeftWall = !leftCell.IsUnderwater
                           && pivotCell.GetEdgeType(leftCell) != HexEdgeType.Cliff;

        bool hasRightWall = !rightCell.IsUnderwater
                           && pivotCell.GetEdgeType(rightCell) != HexEdgeType.Cliff;

        if (hasLeftWall)
        {
            if (hasRightWall)
            {
                bool hasTower = false;

                if (leftCell.Elevation == rightCell.Elevation)
                {
                    HexHash hash = HexMetrics.SampleHashGrid((pivot + left + right) * (1.0f / 3.0f));

                    hasTower = hash.m_e < HexMetrics.WallTowerThreshold;
                }

                AddWallSegment(pivot, left, pivot, right, hasTower);
            }
            else if (leftCell.Elevation < rightCell.Elevation)
            {
                AddWallWedge(pivot, left, right);
            }
            else
            {
                AddWallCap(pivot, left);
            }
        }
        else if (hasRightWall)
        {
            if (rightCell.Elevation < leftCell.Elevation)
            {
                AddWallWedge(right, pivot, left);
            }
            else
            {
                AddWallCap(right, pivot);
            }
        }
    }

    private void AddWallCap(Vector3 near, Vector3 far)
    {
        near = HexMetrics.Perturb(near);
        far = HexMetrics.Perturb(far);

        Vector3 center = HexMetrics.WallLerp(near, far);
        Vector3 thickness = HexMetrics.WallThicknessOffset(near, far);

        Vector3 v1;
        Vector3 v2;
        Vector3 v3;
        Vector3 v4;

        v1 = v3 = center - thickness;
        v2 = v4 = center + thickness;
        v3.y = v4.y = center.y + HexMetrics.WallHeight;

        m_walls.AddQuadUnperturbed(v1, v2, v3, v4);
    }

    private void AddWallWedge(Vector3 near, Vector3 far, Vector3 point)
    {
        near = HexMetrics.Perturb(near);
        far = HexMetrics.Perturb(far);
        point = HexMetrics.Perturb(point);

        Vector3 center = HexMetrics.WallLerp(near, far);
        Vector3 thickness = HexMetrics.WallThicknessOffset(near, far);

        Vector3 v1;
        Vector3 v2;
        Vector3 v3;
        Vector3 v4;
        Vector3 pointTop = point;

        point.y = center.y;

        v1 = v3 = center - thickness;
        v2 = v4 = center + thickness;
        v3.y = v4.y = pointTop.y = center.y + HexMetrics.WallHeight;

        m_walls.AddQuadUnperturbed(v1, point, v3, pointTop);
        m_walls.AddQuadUnperturbed(point, v2, pointTop, v4);
        m_walls.AddTriangleUnperturbed(pointTop, v3, v4);
    }

    public void AddWall(EdgeVertices near, HexCellData nearCell,
        EdgeVertices far, HexCellData farCell,
        bool hasRiver, bool hasRoad)
    {
        if (nearCell.Walled != farCell.Walled
            && !nearCell.IsUnderwater
            && !farCell.IsUnderwater
            && nearCell.GetEdgeType(farCell) != HexEdgeType.Cliff)
        {
            AddWallSegment(near.m_v1, far.m_v1, near.m_v2, far.m_v2);

            if (hasRiver
                || hasRoad)
            {
                AddWallCap(near.m_v2, far.m_v2);
                AddWallCap(far.m_v4, near.m_v4);
            }
            else
            {
                AddWallSegment(near.m_v2, far.m_v2, near.m_v3, far.m_v3);
                AddWallSegment(near.m_v3, far.m_v3, near.m_v4, far.m_v4);
            }

            AddWallSegment(near.m_v4, far.m_v4, near.m_v5, far.m_v5);
        }
    }

    public void AddWall(Vector3 c1, HexCellData cell1,
        Vector3 c2, HexCellData cell2,
        Vector3 c3, HexCellData cell3)
    {
        if (cell1.Walled)
        {
            if (cell2.Walled)
            {
                if (!cell3.Walled)
                {
                    AddWallSegment(c3, cell3, c1, cell1, c2, cell2);
                }
            }
            else if (cell3.Walled)
            {
                AddWallSegment(c2, cell2, c3, cell3, c1, cell1);
            }
            else
            {
                AddWallSegment(c1, cell1, c2, cell2, c3, cell3);
            }
        }
        else if (cell2.Walled)
        {
            if (cell3.Walled)
            {
                AddWallSegment(c1, cell1, c2, cell2, c3, cell3);
            }
            else
            {
                AddWallSegment(c2, cell2, c3, cell3, c1, cell1);
            }
        }
        else if (cell3.Walled)
        {
            AddWallSegment(c3, cell3, c1, cell1, c2, cell2);
        }
    }

    public void AddBridge(Vector3 roadCenter1, Vector3 roadCenter2)
    {
        roadCenter1 = HexMetrics.Perturb(roadCenter1);
        roadCenter2 = HexMetrics.Perturb(roadCenter2);
        
        Transform instance = Instantiate(m_bridge, m_container, false);
        instance.localPosition = (roadCenter1 + roadCenter2) * 0.5f;
        instance.forward = roadCenter2 - roadCenter1;

        float length = Vector3.Distance(roadCenter1, roadCenter2);

        instance.localScale = new Vector3(1.0f, 1.0f, length * (1.0f / HexMetrics.BridgeDesignLength));
    }

    public void AddSpecialFeature(HexCellData cell, Vector3 position)
    {
        Transform instance = Instantiate(m_special[cell.SpecialIndex - 1], m_container, false);

        HexHash hash = HexMetrics.SampleHashGrid(position);
        
        instance.SetLocalPositionAndRotation(HexMetrics.Perturb(position), Quaternion.Euler(0.0f, 360.0f * hash.m_e, 0.0f));
    }
}