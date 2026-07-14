using System;
using UnityEngine;
using UnityEngine.UI;

public struct FlightMapRenderState
{
    public Vector2[] RoutePoints;
    public Vector2[] TrackPoints;
    public Vector2 AircraftPoint;
    public float AircraftHeadingDeg;
    public bool HasAircraft;
    public int ActiveLegIndex;
    public float RangeNm;
}

[RequireComponent(typeof(CanvasRenderer))]
public class FlightMapGraphic : MaskableGraphic
{
    private const int SafeMeshVertexLimit = 62000;
    private const int TrackSegmentVertexCost = 4;

    [SerializeField] private float mapSize = 480f;
    [SerializeField] private bool drawBackground = true;

    private static readonly Color32 Background = new Color32(5, 12, 16, 245);
    private static readonly Color32 MajorGrid = new Color32(45, 83, 87, 120);
    private static readonly Color32 MinorGrid = new Color32(26, 47, 52, 95);
    private static readonly Color32 TrackLine = new Color32(255, 204, 72, 230);
    private static readonly Color32 OriginMarker = new Color32(238, 74, 186, 255);
    private static readonly Color32 AircraftColor = new Color32(124, 75, 34, 255);

    private FlightMapRenderState state;

    public float MapSize => mapSize;

    public void Configure(float size)
    {
        mapSize = Mathf.Max(120f, size);
        SetVerticesDirty();
    }

    public void SetBackgroundVisible(bool visible)
    {
        if (drawBackground == visible)
        {
            return;
        }

        drawBackground = visible;
        SetVerticesDirty();
    }

    public void SetState(FlightMapRenderState nextState)
    {
        state = nextState;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (drawBackground)
        {
            AddRect(vh, Vector2.zero, new Vector2(mapSize, mapSize), Background);
        }

        DrawGrid(vh);
        DrawRangeRings(vh);
        DrawTrack(vh);
        DrawRoute(vh);
        DrawAircraft(vh);
        DrawBorder(vh);
    }

    private void DrawGrid(VertexHelper vh)
    {
        float minorStep = mapSize / 8f;
        float majorStep = mapSize / 4f;

        for (float p = minorStep; p < mapSize; p += minorStep)
        {
            Color32 c = Mathf.Abs(Mathf.Repeat(p, majorStep)) < 0.1f ? MajorGrid : MinorGrid;
            AddLine(vh, new Vector2(p, 0f), new Vector2(p, mapSize), 1f, c);
            AddLine(vh, new Vector2(0f, p), new Vector2(mapSize, p), 1f, c);
        }

        float center = mapSize * 0.5f;
        AddLine(vh, new Vector2(center, 0f), new Vector2(center, mapSize), 1.6f, MajorGrid);
        AddLine(vh, new Vector2(0f, center), new Vector2(mapSize, center), 1.6f, MajorGrid);
    }

    private void DrawRangeRings(VertexHelper vh)
    {
        Vector2 center = new Vector2(mapSize * 0.5f, mapSize * 0.5f);
        AddCircle(vh, center, mapSize * 0.25f, 160, 1.2f, MinorGrid);
        AddCircle(vh, center, mapSize * 0.5f, 224, 1.4f, MajorGrid);
    }

    private void DrawRoute(VertexHelper vh)
    {
        Vector2[] points = state.RoutePoints;
        if (points == null || points.Length == 0)
        {
            return;
        }

        if (IsReasonablePoint(points[0]))
        {
            DrawOriginMarker(vh, points[0]);
        }
    }

    private void DrawOriginMarker(VertexHelper vh, Vector2 center)
    {
        float scale = Mathf.Clamp(mapSize / 520f, 0.78f, 1.22f);
        AddDisc(vh, center, 4.5f * scale, 18, OriginMarker);
    }

    private void DrawTrack(VertexHelper vh)
    {
        Vector2[] points = state.TrackPoints;
        if (points == null || points.Length < 2)
        {
            return;
        }

        int maxSegments = CalculateTrackSegmentBudget(vh.currentVertCount);
        int step = CalculateTrackSampleStep(points.Length, maxSegments);
        int previousIndex = 0;

        for (int i = step; i < points.Length; i += step)
        {
            AddTrackSegment(vh, points[previousIndex], points[i]);
            previousIndex = i;
        }

        int lastIndex = points.Length - 1;
        if (previousIndex != lastIndex)
        {
            AddTrackSegment(vh, points[previousIndex], points[lastIndex]);
        }
    }

