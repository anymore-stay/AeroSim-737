using UnityEngine;

public static class LowAltitudePitchAssistMath
{
    public static float CalculateBlend(float aglFt, float minimumAglFt, float ceilingAglFt)
    {
        float minimum = Mathf.Max(0f, minimumAglFt);
        float ceiling = Mathf.Max(minimum + 1f, ceilingAglFt);
        if (aglFt < minimum || aglFt >= ceiling)
            return 0f;

        float fadeInEnd = Mathf.Min(minimum + 100f, ceiling);
        float groundFade = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(minimum, fadeInEnd, aglFt));
        float ceilingFade = 1f - Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(minimum, ceiling, aglFt));
        return groundFade * ceilingFade;
    }
}
