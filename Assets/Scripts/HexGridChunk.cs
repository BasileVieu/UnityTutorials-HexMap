using UnityEngine;

public class HexGridChunk : MonoBehaviour
{
    private static Color s_weights1 = new Color(1.0f, 0.0f, 0.0f);
    private static Color s_weights2 = new Color(0.0f, 1.0f, 0.0f);
    private static Color s_weights3 = new Color(0.0f, 0.0f, 1.0f);
    
    [SerializeField] private HexMesh m_terrain;
    [SerializeField] private HexMesh m_rivers;
    [SerializeField] private HexMesh m_roads;
    [SerializeField] private HexMesh m_water;
    [SerializeField] private HexMesh m_waterShore;
    [SerializeField] private HexMesh m_estuaries;
    [SerializeField] private HexFeatureManager m_features;
    
    private int[] m_cellIndices;

    private Canvas m_gridCanvas;

    public HexGrid Grid
    {
        get;
        set;
    }

    private void Awake()
    {
        m_gridCanvas = GetComponentInChildren<Canvas>();

        m_cellIndices = new int[HexMetrics.ChunkSizeX * HexMetrics.ChunkSizeZ];
    }

    private void LateUpdate()
    {
        Triangulate();

        enabled = false;
    }

    public void AddCell(int index, int cellIndex, RectTransform cellUI)
    {
        m_cellIndices[index] = cellIndex;

        cellUI.SetParent(m_gridCanvas.transform, false);
    }

    public void Refresh()
    {
        enabled = true;
    }

    public void ShowUI(bool visible)
    {
        m_gridCanvas.gameObject.SetActive(visible);
    }

    private void Triangulate()
    {
        m_terrain.Clear();
        m_rivers.Clear();
        m_roads.Clear();
        m_water.Clear();
        m_waterShore.Clear();
        m_estuaries.Clear();
        m_features.Clear();

        for (int i = 0; i < m_cellIndices.Length; i++)
        {
            Triangulate(m_cellIndices[i]);
        }

        m_terrain.Apply();
        m_rivers.Apply();
        m_roads.Apply();
        m_water.Apply();
        m_waterShore.Apply();
        m_estuaries.Apply();
        m_features.Apply();
    }

    private void Triangulate(int cellIndex)
    {
        HexCellData cell = Grid.CellData[cellIndex];

        Vector3 cellPosition = Grid.CellPositions[cellIndex];
        
        for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
        {
            Triangulate(d, cell, cellIndex, cellPosition);
        }

        if (!cell.IsUnderwater)
        {
            if (!cell.HasRiver
                && !cell.HasRoads)
            {
                m_features.AddFeature(cell, cellPosition);
            }

            if (cell.IsSpecial)
            {
                m_features.AddSpecialFeature(cell, cellPosition);
            }
        }
    }

    private void Triangulate(HexDirection direction, HexCellData cell, int cellIndex, Vector3 center)
    {
        EdgeVertices e = new EdgeVertices(center + HexMetrics.GetFirstSolidCorner(direction),
            center + HexMetrics.GetSecondSolidCorner(direction));

        if (cell.HasRiver)
        {
            if (cell.HasRiverThroughEdge(direction))
            {
                e.m_v3.y = cell.StreamBedY;

                if (cell.HasRiverBeginOrEnd)
                {
                    TriangulateWithRiverBeginOrEnd(cell, cellIndex, center, e);
                }
                else
                {
                    TriangulateWithRiver(direction, cell, cellIndex, center, e);
                }
            }
            else
            {
                TriangulateAdjacentToRiver(direction, cell, cellIndex, center, e);
            }
        }
        else
        {
            TriangulateWithoutRiver(direction, cell, cellIndex, center, e);

            if (!cell.IsUnderwater
                && !cell.HasRoadThroughEdge(direction))
            {
                m_features.AddFeature(cell, (center + e.m_v1 + e.m_v5) * (1.0f / 3.0f));
            }
        }

        if (direction <= HexDirection.SE)
        {
            TriangulateConnection(direction, cell, cellIndex, center.y, e);
        }

        if (cell.IsUnderwater)
        {
            TriangulateWater(direction, cell, cellIndex, center);
        }
    }
    