    public static int CalculateTrackSampleStep(int pointCount, int maxRenderableSegments)
    {
        if (pointCount < 2)
        {
            return 1;
        }

        int segmentCount = pointCount - 1;
        int safeSegmentBudget = Mathf.Max(1, maxRenderableSegments);
        return Mathf.Max(1, Mathf.CeilToInt(segmentCount / (float)safeSegmentBudget));
    }

    public static int EstimateRenderedTrackSegments(int pointCount, int sampleStep)
    {
        if (pointCount < 2)
        {
            return 0;
        }

        int safeStep = Mathf.Max(1, sampleStep);
        int segmentCount = pointCount - 1;
        return Mathf.CeilToInt(segmentCount / (float)safeStep);
    }

    private static int CalculateTrackSegmentBudget(int currentVertexCount)
    {
        int remainingVertices = SafeMeshVertexLimit - currentVertexCount;
        return Mathf.Max(1, remainingVertices / TrackSegmentVertexCost);
    }

    private void AddTrackSegment(VertexHelper vh, Vector2 a, Vector2 b)
    {
        if (!IsReasonablePoint(a) || !IsReasonablePoint(b))
        {
            return;
        }

        AddLineBody(vh, a, b, 2.4f, TrackLine);
    }

    public static Vector2[] SampleTrackForRendering(Vector2[] points, int maxRenderableSegments)
    {
        if (points == null || points.Length == 0)
        {
            return Array.Empty<Vector2>();
        }

        if (points.Length == 1)
        {
            return new[] { points[0] };
        }

        int step = CalculateTrackSampleStep(points.Length, maxRenderableSegments);
        int renderedSegments = EstimateRenderedTrackSegments(points.Length, step);
        Vector2[] sampled = new Vector2[renderedSegments + 1];
        int output = 0;
        sampled[output++] = points[0];

        for (int i = step; i < points.Length; i += step)
        {
            sampled[output++] = points[i];
        }

        int lastIndex = points.Length - 1;
        if (sampled[output - 1] != points[lastIndex])
        {
            sampled[output++] = points[lastIndex];
        }

        if (output == sampled.Length)
        {
            return sampled;
        }

        Vector2[] trimmed = new Vector2[output];
        Array.Copy(sampled, trimmed, output);
        return trimmed;
    }

    public static int EstimateTrackVertexCount(int pointCount, int maxRenderableSegments)
    {
        int step = CalculateTrackSampleStep(pointCount, maxRenderableSegments);
        return EstimateRenderedTrackSegments(pointCount, step) * TrackSegmentVertexCost;
    }

    private void DrawAircraft(VertexHelper vh)
    {
        if (!state.HasAircraft || !IsReasonablePoint(state.AircraftPoint))
        {
            return;
        }

        Vector2 forward = BearingVector(state.AircraftHeadingDeg);
        Vector2 right = new Vector2(-forward.y, forward.x);
        Vector2 center = state.AircraftPoint;

        float scale = Mathf.Clamp(mapSize / 520f, 0.78f, 1.22f);
        float bodyHalf = 4.4f * scale;

        Vector2 nose = center + forward * (25f * scale);
        Vector2 shoulderLeft = center + forward * (8f * scale) - right * bodyHalf;
        Vector2 shoulderRight = center + forward * (8f * scale) + right * bodyHalf;
        Vector2 tailLeft = center - forward * (18f * scale) - right * (3.1f * scale);
        Vector2 tailRight = center - forward * (18f * scale) + right * (3.1f * scale);
        Vector2 tail = center - forward * (23f * scale);

        AddTriangle(vh, nose, shoulderLeft, shoulderRight, AircraftColor);
        AddQuad(vh, shoulderLeft, shoulderRight, tailRight, tailLeft, AircraftColor);
        AddTriangle(vh, tail, tailLeft, tailRight, AircraftColor);

        AddTriangle(vh,
            center + forward * (2f * scale) - right * (4.6f * scale),
            center - forward * (4f * scale) - right * (22f * scale),
            center - forward * (8f * scale) - right * (4.6f * scale),
            AircraftColor);
        AddTriangle(vh,
            center + forward * (2f * scale) + right * (4.6f * scale),
            center - forward * (4f * scale) + right * (22f * scale),
            center - forward * (8f * scale) + right * (4.6f * scale),
            AircraftColor);

        AddTriangle(vh,
            center - forward * (16f * scale) - right * (3.2f * scale),
            center - forward * (20f * scale) - right * (9f * scale),
            center - forward * (20f * scale) - right * (3.2f * scale),
            AircraftColor);
        AddTriangle(vh,
            center - forward * (16f * scale) + right * (3.2f * scale),
            center - forward * (20f * scale) + right * (9f * scale),
            center - forward * (20f * scale) + right * (3.2f * scale),
            AircraftColor);
    }

