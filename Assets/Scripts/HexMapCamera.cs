using UnityEngine;

public class HexMapCamera : MonoBehaviour
{
    private static HexMapCamera s_instance;
    
    [SerializeField] private float m_stickMinZoom;
    [SerializeField] private float m_stickMaxZoom;

    [SerializeField] private float m_swivelMinZoom;
    [SerializeField] private float m_swivelMaxZoom;

    [SerializeField] private float m_moveSpeedMinZoom;
    [SerializeField] private float m_moveSpeedMaxZoom;

    [SerializeField] private float m_rotationSpeed;

    [SerializeField] private HexGrid m_grid;
    
    private Transform m_swivel;
    private Transform m_stick;

    private float m_zoom = 1.0f;

    private float m_rotationAngle;

    public static bool Locked
    {
        set => s_instance.enabled = !value;
    }

    private void Awake()
    {
        m_swivel = transform.GetChild(0);
        m_stick = m_swivel.GetChild(0);
    }

    private void OnEnable()
    {
        s_instance = this;

        ValidatePosition();
    }

    private void Update()
    {
        float zoomDelta = Input.GetAxis("Mouse ScrollWheel");

        if (zoomDelta != 0.0f)
        {
            AdjustZoom(zoomDelta);
        }

        float rotationDelta = Input.GetAxis("Rotation");

        if (rotationDelta != 0.0f)
        {
            AdjustRotation(rotationDelta);
        }

        float xDelta = Input.GetAxis("Horizontal");
        float zDelta = Input.GetAxis("Vertical");

        if (xDelta != 0.0f
            || zDelta != 0.0f)
        {
            AdjustPosition(xDelta, zDelta);
        }
    }

    private void AdjustZoom(float delta)
    {
        m_zoom = Mathf.Clamp01(m_zoom + delta);

        float distance = Mathf.Lerp(m_stickMinZoom, m_stickMaxZoom, m_zoom);

        m_stick.localPosition = new Vector3(0.0f, 0.0f, distance);

        float angle = Mathf.Lerp(m_swivelMinZoom, m_swivelMaxZoom, m_zoom);

        m_swivel.localRotation = Quaternion.Euler(angle, 0.0f, 0.0f);
    }

    private void AdjustPosition(float xDelta, float zDelta)
    {
        Vector3 direction = transform.localRotation * new Vector3(xDelta, 0.0f, zDelta).normalized;

        float damping = Mathf.Max(Mathf.Abs(xDelta), Mathf.Abs(zDelta));
        
        float distance = Mathf.Lerp(m_moveSpeedMinZoom, m_moveSpeedMaxZoom, m_zoom) * damping * Time.deltaTime;
        
        Vector3 position = transform.localPosition;
        position += direction * distance;

        transform.localPosition = m_grid.Wrapping ? WrapPosition(position) : ClampPosition(position);
    }

    private Vector3 WrapPosition(Vector3 position)
    {
        float width = m_grid.CellCountX * HexMetrics.InnerDiameter;

        while (position.x < 0.0f)
        {
            position.x += width;
        }

        while (position.x > width)
        {
            position.x -= width;
        }
        
        float zMax = (m_grid.CellCountZ - 1.0f) * (1.5f * HexMetrics.OuterRadius);

        position.z = Mathf.Clamp(position.z, 0.0f, zMax);

        m_grid.CenterMap(position.x);

        return position;
    }

    private Vector3 ClampPosition(Vector3 position)
    {
        float xMax = (m_grid.CellCountX - 0.5f) * HexMetrics.InnerDiameter;

        position.x = Mathf.Clamp(position.x, 0.0f, xMax);
        
        float zMax = (m_grid.CellCountZ - 1.0f) * (1.5f * HexMetrics.OuterRadius);

        position.z = Mathf.Clamp(position.z, 0.0f, zMax);
        
        return position;
    }

    private void AdjustRotation(float delta)
    {
        m_rotationAngle += delta * m_rotationSpeed * Time.deltaTime;

        if (m_rotationAngle < 0.0f)
        {
            m_rotationAngle += 360.0f;
        }
        else if (m_rotationAngle >= 360.0f)
        {
            m_rotationAngle -= 360.0f;
        }

        transform.localRotation = Quaternion.Euler(0.0f, m_rotationAngle, 0.0f);
    }

    public static void ValidatePosition()
    {
        s_instance.AdjustPosition(0.0f, 0.0f);
    }
}