    private void TriangulateConnection(HexDirection direction, HexCellData cell, int cellIndex, float centerY, EdgeVertices e1)
    {
        if (!Grid.TryGetCellIndex(cell.m_coordinates.Step(direction), out int neighborIndex))
        {
            return;
        }

        HexCellData neighbor = Grid.CellData[neighborIndex];
        
        Vector3 bridge = HexMetrics.GetBridge(direction);
        bridge.y = Grid.CellPositions[neighborIndex].y - centerY;

        EdgeVertices e2 = new EdgeVertices(e1.m_v1 + bridge, e1.m_v5 + bridge);
        
        bool hasRiver = cell.HasRiverThroughEdge(direction);
        bool hasRoad = cell.HasRoadThroughEdge(direction);

        if (hasRiver)
        {
            e2.m_v3.y = neighbor.StreamBedY;

            Vector3 indices;
            indices.x = indices.z = cellIndex;
            indices.y = neighborIndex;

            if (!cell.IsUnderwater)
            {
                if (!neighbor.IsUnderwater)
                {
                    TriangulateRiverQuad(e1.m_v2, e1.m_v4, e2.m_v2, e2.m_v4, cell.RiverSurfaceY,
                        neighbor.RiverSurfaceY, 0.8f, cell.HasIncomingRiverThroughEdge(direction), indices);
                }
                else if (cell.Elevation > neighbor.WaterLevel)
                {
                    TriangulateWaterfallInWater(e1.m_v2, e1.m_v4, e2.m_v2, e2.m_v4, cell.RiverSurfaceY, neighbor.RiverSurfaceY, neighbor.WaterSurfaceY, indices);
                }
            }
            else if (!neighbor.IsUnderwater
                     && neighbor.Elevation > cell.WaterLevel)
            {
                TriangulateWaterfallInWater(e2.m_v4, e2.m_v2, e1.m_v4, e1.m_v2, neighbor.RiverSurfaceY, cell.RiverSurfaceY, cell.WaterSurfaceY, indices);
            }
        }

        if (cell.GetEdgeType(neighbor) == HexEdgeType.Slope)
        {
            TriangulateEdgeTerraces(e1, cellIndex, e2, neighborIndex, hasRoad);
        }
        else
        {
            TriangulateEdgeStrip(e1, s_weights1, cellIndex, e2, s_weights2, neighborIndex, hasRoad);
        }

        m_features.AddWall(e1, cell, e2, neighbor, hasRiver, hasRoad);

        if (direction <= HexDirection.E
            && Grid.TryGetCellIndex(cell.m_coordinates.Step(direction.Next()), out int nextNeighborIndex))
        {
            HexCellData nextNeighbor = Grid.CellData[nextNeighborIndex];
            
            Vector3 v5 = e1.m_v5 + HexMetrics.GetBridge(direction.Next());
            v5.y = Grid.CellPositions[nextNeighborIndex].y;

            if (cell.Elevation <= neighbor.Elevation)
            {
                if (cell.Elevation <= nextNeighbor.Elevation)
                {
                    TriangulateCorner(e1.m_v5, cellIndex, cell,
                        e2.m_v5, neighborIndex, neighbor,
                        v5, nextNeighborIndex, nextNeighbor);
                }
                else
                {
                    TriangulateCorner(v5, nextNeighborIndex, nextNeighbor,
                        e1.m_v5, cellIndex, cell,
                        e2.m_v5, neighborIndex, neighbor);
                }
            }
            else if (neighbor.Elevation <= nextNeighbor.Elevation)
            {
                TriangulateCorner(e2.m_v5, neighborIndex, neighbor,
                    v5, nextNeighborIndex, nextNeighbor,
                    e1.m_v5, cellIndex, cell);
            }
            else
            {
                TriangulateCorner(v5, nextNeighborIndex, nextNeighbor,
                    e1.m_v5, cellIndex, cell,
                    e2.m_v5, neighborIndex, neighbor);
            }
        }
    }

    private void TriangulateEdgeTerraces(EdgeVertices begin, int beginCellIndex,
        EdgeVertices end, int endCellIndex, bool hasRoad)
    {
        EdgeVertices e2 = EdgeVertices.TerraceLerp(begin, end, 1);

        Color w2 = HexMetrics.TerraceLerp(s_weights1, s_weights2, 1);

        float i1 = beginCellIndex;
        float i2 = endCellIndex;

        TriangulateEdgeStrip(begin, s_weights1, i1, e2, w2, i2, hasRoad);

        for (int i = 2; i < HexMetrics.TerraceSteps; i++)
        {
            EdgeVertices e1 = e2;

            Color w1 = w2;

            e2 = EdgeVertices.TerraceLerp(begin, end, i);

            w2 = HexMetrics.TerraceLerp(s_weights1, s_weights2, i);

            TriangulateEdgeStrip(e1, w1, i1, e2, w2, i2, hasRoad);
        }

        TriangulateEdgeStrip(e2, w2, i1, end, s_weights2, i2, hasRoad);
    }

    private void TriangulateCorner(Vector3 bottom, int bottomCellIndex, HexCellData bottomCell,
        Vector3 left, int leftCellIndex, HexCellData leftCell,
        Vector3 right, int rightCellIndex, HexCellData rightCell)
    {
        HexEdgeType leftEdgeType = bottomCell.GetEdgeType(leftCell);
        HexEdgeType rightEdgeType = bottomCell.GetEdgeType(rightCell);

        if (leftEdgeType == HexEdgeType.Slope)
        {
            if (rightEdgeType == HexEdgeType.Slope)
            {
                TriangulateCornerTerraces(bottom, bottomCellIndex,
                    left, leftCellIndex,
                    right, rightCellIndex);
            }
            else if (rightEdgeType == HexEdgeType.Flat)
            {
                TriangulateCornerTerraces(left, leftCellIndex,
                    right, rightCellIndex,
                    bottom, bottomCellIndex);
            }
            else
            {
                TriangulateCornerTerracesCliff(bottom, bottomCellIndex, bottomCell,
                    left, leftCellIndex, bottomCell,
                    right, rightCellIndex, rightCell);
            }
        }
        else if (rightEdgeType == HexEdgeType.Slope)
        {
            if (leftEdgeType == HexEdgeType.Flat)
            {
                TriangulateCornerTerraces(right, rightCellIndex,
                    bottom, bottomCellIndex,
                    left, leftCellIndex);
            }
            else
            {
                TriangulateCornerCliffTerraces(bottom, bottomCellIndex, bottomCell,
                    left, leftCellIndex, bottomCell,
                    right, rightCellIndex, rightCell);
            }
        }
        else if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
        {
            if (leftCell.Elevation < rightCell.Elevation)
            {
                TriangulateCornerCliffTerraces(right, rightCellIndex, rightCell,
                    bottom, bottomCellIndex, bottomCell,
                    left, leftCellIndex, bottomCell);
            }
            else
            {
                TriangulateCornerTerracesCliff(left, leftCellIndex, bottomCell,
                    right, rightCellIndex, rightCell,
                    bottom, bottomCellIndex, bottomCell);
            }
        }
        else
        {
            m_terrain.AddTriangle(bottom, left, right);

            Vector3 indices;
            indices.x = bottomCellIndex;
            indices.y = leftCellIndex;
            indices.z = rightCellIndex;
            
            m_terrain.AddTriangleCellData(indices, s_weights1, s_weights2, s_weights3);
        }
        
        m_features.AddWall(bottom, bottomCell, left, leftCell, right, rightCell);
    }

