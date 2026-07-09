using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws a filled radial sector for EICAS gauges. Angles are in UI local space,
/// with 0 degrees pointing right and positive angles rotating counterclockwise.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public class EicasRadialFill : MaskableGraphic
{
    [Tooltip("当前填充比例，范围 0 到 1。0 表示不显示，1 表示填满 Start Angle 到 End Angle 的全部范围。")]
    [SerializeField, Range(0f, 1f)]
    private float amount = 0.5f;

    [Tooltip("扇形起始角度。0 度指向右侧，负数通常表示顺时针方向。")]
    [SerializeField]
    private float startAngle = 210f;

    [Tooltip("扇形结束角度。例如 Start=0、End=-180 表示顺时针半圈；End=-360 表示顺时针一整圈。")]
    [SerializeField]
    private float endAngle = -30f;

    [Tooltip("扇形边缘分段数量。数值越大越圆滑，但 UI 顶点越多。")]
    [SerializeField, Min(3)]
    private int segments = 48;

    [Tooltip("内半径。设为 0 是实心扇形；大于 0 会变成环形扇区。")]
    [SerializeField, Min(0f)]
    private float innerRadius = 0f;

    [Tooltip("外半径，也就是实心扇形的大小。通常由 EicasGauge 根据指针长度自动同步。")]
    [SerializeField, Min(0f)]
    private float outerRadius = 90f;

    public float Amount
    {
        get => amount;
        set
        {
            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(amount, clamped))
            {
                return;
            }

            amount = clamped;
            SetVerticesDirty();
        }
    }

    public float StartAngle
    {
        get => startAngle;
        set
        {
            if (Mathf.Approximately(startAngle, value))
            {
                return;
            }

            startAngle = value;
            SetVerticesDirty();
        }
    }

    public float EndAngle
    {
        get => endAngle;
        set
        {
            if (Mathf.Approximately(endAngle, value))
            {
                return;
            }

            endAngle = value;
            SetVerticesDirty();
        }
    }

    public float InnerRadius
    {
        get => innerRadius;
        set
        {
            innerRadius = Mathf.Max(0f, value);
            SetVerticesDirty();
        }
    }

    public float OuterRadius
    {
        get => outerRadius;
        set
        {
            outerRadius = Mathf.Max(0f, value);
            SetVerticesDirty();
        }
    }

    public void SetAmount(float normalizedAmount)
    {
        Amount = normalizedAmount;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (amount <= 0f || outerRadius <= 0f)
        {
            return;
        }

        float sweep = (endAngle - startAngle) * amount;
        int steps = Mathf.Max(1, Mathf.CeilToInt(segments * amount));
        Vector2 center = rectTransform.rect.center;

        if (innerRadius <= 0.01f)
        {
            AddSolidSector(vh, center, sweep, steps);
        }
        else
        {
            AddRingSector(vh, center, sweep, steps);
        }
    }

    private void AddSolidSector(VertexHelper vh, Vector2 center, float sweep, int steps)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = center;
        vh.AddVert(vertex);

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            float angle = (startAngle + sweep * t) * Mathf.Deg2Rad;
            vertex.position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * outerRadius;
            vh.AddVert(vertex);
        }

        for (int i = 1; i <= steps; i++)
        {
            vh.AddTriangle(0, i, i + 1);
        }
    }

    private void AddRingSector(VertexHelper vh, Vector2 center, float sweep, int steps)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            float angle = (startAngle + sweep * t) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            vertex.position = center + direction * innerRadius;
            vh.AddVert(vertex);

            vertex.position = center + direction * outerRadius;
            vh.AddVert(vertex);
        }

        for (int i = 0; i < steps; i++)
        {
            int innerA = i * 2;
            int outerA = innerA + 1;
            int innerB = innerA + 2;
            int outerB = innerA + 3;

            vh.AddTriangle(innerA, outerA, outerB);
            vh.AddTriangle(innerA, outerB, innerB);
        }
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        amount = Mathf.Clamp01(amount);
        segments = Mathf.Max(3, segments);
        innerRadius = Mathf.Max(0f, innerRadius);
        outerRadius = Mathf.Max(0f, outerRadius);
        SetVerticesDirty();
    }
#endif
}
