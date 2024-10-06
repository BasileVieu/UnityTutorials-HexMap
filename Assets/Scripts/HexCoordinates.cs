using System.IO;
using UnityEngine;

[System.Serializable]
public struct HexCoordinates
{
    [SerializeField] private int m_x;
    [SerializeField] private int m_z;

    public readonly int X => m_x;

    public readonly int Z => m_z;

    public readonly int Y => -X - Z;

    public readonly float HexX => X + Z / 2 + ((Z & 1) == 0 ? 0.0f : 0.5f);

    public readonly float HexZ => Z * HexMetrics.OuterToInner;

    public readonly int ColumnIndex => (X + Z / 2) / HexMetrics.ChunkSizeX;

    public HexCoordinates(int x, int z)
    {
        if (HexMetrics.Wrapping)
        {
            int oX = x + z / 2;

            if (oX < 0)
            {
                x += HexMetrics.WrapSize;
            }
            else if (oX >= HexMetrics.WrapSize)
            {
                x -= HexMetrics.WrapSize;
            }
        }
        
        m_x = x;
        m_z = z;
    }

    public static HexCoordinates FromOffsetCoordinates(int x, int z)
    {
        return new HexCoordinates(x - z / 2, z);
    }

    public static HexCoordinates FromPosition(Vector3 position)
    {
        float x = position.x / HexMetrics.InnerDiameter;
        float y = -x;

        float offset = position.z / (HexMetrics.OuterRadius * 3.0f);

        x -= offset;
        y -= offset;

        int iX = Mathf.RoundToInt(x);
        int iY = Mathf.RoundToInt(y);
        int iZ = Mathf.RoundToInt(-x - y);

        if (iX + iY + iZ != 0)
        {
            float dX = Mathf.Abs(x - iX);
            float dY = Mathf.Abs(y - iY);
            float dZ = Mathf.Abs(-x - y - iZ);

            if (dX > dY
                && dX > dZ)
            {
                iX = -iY - iZ;
            }
            else if (dZ > dY)
            {
                iZ = -iX - iY;
            }
        }

        return new HexCoordinates(iX, iZ);
    }

    public int DistanceTo(HexCoordinates other)
    {
        int xy = (X < other.X ? other.X - X : X - other.X)
                  + (Y < other.Y ? other.Y - Y : Y - other.Y);

        if (HexMetrics.Wrapping)
        {
            other.m_x += HexMetrics.WrapSize;

            int xyWrapped = (X < other.X ? other.X - X : X - other.X) + (Y < other.Y ? other.Y - Y : Y - other.Y);

            if (xyWrapped < xy)
            {
                xy = xyWrapped;
            }
            else
            {
                other.m_x -= 2 * HexMetrics.WrapSize;
                
                xyWrapped = (X < other.X ? other.X - X : X - other.X) + (Y < other.Y ? other.Y - Y : Y - other.Y);
                
                if (xyWrapped < xy)
                {
                    xy = xyWrapped;
                }
            }
        }
        
        return (xy + (Z < other.Z ? other.Z - Z : Z - other.Z)) / 2;
    }

    public HexCoordinates Step(HexDirection direction)
    {
        return direction switch
        {
            HexDirection.NE => new HexCoordinates(X, Z + 1),
            HexDirection.E => new HexCoordinates(X + 1, Z),
            HexDirection.SE => new HexCoordinates(X + 1, Z - 1),
            HexDirection.SW => new HexCoordinates(X, Z - 1),
            HexDirection.W => new HexCoordinates(X - 1, Z),
            _ => new HexCoordinates(X - 1, Z + 1)
        };
    }

    public void Save(BinaryWriter writer)
    {
        writer.Write(X);
        writer.Write(Y);
    }

    public static HexCoordinates Load(BinaryReader reader)
    {
        HexCoordinates c;
        c.m_x = reader.ReadInt32();
        c.m_z = reader.ReadInt32();

        return c;
    }

    public override string ToString()
    {
        return "(" + X + ", " + Y + ", " + Z + ")";
    }

    public string ToStringOnSeparateLines()
    {
        return X + "\n" + Y + "\n" + Z;
    }
}