    private void TriangulateCornerTerraces(Vector3 begin, int beginCellIndex,
        Vector3 left, int leftCellIndex,
        Vector3 right, int rightCellIndex)
    {
        Vector3 v3 = HexMetrics.TerraceLerp(begin, left, 1);
        Vector3 v4 = HexMetrics.TerraceLerp(begin, right, 1);

        Color w3 = HexMetrics.TerraceLerp(s_weights1, s_weights2, 1);
        Color w4 = HexMetrics.TerraceLerp(s_weights1, s_weights3, 1);

        Vector3 indices;
        indices.x = beginCellIndex;
        indices.y = leftCellIndex;
        indices.z = rightCellIndex;
        
        m_terrain.AddTriangle(begin, v3, v4);
        m_terrain.AddTriangleCellData(indices, s_weights1, w3, w4);

        for (int i = 2; i < HexMetrics.TerraceSteps; i++)
        {
            Vector3 v1 = v3;
            Vector3 v2 = v4;

            Color w1 = w3;
            Color w2 = w4;
            
            v3 = HexMetrics.TerraceLerp(begin, left, i);
            v4 = HexMetrics.TerraceLerp(begin, right, i);

            w3 = HexMetrics.TerraceLerp(s_weights1, s_weights2, i);
            w4 = HexMetrics.TerraceLerp(s_weights1, s_weights3, i);

            m_terrain.AddQuad(v1, v2, v3, v4);
            m_terrain.AddQuadCellData(indices, w1, w2, w3, w4);
        }

        m_terrain.AddQuad(v3, v4, left, right);
        m_terrain.AddQuadCellData(indices, w3, w4, s_weights2, s_weights3);
    }

    private void TriangulateCornerTerracesCliff(Vector3 begin, int beginCellIndex, HexCellData beginCell,
        Vector3 left, int leftCellIndex, HexCellData leftCell,
        Vector3 right, int rightCellIndex, HexCellData rightCell)
    {
        float b = 1.0f / (rightCell.Elevation - beginCell.Elevation);

        if (b < 0)
        {
            b = -b;
        }

        Vector3 boundary = Vector3.Lerp(HexMetrics.Perturb(begin), HexMetrics.Perturb(right), b);

        Color boundaryWeights = Color.Lerp(s_weights1, s_weights3, b);

        Vector3 indices;
        indices.x = beginCellIndex;
        indices.y = leftCellIndex;
        indices.z = rightCellIndex;

        TriangulateBoundaryTriangle(begin, s_weights1, left, s_weights2, boundary, boundaryWeights, indices);

        if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
        {
            TriangulateBoundaryTriangle(left, s_weights1, right, s_weights3, boundary, boundaryWeights, indices);
        }
        else
        {
            m_terrain.AddTriangleUnperturbed(HexMetrics.Perturb(left), HexMetrics.Perturb(right), boundary);
            m_terrain.AddTriangleCellData(indices, s_weights2, s_weights3, boundaryWeights);
        }
    }

    private void TriangulateCornerCliffTerraces(Vector3 begin, int beginCellIndex, HexCellData beginCell,
        Vector3 left, int leftCellIndex, HexCellData leftCell,
        Vector3 right, int rightCellIndex, HexCellData rightCell)
    {
        float b = 1.0f / (leftCell.Elevation - beginCell.Elevation);

        if (b < 0)
        {
            b = -b;
        }

        Vector3 boundary = Vector3.Lerp(HexMetrics.Perturb(begin), HexMetrics.Perturb(left), b);

        Color boundaryWeights = Color.Lerp(s_weights1, s_weights2, b);

        Vector3 indices;
        indices.x = beginCellIndex;
        indices.y = leftCellIndex;
        indices.z = rightCellIndex;

        TriangulateBoundaryTriangle(right, s_weights3, begin, s_weights1, boundary, boundaryWeights, indices);

        if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
        {
            TriangulateBoundaryTriangle(left, s_weights2, right, s_weights3, boundary, boundaryWeights, indices);
        }
        else
        {
            m_terrain.AddTriangleUnperturbed(HexMetrics.Perturb(left), HexMetrics.Perturb(right), boundary);
            m_terrain.AddTriangleCellData(indices, s_weights2, s_weights3, boundaryWeights);
        }
    }
    
    private void TriangulateBoundaryTriangle(Vector3 begin, Color beginWeights,
        Vector3 left, Color leftWeights,
        Vector3 boundary, Color boundaryWeights,
        Vector3 indices)
    {
        Vector3 v2 = HexMetrics.Perturb(HexMetrics.TerraceLerp(begin, left, 1));

        Color w2 = HexMetrics.TerraceLerp(beginWeights, leftWeights, 1);
        
        m_terrain.AddTriangleUnperturbed(HexMetrics.Perturb(begin), v2, boundary);
        m_terrain.AddTriangleCellData(indices, beginWeights, w2, boundaryWeights);

        for (int i = 2; i < HexMetrics.TerraceSteps; i++)
        {
            Vector3 v1 = v2;

            Color w1 = w2;

            v2 = HexMetrics.Perturb(HexMetrics.TerraceLerp(begin, left, i));

            w2 = HexMetrics.TerraceLerp(beginWeights, leftWeights, i);
            
            m_terrain.AddTriangleUnperturbed(v1, v2, boundary);
            m_terrain.AddTriangleCellData(indices, w1, w2, boundaryWeights);
        }

        m_terrain.AddTriangleUnperturbed(v2, HexMetrics.Perturb(left), boundary);
        m_terrain.AddTriangleCellData(indices, w2, leftWeights, boundaryWeights);
    }

