using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coordinates landing gear toggle input across independent gear controllers.
/// Controllers that receive the same key press in the same frame can all start,
/// then later toggle presses are ignored until every registered motion finishes.
/// </summary>
public static class LandingGearToggleGate
{
    private static readonly HashSet<int> ActiveMotionOwners = new HashSet<int>();
    private static int acceptedToggleFrame = -1;
    private static bool hasAcceptedToggleTarget;
    private static bool acceptedToggleTargetExtended;

    public static bool HasActiveMotion => ActiveMotionOwners.Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        ActiveMotionOwners.Clear();
        acceptedToggleFrame = -1;
        hasAcceptedToggleTarget = false;
        acceptedToggleTargetExtended = false;
    }

    public static bool CanUseToggleInputThisFrame
    {
        get
        {
            int frame = Time.frameCount;
            return !HasActiveMotion || acceptedToggleFrame == frame;
        }
    }

    public static bool TryBeginMotion(Object owner)
    {
        int frame = Time.frameCount;

        if (HasActiveMotion && acceptedToggleFrame != frame)
        {
            return false;
        }

        acceptedToggleFrame = frame;

        if (owner != null)
        {
            ActiveMotionOwners.Add(owner.GetInstanceID());
        }

        return true;
    }

    public static bool TryGetToggleTarget(Object owner, bool currentExtended, out bool targetExtended)
    {
        int frame = Time.frameCount;

        if (HasActiveMotion && acceptedToggleFrame != frame)
        {
            targetExtended = currentExtended;
            return false;
        }

        if (acceptedToggleFrame != frame || !hasAcceptedToggleTarget)
        {
            acceptedToggleFrame = frame;
            acceptedToggleTargetExtended = !currentExtended;
            hasAcceptedToggleTarget = true;
        }

        targetExtended = acceptedToggleTargetExtended;
        return true;
    }

    public static void EndMotion(Object owner)
    {
        if (owner == null)
        {
            return;
        }

        ActiveMotionOwners.Remove(owner.GetInstanceID());
    }
}
