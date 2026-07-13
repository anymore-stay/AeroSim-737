using UnityEngine;

public static class ReverseThrustMath
{
    public static float UpdateSignedThrottle(
        float currentThrottle,
        bool increasePressed,
        bool decreasePressed,
        bool reverseAllowed,
        float rate,
        float deltaTime)
    {
        currentThrottle = Mathf.Clamp(currentThrottle, -1f, 1f);
        float maxDelta = Mathf.Max(0f, rate) * Mathf.Max(0f, deltaTime);

        if (increasePressed && decreasePressed)
        {
            float target = reverseAllowed ? -1f : 0f;
            return Mathf.MoveTowards(currentThrottle, target, maxDelta);
        }

        if (increasePressed)
            return Mathf.MoveTowards(currentThrottle, 1f, maxDelta);

        if (decreasePressed)
            return Mathf.MoveTowards(currentThrottle, 0f, maxDelta);

        return currentThrottle;
    }

    public static void CalculateEngineCommands(
        float signedThrottle,
        bool reverseAllowed,
        float reverseAngleRad,
        out float engineThrottle,
        out float reverserAngleRad)
    {
        bool reverseActive = reverseAllowed && signedThrottle < 0f;
        engineThrottle = reverseActive
            ? Mathf.Clamp01(-signedThrottle)
            : Mathf.Clamp01(signedThrottle);
        reverserAngleRad = reverseActive
            ? Mathf.Clamp(reverseAngleRad, Mathf.PI * 0.5f, Mathf.PI)
            : 0f;
    }
}