    private void TriangulateEdgeFan(Vector3 center, EdgeVertices edge, float index)
    {
        m_terrain.AddTriangle(center, edge.m_v1, edge.m_v2);
        m_terrain.AddTriangle(center, edge.m_v2, edge.m_v3);
        m_terrain.AddTriangle(center, edge.m_v3, edge.m_v4);
        m_terrain.AddTriangle(center, edge.m_v4, edge.m_v5);

        Vector3 indices;
        indices.x = indices.y = indices.z = index;
        
        m_terrain.AddTriangleCellData(indices, s_weights1);
        m_terrain.AddTriangleCellData(indices, s_weights1);
        m_terrain.AddTriangleCellData(indices, s_weights1);
        m_terrain.AddTriangleCellData(indices, s_weights1);
    }

    private void TriangulateEdgeStrip(EdgeVertices e1, Color w1, float index1,
        EdgeVertices e2, Color w2, float index2,
        bool hasRoad = false)
    {
        m_terrain.AddQuad(e1.m_v1, e1.m_v2, e2.m_v1, e2.m_v2);
        m_terrain.AddQuad(e1.m_v2, e1.m_v3, e2.m_v2, e2.m_v3);
        m_terrain.AddQuad(e1.m_v3, e1.m_v4, e2.m_v3, e2.m_v4);
        m_terrain.AddQuad(e1.m_v4, e1.m_v5, e2.m_v4, e2.m_v5);
        
        Vector3 indices;
        indices.x = indices.z = index1;
        indices.y = index2;

        m_terrain.AddQuadCellData(indices, w1, w2);
        m_terrain.AddQuadCellData(indices, w1, w2);
        m_terrain.AddQuadCellData(indices, w1, w2);
        m_terrain.AddQuadCellData(indices, w1, w2);

        if (hasRoad)
        {
            TriangulateRoadSegment(e1.m_v2, e1.m_v3, e1.m_v4, e2.m_v2, e2.m_v3, e2.m_v4, w1, w2, indices);
        }
    }

    private void TriangulateWithoutRiver(HexDirection direction, HexCellData cell, int cellIndex, Vector3 center, EdgeVertices e)
    {
        TriangulateEdgeFan(center, e, cellIndex);

        if (cell.HasRoads)
        {
            Vector2 interpolators = GetRoadInterpolators(direction, cell);
            
            TriangulateRoad(center,
                Vector3.Lerp(center, e.m_v1, interpolators.x),
                Vector3.Lerp(center, e.m_v5, interpolators.y),
                e,
                cell.HasRoadThroughEdge(direction),
                cellIndex);
        }
    }

    private void TriangulateWithRiver(HexDirection direction, HexCellData cell, int cellIndex, Vector3 center, EdgeVertices e)
    {
        Vector3 centerL;
        Vector3 centerR;
        
        if (cell.HasRiverThroughEdge(direction.Opposite()))
        {
            centerL = center + HexMetrics.GetFirstSolidCorner(direction.Previous()) * 0.25f;
            centerR = center + HexMetrics.GetSecondSolidCorner(direction.Next()) * 0.25f;
        }
        else if (cell.HasRiverThroughEdge(direction.Next()))
        {
            centerL = center;
            centerR = Vector3.Lerp(center, e.m_v5, 2.0f / 3.0f);
        }
        else if (cell.HasRiverThroughEdge(direction.Previous()))
        {
            centerL = Vector3.Lerp(center, e.m_v1, 2.0f / 3.0f);
            centerR = center;
        }
        else if (cell.HasRiverThroughEdge(direction.Next2()))
        {
            centerL = center;
            centerR = center + HexMetrics.GetSolidEdgeMiddle(direction.Next()) * (0.5f * HexMetrics.InnerToOuter);
        }
        else
        {
            centerL = center + HexMetrics.GetSolidEdgeMiddle(direction.Previous()) * (0.5f * HexMetrics.InnerToOuter);
            centerR = center;
        }

        center = Vector3.Lerp(centerL, centerR, 0.5f);

        EdgeVertices m = new EdgeVertices(
            Vector3.Lerp(centerL, e.m_v1, 0.5f),
            Vector3.Lerp(centerR, e.m_v5, 0.5f),
            1.0f / 6.0f);

        m.m_v3.y = center.y = e.m_v3.y;

        TriangulateEdgeStrip(m, s_weights1, cellIndex, e, s_weights1, cellIndex);
        
        m_terrain.AddTriangle(centerL, m.m_v1, m.m_v2);
        m_terrain.AddQuad(centerL, center, m.m_v2, m.m_v3);
        m_terrain.AddQuad(center, centerR, m.m_v3, m.m_v4);
        m_terrain.AddTriangle(centerR, m.m_v4, m.m_v5);

        Vector3 indices;
        indices.x = indices.y = indices.z = cellIndex;
        
        m_terrain.AddTriangleCellData(indices, s_weights1);
        m_terrain.AddQuadCellData(indices, s_weights1);
        m_terrain.AddQuadCellData(indices, s_weights1);
        m_terrain.AddTriangleCellData(indices, s_weights1);

        if (!cell.IsUnderwater)
        {
            bool reversed = cell.HasIncomingRiverThroughEdge(direction);

            TriangulateRiverQuad(centerL, centerR, m.m_v2, m.m_v4, cell.RiverSurfaceY, 0.4f, reversed, indices);
            TriangulateRiverQuad(m.m_v2, m.m_v4, e.m_v2, e.m_v4, cell.RiverSurfaceY, 0.6f, reversed, indices);
        }
    }

