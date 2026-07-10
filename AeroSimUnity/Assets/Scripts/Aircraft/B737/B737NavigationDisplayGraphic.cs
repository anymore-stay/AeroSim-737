using UnityEngine;
using UnityEngine.UI;

public enum B737NavigationDisplaySymbolType
{
    Waypoint,
    ActiveWaypoint,
    Airport,
    Vor
}

public struct B737NavigationDisplaySymbolSnapshot
{
    public string Ident;
    public B737NavigationDisplaySymbolType Type;
    public float BearingMagDeg;
    public float DistanceNm;
    public bool IsActive;
}

public struct B737NavigationDisplayState
{
    public float HeadingMagDeg;
    public float TrackMagDeg;
    public float HeadingBugMagDeg;
    public float CourseMagDeg;
    public float WindFromMagDeg;
    public float WindSpeedKts;
    public float DisplayRangeNm;
    public B737NavigationDisplaySymbolSnapshot[] Symbols;
}

/// <summary>
/// 737 ND/EHSI 的矢量绘制层。所有点位以 530x530 左上角像素坐标为基准。
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class B737NavigationDisplayGraphic : MaskableGraphic
{
    [SerializeField] private float canvasSize = 530f;
    [SerializeField] private Vector2 aircraftApex = new Vector2(260f, 388f);
    [SerializeField] private float arcRadius = 306f;
    [SerializeField] private float visibleArcDeg = 90f;
    [SerializeField] private float lineWidth = 2f;

    private static readonly Color32 Background = new Color32(0, 2, 3, 255);
    private static readonly Color32 GridGreen = new Color32(87, 173, 86, 190);
    private static readonly Color32 DimGreen = new Color32(70, 120, 70, 120);
    private static readonly Color32 White = new Color32(230, 236, 230, 255);
    private static readonly Color32 Cyan = new Color32(79, 168, 232, 255);
    private static readonly Color32 Magenta = new Color32(221, 72, 185, 255);

    private B737NavigationDisplayState state;

    public float CanvasSize => canvasSize;
    public Vector2 AircraftApex => aircraftApex;
    public float ArcRadius => arcRadius;
    public float VisibleArcDeg => visibleArcDeg;

    public void ConfigureGeometry(float size, Vector2 apex, float radius, float arcDeg)
    {
        canvasSize = Mathf.Max(1f, size);
        aircraftApex = apex;
        arcRadius = Mathf.Max(1f, radius);
        visibleArcDeg = Mathf.Clamp(arcDeg, 10f, 180f);
        SetVerticesDirty();
    }

    public void SetState(B737NavigationDisplayState nextState)
    {
        state = nextState;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        AddRect(vh, new Vector2(0f, 0f), new Vector2(canvasSize, canvasSize), Background);
        AddVignette(vh);
        DrawRangeRings(vh);
        DrawFlightPlan(vh);
        DrawCompassArc(vh);
        DrawWindArrow(vh);
        DrawNavigationSymbols(vh);
        DrawAircraftSymbol(vh);
    }

    private void AddVignette(VertexHelper vh)
    {
        AddLine(vh, new Vector2(0f, 0f), new Vector2(canvasSize, 0f), 4f, new Color32(10, 36, 35, 210));
        AddLine(vh, new Vector2(0f, canvasSize), new Vector2(canvasSize, canvasSize), 4f, new Color32(0, 0, 0, 255));
        AddLine(vh, new Vector2(0f, 0f), new Vector2(0f, canvasSize), 4f, new Color32(0, 0, 0, 255));
        AddLine(vh, new Vector2(canvasSize, 0f), new Vector2(canvasSize, canvasSize), 4f, new Color32(0, 0, 0, 255));
    }

    private void DrawRangeRings(VertexHelper vh)
    {
        float range = Mathf.Max(5f, state.DisplayRangeNm <= 0f ? 40f : state.DisplayRangeNm);
        float[] ranges = { range * 0.25f, range * 0.5f, range * 0.75f };
        float halfArc = visibleArcDeg * 0.5f;

        for (int i = 0; i < ranges.Length; i++)
        {
            float radius = arcRadius * (ranges[i] / range);
            AddDottedArc(vh, aircraftApex, radius, -halfArc, halfArc, 7f, 9f, 1.3f, DimGreen);
        }

        AddDashedLine(vh,
            aircraftApex,
            PointFromBearing(0f, arcRadius * 0.98f),
            13f,
            10f,
            1.5f,
            new Color32(105, 178, 104, 180));
    }

    private void DrawFlightPlan(VertexHelper vh)
    {
        float relTrack = Mathf.DeltaAngle(state.HeadingMagDeg, state.TrackMagDeg);
        if (Mathf.Abs(relTrack) <= visibleArcDeg * 0.5f + 12f)
        {
            AddLine(vh,
                aircraftApex,
                PointFromBearing(relTrack, arcRadius * 0.98f),
                1.4f,
                new Color32(210, 226, 216, 210));
        }

        float relCourse = Mathf.DeltaAngle(state.HeadingMagDeg, state.CourseMagDeg);
        AddDashedLine(vh,
            PointFromBearing(relCourse + 180f, arcRadius * 0.18f),
            PointFromBearing(relCourse, arcRadius * 0.94f),
            18f,
            10f,
            1.8f,
            Magenta);
    }

    private void DrawCompassArc(VertexHelper vh)
    {
        float halfArc = visibleArcDeg * 0.5f;
        AddArc(vh, aircraftApex, arcRadius, -halfArc, halfArc, 72, lineWidth, White);

        for (float rel = -halfArc; rel <= halfArc + 0.1f; rel += 5f)
        {
            bool major = Mathf.Abs(Mathf.Repeat(rel + 360f, 30f)) < 0.1f || Mathf.Abs(Mathf.Repeat(rel + 360f, 30f) - 30f) < 0.1f;
            bool medium = Mathf.Abs(Mathf.Repeat(rel + 360f, 10f)) < 0.1f || Mathf.Abs(Mathf.Repeat(rel + 360f, 10f) - 10f) < 0.1f;
            float tick = major ? 22f : (medium ? 16f : 10f);
            float width = major ? 2f : 1.2f;
            AddLine(vh, PointFromBearing(rel, arcRadius), PointFromBearing(rel, arcRadius - tick), width, White);
        }

        float bugRel = Mathf.DeltaAngle(state.HeadingMagDeg, state.HeadingBugMagDeg);
        if (Mathf.Abs(bugRel) <= halfArc + 5f)
        {
            Vector2 p = PointFromBearing(bugRel, arcRadius - 3f);
            Vector2 l = PointFromBearing(bugRel - 2.2f, arcRadius - 23f);
            Vector2 r = PointFromBearing(bugRel + 2.2f, arcRadius - 23f);
            AddLine(vh, p, l, 2.2f, Magenta);
            AddLine(vh, p, r, 2.2f, Magenta);
            AddLine(vh, l, r, 2.2f, Magenta);
        }
    }

    private void DrawWindArrow(VertexHelper vh)
    {
        if (state.WindSpeedKts < 1f)
        {
            return;
        }

        float rel = Mathf.DeltaAngle(state.HeadingMagDeg, state.WindFromMagDeg);
        Vector2 center = new Vector2(78f, 98f);
        Vector2 tip = center + BearingVector(rel) * 34f;
        Vector2 tail = center - BearingVector(rel) * 18f;
        AddLine(vh, tail, tip, 1.8f, White);

        Vector2 left = tip - BearingVector(rel - 28f) * 12f;
        Vector2 right = tip - BearingVector(rel + 28f) * 12f;
        AddLine(vh, tip, left, 1.8f, White);
        AddLine(vh, tip, right, 1.8f, White);
    }

    private void DrawNavigationSymbols(VertexHelper vh)
    {
        if (state.Symbols == null)
        {
            return;
        }

        for (int i = 0; i < state.Symbols.Length; i++)
        {
            B737NavigationDisplaySymbolSnapshot symbol = state.Symbols[i];
            if (!TryProject(symbol.BearingMagDeg, symbol.DistanceNm, out Vector2 pos))
            {
                continue;
            }

            switch (symbol.Type)
            {
                case B737NavigationDisplaySymbolType.Airport:
                    AddCircle(vh, pos, 10f, 24, 1.7f, Cyan);
                    break;
                case B737NavigationDisplaySymbolType.Vor:
                    DrawVorSymbol(vh, pos, Cyan);
                    break;
                case B737NavigationDisplaySymbolType.ActiveWaypoint:
                    DrawDiamond(vh, pos, 10f, Magenta);
                    break;
                default:
                    DrawDiamond(vh, pos, 8f, GridGreen);
                    break;
            }
        }
    }

    private void DrawVorSymbol(VertexHelper vh, Vector2 pos, Color32 c)
    {
        AddCircle(vh, pos, 8f, 20, 1.6f, c);
        AddLine(vh, pos, pos + new Vector2(0f, -15f), 1.4f, c);
        AddLine(vh, pos, pos + new Vector2(-13f, 8f), 1.4f, c);
        AddLine(vh, pos, pos + new Vector2(13f, 8f), 1.4f, c);
    }

    private void DrawAircraftSymbol(VertexHelper vh)
    {
        Vector2 apex = aircraftApex;
        Vector2 leftBase = apex + new Vector2(-13f, 35f);
        Vector2 rightBase = apex + new Vector2(13f, 35f);
        Vector2 tail = apex + new Vector2(0f, 50f);
        Vector2 wingLeft = apex + new Vector2(-22f, 24f);
        Vector2 wingRight = apex + new Vector2(22f, 24f);

        AddLine(vh, apex, leftBase, 2f, White);
        AddLine(vh, apex, rightBase, 2f, White);
        AddLine(vh, leftBase, rightBase, 2f, White);
        AddLine(vh, apex + new Vector2(0f, 14f), tail, 2f, White);
        AddLine(vh, wingLeft, wingRight, 2f, White);
    }

    private bool TryProject(float bearingMagDeg, float distanceNm, out Vector2 pos)
    {
        float range = Mathf.Max(5f, state.DisplayRangeNm <= 0f ? 40f : state.DisplayRangeNm);
        float rel = Mathf.DeltaAngle(state.HeadingMagDeg, bearingMagDeg);
        float radius = arcRadius * Mathf.Clamp(distanceNm / range, 0f, 1.25f);
        pos = PointFromBearing(rel, radius);
        return pos.x >= -20f && pos.x <= canvasSize + 20f && pos.y >= 74f && pos.y <= canvasSize + 20f;
    }

    private void DrawDiamond(VertexHelper vh, Vector2 center, float size, Color32 c)
    {
        Vector2 top = center + new Vector2(0f, -size);
        Vector2 right = center + new Vector2(size, 0f);
        Vector2 bottom = center + new Vector2(0f, size);
        Vector2 left = center + new Vector2(-size, 0f);
        AddLine(vh, top, right, 1.7f, c);
        AddLine(vh, right, bottom, 1.7f, c);
        AddLine(vh, bottom, left, 1.7f, c);
        AddLine(vh, left, top, 1.7f, c);
    }

    private void AddRect(VertexHelper vh, Vector2 topLeft, Vector2 bottomRight, Color32 c)
    {
        Vector2 a = PixelToLocal(topLeft);
        Vector2 b = PixelToLocal(new Vector2(bottomRight.x, topLeft.y));
        Vector2 d = PixelToLocal(new Vector2(topLeft.x, bottomRight.y));
        Vector2 e = PixelToLocal(bottomRight);
        int start = vh.currentVertCount;
        vh.AddVert(a, c, Vector2.zero);
        vh.AddVert(b, c, Vector2.zero);
        vh.AddVert(e, c, Vector2.zero);
        vh.AddVert(d, c, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    private void AddArc(VertexHelper vh, Vector2 center, float radius, float startRelDeg, float endRelDeg, int segments, float width, Color32 c)
    {
        Vector2 prev = PointFromBearing(startRelDeg, radius, center);
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(startRelDeg, endRelDeg, t);
            Vector2 next = PointFromBearing(angle, radius, center);
            AddLine(vh, prev, next, width, c);
            prev = next;
        }
    }

    private void AddDottedArc(VertexHelper vh, Vector2 center, float radius, float startRelDeg, float endRelDeg, float dashDeg, float gapDeg, float width, Color32 c)
    {
        float angle = startRelDeg;
        while (angle < endRelDeg)
        {
            float end = Mathf.Min(angle + dashDeg, endRelDeg);
            AddArc(vh, center, radius, angle, end, 4, width, c);
            angle += dashDeg + gapDeg;
        }
    }

    private void AddDashedLine(VertexHelper vh, Vector2 startPx, Vector2 endPx, float dashLength, float gapLength, float width, Color32 c)
    {
        Vector2 delta = endPx - startPx;
        float length = delta.magnitude;
        if (length < 0.001f)
        {
            return;
        }

        Vector2 dir = delta / length;
        float distance = 0f;
        while (distance < length)
        {
            float end = Mathf.Min(distance + dashLength, length);
            AddLine(vh, startPx + dir * distance, startPx + dir * end, width, c);
            distance += dashLength + gapLength;
        }
    }

    private void AddCircle(VertexHelper vh, Vector2 center, float radius, int segments, float width, Color32 c)
    {
        Vector2 prev = center + new Vector2(radius, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vector2 next = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            AddLine(vh, prev, next, width, c);
            prev = next;
        }
    }

    private void AddLine(VertexHelper vh, Vector2 aPx, Vector2 bPx, float width, Color32 c)
    {
        Vector2 a = PixelToLocal(aPx);
        Vector2 b = PixelToLocal(bPx);
        Vector2 delta = b - a;
        if (delta.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector2 n = new Vector2(-delta.y, delta.x).normalized * (width * 0.5f);
        int start = vh.currentVertCount;
        vh.AddVert(a - n, c, Vector2.zero);
        vh.AddVert(a + n, c, Vector2.zero);
        vh.AddVert(b + n, c, Vector2.zero);
        vh.AddVert(b - n, c, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    private Vector2 PointFromBearing(float relativeBearingDeg, float radius)
    {
        return PointFromBearing(relativeBearingDeg, radius, aircraftApex);
    }

    private static Vector2 PointFromBearing(float relativeBearingDeg, float radius, Vector2 center)
    {
        return center + BearingVector(relativeBearingDeg) * radius;
    }

    private static Vector2 BearingVector(float relativeBearingDeg)
    {
        float rad = relativeBearingDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad));
    }

    private Vector2 PixelToLocal(Vector2 p)
    {
        return new Vector2(p.x - canvasSize * 0.5f, canvasSize * 0.5f - p.y);
    }
}
