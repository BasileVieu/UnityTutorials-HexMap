using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class HexUnit : MonoBehaviour
{
    public static HexUnit m_unitPrefab;

    private List<int> m_pathToTravel;
    
    private int m_locationCellIndex = -1;
    private int m_currentTravelLocationCellIndex = -1;

    private float m_orientation;

    private const float m_travelSpeed = 4.0f;
    private const float m_rotationSpeed = 180.0f;

    public HexGrid Grid
    {
        get;
        set;
    }

    public HexCell Location
    {
        get => Grid.GetCell(m_locationCellIndex);
        set
        {
            if (m_locationCellIndex >= 0)
            {
                HexCell location = Grid.GetCell(m_locationCellIndex);
                
                Grid.DecreaseVisibility(location, VisionRange);
                
                location.Unit = null;
            }
            
            m_locationCellIndex = value.Index;

            value.Unit = this;
            
            Grid.IncreaseVisibility(value, VisionRange);

            transform.localPosition = value.Position;

            Grid.MakeChildOfColumn(transform, value.Coordinates.ColumnIndex);
        }
    }

    public float Orientation
    {
        get => m_orientation;
        set
        {
            m_orientation = value;

            transform.localRotation = Quaternion.Euler(0.0f, value, 0.0f);
        }
    }

    public int VisionRange => 3;

    public int Speed => 24;

    private void OnEnable()
    {
        if (m_locationCellIndex >= 0)
        {
            HexCell location = Grid.GetCell(m_locationCellIndex);
            
            transform.localPosition = location.Position;

            if (m_currentTravelLocationCellIndex >= 0)
            {
                HexCell currentTravelLocation = Grid.GetCell(m_currentTravelLocationCellIndex);
                
                Grid.IncreaseVisibility(location, VisionRange);
                Grid.DecreaseVisibility(currentTravelLocation, VisionRange);

                m_currentTravelLocationCellIndex = -1;
            }
        }
    }

    public bool IsValidDestination(HexCell cell)
    {
        return cell.Flags.HasAll(HexFlags.Explored | HexFlags.Explorable)
               && !cell.Values.IsUnderwater
               && !cell.Unit;
    }

    public void ValidateLocation()
    {
        transform.localPosition = Grid.GetCell(m_locationCellIndex).Position;
    }

    public void Travel(List<int> path)
    {
        HexCell location = Grid.GetCell(m_locationCellIndex);
        
        location.Unit = null;
        location = Grid.GetCell(path[^1]);
        m_locationCellIndex = location.Index;

        m_pathToTravel = path;
        
        StopAllCoroutines();

        StartCoroutine(TravelPath());
    }

    private IEnumerator TravelPath()
    {
        Vector3 a;
        Vector3 b;
        Vector3 c;

        a = b = c = Grid.GetCell(m_pathToTravel[0]).Position;

        yield return LookAt(Grid.GetCell(m_pathToTravel[1]).Position);

        if (m_currentTravelLocationCellIndex < 0)
        {
            m_currentTravelLocationCellIndex = m_pathToTravel[0];
        }

        HexCell currentTravelLocation = Grid.GetCell(m_currentTravelLocationCellIndex);
        
        Grid.DecreaseVisibility(currentTravelLocation, VisionRange);

        int currentColumn = currentTravelLocation.Coordinates.ColumnIndex;

        float t = Time.deltaTime * m_travelSpeed;

        for (int i = 1; i < m_pathToTravel.Count; i++)
        {
            currentTravelLocation = Grid.GetCell(m_pathToTravel[i]);
            m_currentTravelLocationCellIndex = currentTravelLocation.Index;
            
            a = c;
            b = Grid.GetCell(m_pathToTravel[i - 1]).Position;

            int nextColumn = currentTravelLocation.Coordinates.ColumnIndex;

            if (currentColumn != nextColumn)
            {
                if (nextColumn < currentColumn - 1)
                {
                    a.x -= HexMetrics.InnerDiameter * HexMetrics.WrapSize;
                    b.x -= HexMetrics.InnerDiameter * HexMetrics.WrapSize;
                }
                else if (nextColumn > currentColumn + 1)
                {
                    a.x += HexMetrics.InnerDiameter * HexMetrics.WrapSize;
                    b.x += HexMetrics.InnerDiameter * HexMetrics.WrapSize;
                }
                
                Grid.MakeChildOfColumn(transform, nextColumn);

                currentColumn = nextColumn;
            }
            
            c = (b + currentTravelLocation.Position) * 0.5f;
        
            Grid.IncreaseVisibility(Grid.GetCell(m_pathToTravel[i]), VisionRange);

            for (; t < 1.0f; t += Time.deltaTime * m_travelSpeed)
            {
                transform.localPosition = Bezier.GetPoint(a, b, c, t);

                Vector3 d = Bezier.GetDerivative(a, b, c, t);
                d.y = 0.0f;

                transform.localRotation = Quaternion.LookRotation(d);

                yield return null;
            }
        
            Grid.DecreaseVisibility(Grid.GetCell(m_pathToTravel[i]), VisionRange);

            t -= 1.0f;
        }

        m_currentTravelLocationCellIndex = -1;

        HexCell location = Grid.GetCell(m_locationCellIndex);
        
        a = c;
        b = location.Position;
        c = b;
        
        Grid.IncreaseVisibility(location, VisionRange);

        for (; t < 1.0f; t += Time.deltaTime * m_travelSpeed)
        {
            transform.localPosition = Bezier.GetPoint(a, b, c, t);

            Vector3 d = Bezier.GetDerivative(a, b, c, t);
            d.y = 0.0f;

            transform.localRotation = Quaternion.LookRotation(d);

            yield return null;
        }
        
        transform.localPosition = location.Position;
        m_orientation = transform.localRotation.eulerAngles.y;

        ListPool<int>.Add(m_pathToTravel);
        m_pathToTravel = null;
    }

    private IEnumerator LookAt(Vector3 point)
    {
        if (HexMetrics.Wrapping)
        {
            float xDistance = point.x - transform.localPosition.x;

            if (xDistance < -HexMetrics.InnerRadius * HexMetrics.WrapSize)
            {
                point.x += HexMetrics.InnerDiameter * HexMetrics.WrapSize;
            }
            else if (xDistance > HexMetrics.InnerRadius * HexMetrics.WrapSize)
            {
                point.x -= HexMetrics.InnerDiameter * HexMetrics.WrapSize;
            }
        }
        
        point.y = transform.localPosition.y;

        Quaternion fromRotation = transform.localRotation;
        Quaternion toRotation = Quaternion.LookRotation(point - transform.localPosition);

        float angle = Quaternion.Angle(fromRotation, toRotation);

        if (angle > 0.0f)
        {
            float speed = m_rotationSpeed / angle;

            for (float t = Time.deltaTime * speed; t < 1.0f; t += Time.deltaTime * speed)
            {
                transform.localRotation = Quaternion.Slerp(fromRotation, toRotation, t);

                yield return null;
            }
        }

        transform.LookAt(point);

        m_orientation = transform.localRotation.eulerAngles.y;
    }

    public int GetMoveCost(HexCell fromCell, HexCell toCell, HexDirection direction)
    {
        HexEdgeType edgeType = HexMetrics.GetEdgeType(fromCell.Values.Elevation, toCell.Values.Elevation);

        if (edgeType == HexEdgeType.Cliff)
        {
            return -1;
        }

        int moveCost;

        if (fromCell.Flags.HasRoad(direction))
        {
            moveCost = 1;
        }
        else if (fromCell.Flags.HasAny(HexFlags.Walled) != toCell.Flags.HasAny(HexFlags.Walled))
        {
            return -1;
        }
        else
        {
            moveCost = edgeType == HexEdgeType.Flat ? 5 : 10;

            HexValues v = toCell.Values;

            moveCost += v.UrbanLevel + v.FarmLevel + v.PlantLevel;
        }

        return moveCost;
    }

    public void Die()
    {
        HexCell location = Grid.GetCell(m_locationCellIndex);
        
        Grid.DecreaseVisibility(location, VisionRange);
        
        location.Unit = null;
        
        Destroy(gameObject);
    }

    public void Save(BinaryWriter writer)
    {
        Grid.GetCell(m_locationCellIndex).Coordinates.Save(writer);
        
        writer.Write(m_orientation);
    }

    public static void Load(BinaryReader reader, HexGrid grid)
    {
        HexCoordinates coordinates = HexCoordinates.Load(reader);

        float orientation = reader.ReadSingle();
        
        grid.AddUnit(Instantiate(m_unitPrefab), grid.GetCell(coordinates), orientation);
    }
}