    private void TriangulateWithRiverBeginOrEnd(HexCellData cell, int cellIndex, Vector3 center, EdgeVertices e)
    {
        EdgeVertices m = new EdgeVertices(
            Vector3.Lerp(center, e.m_v1, 0.5f),
            Vector3.Lerp(center, e.m_v5, 0.5f));

        m.m_v3.y = e.m_v3.y;

        TriangulateEdgeStrip(m, s_weights1, cellIndex, e, s_weights1, cellIndex);
        TriangulateEdgeFan(center, m, cellIndex);

        if (!cell.IsUnderwater)
        {
            bool reversed = cell.HasIncomingRiver;

            Vector3 indices;
            indices.x = indices.y = indices.z = cellIndex;

            TriangulateRiverQuad(m.m_v2, m.m_v4, e.m_v2, e.m_v4, cell.RiverSurfaceY, 0.6f, reversed, indices);

            center.y = m.m_v2.y = m.m_v4.y = cell.RiverSurfaceY;

            m_rivers.AddTriangle(center, m.m_v2, m.m_v4);

            if (reversed)
            {
                m_rivers.AddTriangleUV(new Vector2(0.5f, 0.4f), new Vector2(1.0f, 0.2f), new Vector2(0.0f, 0.2f));
            }
            else
            {
                m_rivers.AddTriangleUV(new Vector2(0.5f, 0.4f), new Vector2(0.0f, 0.6f), new Vector2(1.0f, 0.6f));
            }
            
            m_rivers.AddTriangleCellData(indices, s_weights1);
        }
    }

    private void TriangulateAdjacentToRiver(HexDirection direction, HexCellData cell, int cellIndex, Vector3 center, EdgeVertices e)
    {
        if (cell.HasRoads)
        {
            TriangulateRoadAdjacentToRiver(direction, cell, cellIndex, center, e);
        }
        
        if (cell.HasRiverThroughEdge(direction.Next()))
        {
            if (cell.HasRiverThroughEdge(direction.Previous()))
            {
                center += HexMetrics.GetSolidEdgeMiddle(direction) * (HexMetrics.InnerToOuter * 0.5f);
            }
            else if (cell.HasRiverThroughEdge(direction.Previous2()))
            {
                center += HexMetrics.GetFirstSolidCorner(direction) * 0.25f;
            }
        }
        else if (cell.HasRiverThroughEdge(direction.Previous())
                 && cell.HasRiverThroughEdge(direction.Next2()))
        {
            center += HexMetrics.GetSecondSolidCorner(direction) * 0.25f;
        }
        
        EdgeVertices m = new EdgeVertices(
            Vector3.Lerp(center, e.m_v1, 0.5f),
            Vector3.Lerp(center, e.m_v5, 0.5f));

        TriangulateEdgeStrip(m, s_weights1, cellIndex, e, s_weights1, cellIndex);
        TriangulateEdgeFan(center, m, cellIndex);
        
        if (!cell.IsUnderwater
            && !cell.HasRoadThroughEdge(direction))
        {
            m_features.AddFeature(cell, (center + e.m_v1 + e.m_v5) * (1.0f / 3.0f));
        }
    }

    private void TriangulateRiverQuad(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4, float y1, float y2, float v, bool reversed, Vector3 indices)
    {
        v1.y = v2.y = y1;
        v3.y = v4.y = y2;

        m_rivers.AddQuad(v1, v2, v3, v4);

        if (reversed)
        {
            m_rivers.AddQuadUV(1.0f, 0.0f, 0.8f - v, 0.6f - v);
        }
        else
        {
            m_rivers.AddQuadUV(0.0f, 1.0f, v, v + 0.2f);
        }
        
        m_rivers.AddQuadCellData(indices, s_weights1, s_weights2);
    }

    private void TriangulateRiverQuad(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4, float y, float v, bool reversed, Vector3 indices)
    {
        TriangulateRiverQuad(v1, v2, v3, v4, y, y, v, reversed, indices);
    }

    private void TriangulateRoadSegment(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4, Vector3 v5, Vector3 v6,
        Color w1, Color w2, Vector3 indices)
    {
        m_roads.AddQuad(v1, v2, v4, v5);
        m_roads.AddQuad(v2, v3, v5, v6);
        
        m_roads.AddQuadUV(0.0f, 1.0f, 0.0f, 0.0f);
        m_roads.AddQuadUV(1.0f, 0.0f, 0.0f, 0.0f);
        
        m_roads.AddQuadCellData(indices, w1, w2);
        m_roads.AddQuadCellData(indices, w1, w2);
    }