    private void DrawBorder(VertexHelper vh)
    {
        Color32 border = new Color32(150, 154, 158, 235);
        AddLine(vh, new Vector2(0f, 0f), new Vector2(mapSize, 0f), 3f, border);
        AddLine(vh, new Vector2(mapSize, 0f), new Vector2(mapSize, mapSize), 3f, border);
        AddLine(vh, new Vector2(mapSize, mapSize), new Vector2(0f, mapSize), 3f, border);
        AddLine(vh, new Vector2(0f, mapSize), new Vector2(0f, 0f), 3f, border);
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

    private void AddTriangle(VertexHelper vh, Vector2 aPx, Vector2 bPx, Vector2 cPx, Color32 c)
    {
        int start = vh.currentVertCount;
        vh.AddVert(PixelToLocal(aPx), c, Vector2.zero);
        vh.AddVert(PixelToLocal(bPx), c, Vector2.zero);
        vh.AddVert(PixelToLocal(cPx), c, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
    }

    private void AddQuad(VertexHelper vh, Vector2 aPx, Vector2 bPx, Vector2 cPx, Vector2 dPx, Color32 c)
    {
        int start = vh.currentVertCount;
        vh.AddVert(PixelToLocal(aPx), c, Vector2.zero);
        vh.AddVert(PixelToLocal(bPx), c, Vector2.zero);
        vh.AddVert(PixelToLocal(cPx), c, Vector2.zero);
        vh.AddVert(PixelToLocal(dPx), c, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
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
        if (width >= 1.35f && c.a > 80)
        {
            Color32 soft = WithAlpha(c, Mathf.Min(90, c.a));
            AddLineBody(vh, aPx, bPx, width + 1.25f, soft);
            AddDisc(vh, aPx, width * 0.5f + 0.65f, 12, soft);
            AddDisc(vh, bPx, width * 0.5f + 0.65f, 12, soft);
        }

        AddLineBody(vh, aPx, bPx, width, c);
        AddDisc(vh, aPx, width * 0.5f, 12, c);
        AddDisc(vh, bPx, width * 0.5f, 12, c);
    }

    private void AddLineBody(VertexHelper vh, Vector2 aPx, Vector2 bPx, float width, Color32 c)
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

    private void AddDisc(VertexHelper vh, Vector2 centerPx, float radius, int segments, Color32 c)
    {
        if (radius <= 0.01f || segments < 3)
        {
            return;
        }

        Vector2 center = PixelToLocal(centerPx);
        int start = vh.currentVertCount;
        vh.AddVert(center, c, Vector2.zero);

        for (int i = 0; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            vh.AddVert(p, c, Vector2.zero);
        }

        for (int i = 1; i <= segments; i++)
        {
            vh.AddTriangle(start, start + i, start + i + 1);
        }
    }

    private static Color32 WithAlpha(Color32 c, int alpha)
    {
        return new Color32(c.r, c.g, c.b, (byte)Mathf.Clamp(alpha, 0, 255));
    }

    private Vector2 PixelToLocal(Vector2 p)
    {
        return new Vector2(p.x - mapSize * 0.5f, mapSize * 0.5f - p.y);
    }

    private static Vector2 BearingVector(float headingDeg)
    {
        float rad = headingDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad));
    }

    private static bool IsReasonablePoint(Vector2 p)
    {
        return !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsInfinity(p.x) && !float.IsInfinity(p.y);
    }

    private bool IsNearViewport(Vector2 p)
    {
        float margin = mapSize * 0.2f;
        return p.x >= -margin && p.x <= mapSize + margin && p.y >= -margin && p.y <= mapSize + margin;
    }
}