    private void TriangulateRoad(Vector3 center, Vector3 mL, Vector3 mR, EdgeVertices e, bool hasRoadThroughCellEdge, float index)
    {
        if (hasRoadThroughCellEdge)
        {
            Vector3 indices;
            indices.x = indices.y = indices.z = index;
            
            Vector3 mC = Vector3.Lerp(mL, mR, 0.5f);

            TriangulateRoadSegment(mL, mC, mR, e.m_v2, e.m_v3, e.m_v4, s_weights1, s_weights1, indices);

            m_roads.AddTriangle(center, mL, mC);
            m_roads.AddTriangle(center, mC, mR);

            m_roads.AddTriangleUV(new Vector2(1.0f, 0.0f), new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f));
            m_roads.AddTriangleUV(new Vector2(1.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(0.0f, 0.0f));
            
            m_roads.AddTriangleCellData(indices, s_weights1);
            m_roads.AddTriangleCellData(indices, s_weights1);
        }
        else
        {
            TriangulateRoadEdge(center, mL, mR, index);
        }
    }

    private void TriangulateRoadEdge(Vector3 center, Vector3 mL, Vector3 mR, float index)
    {
        m_roads.AddTriangle(center, mL, mR);
        m_roads.AddTriangleUV(new Vector2(1.0f, 0.0f), new Vector2(0.0f, 0.0f), new Vector2(0.0f, 0.0f));

        Vector3 indices;
        indices.x = indices.y = indices.z = index;
        
        m_roads.AddTriangleCellData(indices, s_weights1);
    }

    private Vector2 GetRoadInterpolators(HexDirection direction, HexCellData cell)
    {
        Vector2 interpolators;

        if (cell.HasRoadThroughEdge(direction))
        {
            interpolators.x = interpolators.y = 0.5f;
        }
        else
        {
            interpolators.x = cell.HasRoadThroughEdge(direction.Previous()) ? 0.5f : 0.25f;
            interpolators.y = cell.HasRoadThroughEdge(direction.Next()) ? 0.5f : 0.25f;
        }

        return interpolators;
    }

    private void TriangulateRoadAdjacentToRiver(HexDirection direction, HexCellData cell, int cellIndex, Vector3 center, EdgeVertices e)
    {
        bool hasRoadThroughEdge = cell.HasRoadThroughEdge(direction);

        bool previousHasRiver = cell.HasRiverThroughEdge(direction.Previous());
        bool nextHasRiver = cell.HasRiverThroughEdge(direction.Next());

        Vector2 interpolators = GetRoadInterpolators(direction, cell);

        Vector3 roadCenter = center;

        HexDirection riverIn = cell.IncomingRiver;
        HexDirection riverOut = cell.OutgoingRiver;

        if (cell.HasRiverBeginOrEnd)
        {
            roadCenter += HexMetrics.GetSolidEdgeMiddle((cell.HasIncomingRiver ? riverIn : riverOut).Opposite()) * (1.0f / 3.0f);
        }
        else if (riverIn == riverOut.Opposite())
        {
            Vector3 corner;
            
            if (previousHasRiver)
            {
                if (!hasRoadThroughEdge
                    && !cell.HasRoadThroughEdge(direction.Next()))
                {
                    return;
                }
                
                corner = HexMetrics.GetSecondSolidCorner(direction);
            }
            else
            {
                if (!hasRoadThroughEdge
                    && !cell.HasRoadThroughEdge(direction.Previous()))
                {
                    return;
                }
                
                corner = HexMetrics.GetFirstSolidCorner(direction);
            }

            roadCenter += corner * 0.5f;

            if (riverIn == direction.Next()
                && cell.HasRoadThroughEdge(direction.Next2())
                || cell.HasRoadThroughEdge(direction.Opposite()))
            {
                m_features.AddBridge(roadCenter, center - corner * 0.5f);
            }

            center += corner * 0.25f;
        }
        else if (riverIn == riverOut.Previous())
        {
            roadCenter -= HexMetrics.GetSecondCorner(riverIn) * 0.2f;
        }
        else if (riverIn == riverOut.Next())
        {
            roadCenter -= HexMetrics.GetFirstCorner(riverIn) * 0.2f;
        }
        else if (previousHasRiver
                 && nextHasRiver)
        {
            if (!hasRoadThroughEdge)
            {
                return;
            }
            
            Vector3 offset = HexMetrics.GetSolidEdgeMiddle(direction) * HexMetrics.InnerToOuter;

            roadCenter += offset * 0.7f;

            center += offset * 0.5f;
        }
        else
        {
            HexDirection middle;

            if (previousHasRiver)
            {
                middle = direction.Next();
            }
            else if (nextHasRiver)
            {
                middle = direction.Previous();
            }
            else
            {
                middle = direction;
            }
            
            if (!cell.HasRoadThroughEdge(middle)
                && !cell.HasRoadThroughEdge(middle.Previous())
                && !cell.HasRoadThroughEdge(middle.Next()))
            {
                return;
            }

            Vector3 offset = HexMetrics.GetSolidEdgeMiddle(middle);
            
            roadCenter += offset * 0.25f;

            if (direction == middle
                && cell.HasRoadThroughEdge(direction.Opposite()))
            {
                m_features.AddBridge(roadCenter, center - offset * (HexMetrics.InnerToOuter * 0.7f));
            }
        }
        
        Vector3 mL = Vector3.Lerp(roadCenter, e.m_v1, interpolators.x);
        Vector3 mR = Vector3.Lerp(roadCenter, e.m_v5, interpolators.y);
        
        TriangulateRoad(roadCenter, mL, mR, e, hasRoadThroughEdge, cellIndex);

        if (previousHasRiver)
        {
            TriangulateRoadEdge(roadCenter, center, mL, cellIndex);
        }

        if (nextHasRiver)
        {
            TriangulateRoadEdge(roadCenter, mR, center, cellIndex);
        }
    }

    private void TriangulateWater(HexDirection direction, HexCellData cell, int cellIndex, Vector3 center)
    {
        center.y = cell.WaterSurfaceY;

        HexCoordinates neighborCoordinates = cell.m_coordinates.Step(direction);

        if (Grid.TryGetCellIndex(neighborCoordinates, out int neighborIndex)
            && !Grid.CellData[neighborIndex].IsUnderwater)
        {
            TriangulateWaterShore(direction, cell, cellIndex, neighborIndex, neighborCoordinates.ColumnIndex, center);
        }
        else
        {
            TriangulateOpenWater(cell.m_coordinates, direction, cellIndex, neighborIndex, center);
        }
    }
    
    private void TriangulateOpenWater(HexCoordinates coordinates, HexDirection direction, int cellIndex, int neighborIndex, Vector3 center)
    {
        Vector3 c1 = center + HexMetrics.GetFirstWaterCorner(direction);
        Vector3 c2 = center + HexMetrics.GetSecondWaterCorner(direction);

        m_water.AddTriangle(center, c1, c2);

        Vector3 indices;
        indices.x = indices.y = indices.z = cellIndex;
        
        m_water.AddTriangleCellData(indices, s_weights1);

        if (direction <= HexDirection.SE
            && neighborIndex != -1)
        {
            Vector3 bridge = HexMetrics.GetWaterBridge(direction);

            Vector3 e1 = c1 + bridge;
            Vector3 e2 = c2 + bridge;

            m_water.AddQuad(c1, c2, e1, e2);

            indices.z = neighborIndex;

            m_water.AddQuadCellData(indices, s_weights1, s_weights2);

            if (direction <= HexDirection.E)
            {
                if (!Grid.TryGetCellIndex(coordinates.Step(direction.Next()), out int nextNeighborIndex)
                    || !Grid.CellData[nextNeighborIndex].IsUnderwater)
                {
                    return;
                }
                
                m_water.AddTriangle(c2, e2, c2 + HexMetrics.GetWaterBridge(direction.Next()));

                indices.z = nextNeighborIndex;

                m_water.AddTriangleCellData(indices, s_weights1, s_weights2, s_weights3);
            }
        }
    }

    private void TriangulateWaterShore(HexDirection direction, HexCellData cell, int cellIndex, int neighborIndex, int neighborColumnIndex, Vector3 center)
    {
        EdgeVertices e1 = new EdgeVertices(center + HexMetrics.GetFirstWaterCorner(direction),
            center + HexMetrics.GetSecondWaterCorner(direction));
        
        m_water.AddTriangle(center, e1.m_v1, e1.m_v2);
        m_water.AddTriangle(center, e1.m_v2, e1.m_v3);
        m_water.AddTriangle(center, e1.m_v3, e1.m_v4);
        m_water.AddTriangle(center, e1.m_v4, e1.m_v5);

        Vector3 indices;
        indices.x = indices.z = cellIndex;
        indices.y = neighborIndex;
        
        m_water.AddTriangleCellData(indices, s_weights1);
        m_water.AddTriangleCellData(indices, s_weights1);
        m_water.AddTriangleCellData(indices, s_weights1);
        m_water.AddTriangleCellData(indices, s_weights1);

        Vector3 center2 = Grid.CellPositions[neighborIndex];

        int cellColumnIndex = cell.m_coordinates.ColumnIndex;

        if (neighborColumnIndex < cellColumnIndex - 1)
        {
            center2.x += HexMetrics.WrapSize * HexMetrics.InnerDiameter;
        }
        else if (neighborColumnIndex > cellColumnIndex + 1)
        {
            center2.x -= HexMetrics.WrapSize * HexMetrics.InnerDiameter;
        }
        
        center2.y = center.y;

        EdgeVertices e2 = new EdgeVertices(center2 + HexMetrics.GetSecondSolidCorner(direction.Opposite()),
            center2 + HexMetrics.GetFirstSolidCorner(direction.Opposite()));

        if (cell.HasRiverThroughEdge(direction))
        {
            TriangulateEstuary(e1, e2, cell.HasIncomingRiverThroughEdge(direction), indices);
        }
        else
        {
            m_waterShore.AddQuad(e1.m_v1, e1.m_v2, e2.m_v1, e2.m_v2);
            m_waterShore.AddQuad(e1.m_v2, e1.m_v3, e2.m_v2, e2.m_v3);
            m_waterShore.AddQuad(e1.m_v3, e1.m_v4, e2.m_v3, e2.m_v4);
            m_waterShore.AddQuad(e1.m_v4, e1.m_v5, e2.m_v4, e2.m_v5);
            
            m_waterShore.AddQuadUV(0.0f, 0.0f, 0.0f, 1.0f);
            m_waterShore.AddQuadUV(0.0f, 0.0f, 0.0f, 1.0f);
            m_waterShore.AddQuadUV(0.0f, 0.0f, 0.0f, 1.0f);
            m_waterShore.AddQuadUV(0.0f, 0.0f, 0.0f, 1.0f);
            
            m_waterShore.AddQuadCellData(indices, s_weights1, s_weights2);
            m_waterShore.AddQuadCellData(indices, s_weights1, s_weights2);
            m_waterShore.AddQuadCellData(indices, s_weights1, s_weights2);
            m_waterShore.AddQuadCellData(indices, s_weights1, s_weights2);
        }

        HexCoordinates nextNeighborCoordinates = cell.m_coordinates.Step(direction.Next());

        if (Grid.TryGetCellIndex(nextNeighborCoordinates, out int nextNeighborIndex))
        {
            Vector3 center3 = Grid.CellPositions[nextNeighborIndex];

            bool nextNeighborIsUnderwater = Grid.CellData[nextNeighborIndex].IsUnderwater;

            int nextNeighborColumnIndex = nextNeighborCoordinates.ColumnIndex;

            if (nextNeighborColumnIndex < cellColumnIndex - 1)
            {
                center3.x += HexMetrics.WrapSize * HexMetrics.InnerDiameter;
            }
            else if (nextNeighborColumnIndex > cellColumnIndex + 1)
            {
                center3.x -= HexMetrics.WrapSize * HexMetrics.InnerDiameter;
            }
            
            Vector3 v3 = center3 + (nextNeighborIsUnderwater
                ? HexMetrics.GetFirstWaterCorner(direction.Previous())
                : HexMetrics.GetFirstSolidCorner(direction.Previous()));
            v3.y = center.y;
            
            m_waterShore.AddTriangle(e1.m_v5, e2.m_v5, v3);
            
            m_waterShore.AddTriangleUV(new Vector2(0.0f, 0.0f),
                new Vector2(0.0f, 1.0f),
                new Vector2(0.0f, nextNeighborIsUnderwater ? 0.0f : 1.0f));

            indices.z = nextNeighborIndex;

            m_waterShore.AddTriangleCellData(indices, s_weights1, s_weights2, s_weights3);
        }
    }

    private void TriangulateWaterfallInWater(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4, float y1, float y2,
        float waterY, Vector3 indices)
    {
        v1.y = v2.y = y1;
        v3.y = v4.y = y2;

        v1 = HexMetrics.Perturb(v1);
        v2 = HexMetrics.Perturb(v2);
        v3 = HexMetrics.Perturb(v3);
        v4 = HexMetrics.Perturb(v4);

        float t = (waterY - y2) / (y1 - y2);

        v3 = Vector3.Lerp(v3, v1, t);
        v4 = Vector3.Lerp(v4, v2, t);

        m_rivers.AddQuadUnperturbed(v1, v2, v3, v4);
        m_rivers.AddQuadUV(0.0f, 1.0f, 0.8f, 1.0f);

        m_rivers.AddQuadCellData(indices, s_weights1, s_weights2);
    }

    private void TriangulateEstuary(EdgeVertices e1, EdgeVertices e2, bool incomingRiver, Vector3 indices)
    {
        m_waterShore.AddTriangle(e2.m_v1, e1.m_v2, e1.m_v1);
        m_waterShore.AddTriangle(e2.m_v5, e1.m_v5, e1.m_v4);
        
        m_waterShore.AddTriangleUV(new Vector2(0.0f, 1.0f),
            new Vector2(0.0f, 0.0f),
            new Vector2(0.0f, 0.0f));
        m_waterShore.AddTriangleUV(new Vector2(0.0f, 1.0f),
            new Vector2(0.0f, 0.0f),
            new Vector2(0.0f, 0.0f));
        
        m_waterShore.AddTriangleCellData(indices, s_weights2, s_weights1, s_weights1);
        m_waterShore.AddTriangleCellData(indices, s_weights2, s_weights1, s_weights1);
        
        m_estuaries.AddQuad(e2.m_v1, e1.m_v2, e2.m_v2, e1.m_v3);
        m_estuaries.AddTriangle(e1.m_v3, e2.m_v2, e2.m_v4);
        m_estuaries.AddQuad(e1.m_v3, e1.m_v4, e2.m_v4, e2.m_v5);
        
        m_estuaries.AddQuadUV(new Vector2(0.0f, 1.0f),
            new Vector2(0.0f, 0.0f),
            new Vector2(1.0f, 1.0f),
            new Vector2(0.0f, 0.0f));
        m_estuaries.AddTriangleUV(new Vector2(0.0f, 0.0f),
            new Vector2(1.0f, 1.0f),
            new Vector2(1.0f, 1.0f));
        m_estuaries.AddQuadUV(new Vector2(0.0f, 0.0f),
            new Vector2(0.0f, 0.0f),
            new Vector2(1.0f, 1.0f),
            new Vector2(0.0f, 1.0f));
        
        m_estuaries.AddQuadCellData(indices, s_weights2, s_weights1, s_weights2, s_weights1);
        m_estuaries.AddTriangleCellData(indices, s_weights1, s_weights2, s_weights2);
        m_estuaries.AddQuadCellData(indices, s_weights1, s_weights2);

        if (incomingRiver)
        {
            m_estuaries.AddQuadUV2(new Vector2(1.5f, 1.0f),
                new Vector2(0.7f, 1.15f),
                new Vector2(1.0f, 0.8f),
                new Vector2(0.5f, 1.1f));
            m_estuaries.AddTriangleUV2(new Vector2(0.5f, 1.1f),
                new Vector2(1.0f, 0.8f),
                new Vector2(0.0f, 0.8f));
            m_estuaries.AddQuadUV2(new Vector2(0.5f, 1.1f),
                new Vector2(0.3f, 1.15f),
                new Vector2(0.0f, 0.8f),
                new Vector2(-0.5f, 1.0f));
        }
        else
        {
            m_estuaries.AddQuadUV2(new Vector2(-0.5f, -0.2f),
                new Vector2(0.3f, -0.35f),
                new Vector2(0.0f, 0.0f),
                new Vector2(0.5f, -0.3f));
            m_estuaries.AddTriangleUV2(new Vector2(0.5f, -0.3f),
                new Vector2(0.0f, 0.0f),
                new Vector2(1.0f, 0.0f));
            m_estuaries.AddQuadUV2(new Vector2(0.5f, -0.3f),
                new Vector2(0.7f, -0.35f),
                new Vector2(1.0f, 0.0f),
                new Vector2(1.5f, -0.2f));
        }
    }
}