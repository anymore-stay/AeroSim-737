using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class B737ThrottleLeverController : MonoBehaviour
{
    [Serializable]
    public class LeverPart
    {
        public string label;
        public Transform target;
        [HideInInspector] public string targetPath;

        [HideInInspector] public Quaternion neutralLocalRotation;
    }

    [Header("输入")]
    [Tooltip("为空时自动从 B737 根物体查找 FlightInput。")]
    [SerializeField] private FlightInput flightInput;

    [Header("油门杆")]
    [SerializeField] private LeverPart[] levers =
    {
        new LeverPart { label = "左油门杆", targetPath = "ImpEmpty.015_5ee6_81071" },
        new LeverPart { label = "右油门杆", targetPath = "ImpEmpty.020_5ee6_39963" }
    };

    [Header("角度")]
    [SerializeField] private float idleLocalXAngle = 0f;
    [SerializeField] private float fullThrottleLocalXAngle = -50f;

    private void OnValidate()
    {
        SyncTargetBindings();
    }

    private void Awake()
    {
        if (flightInput == null)
        {
            flightInput = GetComponent<FlightInput>();
        }

        SyncTargetBindings();
        CaptureNeutralPose();
        ApplyThrottle();
    }

    private void LateUpdate()
    {
        ApplyThrottle();
    }

    [ContextMenu("Capture Neutral Pose")]
    public void CaptureNeutralPose()
    {
        SyncTargetBindings();

        if (levers == null)
        {
            return;
        }

        foreach (LeverPart lever in levers)
        {
            if (lever?.target == null)
            {
                continue;
            }

            lever.neutralLocalRotation = lever.target.localRotation;
        }
    }

    private void ApplyThrottle()
    {
        if (flightInput == null || levers == null)
        {
            return;
        }

        float angle = CalculateLeverAngle(
            flightInput.Throttle,
            idleLocalXAngle,
            fullThrottleLocalXAngle);
        Quaternion delta = Quaternion.Euler(angle, 0f, 0f);

        foreach (LeverPart lever in levers)
        {
            if (lever?.target == null)
            {
                continue;
            }

            lever.target.localRotation = lever.neutralLocalRotation * delta;
        }
    }

    public static float CalculateLeverAngle(
        float signedThrottle,
        float idleLocalXAngle,
        float fullThrottleLocalXAngle)
    {
        return Mathf.Lerp(idleLocalXAngle, fullThrottleLocalXAngle, Mathf.Clamp01(signedThrottle));
    }

    [ContextMenu("Rebind Targets From Saved Paths")]
    private void RebindTargetsFromSavedPaths()
    {
        SyncTargetBindings();
    }

    private void SyncTargetBindings()
    {
        if (levers == null)
        {
            return;
        }

        foreach (LeverPart lever in levers)
        {
            if (lever == null)
            {
                continue;
            }

            SyncTargetReference(ref lever.target, ref lever.targetPath);
        }
    }

    private void SyncTargetReference(ref Transform target, ref string targetPath)
    {
        if (target != null)
        {
            string relativePath = GetRelativePath(target);
            if (!string.IsNullOrEmpty(relativePath))
            {
                targetPath = relativePath;
            }

            return;
        }

        if (string.IsNullOrEmpty(targetPath))
        {
            return;
        }

        Transform resolvedTarget = FindRelativeTarget(targetPath);
        if (resolvedTarget == null)
        {
            resolvedTarget = FindDescendantByName(transform, GetLeafName(targetPath));
        }

        if (resolvedTarget == null)
        {
            return;
        }

        target = resolvedTarget;
        targetPath = GetRelativePath(resolvedTarget);
    }

    private string GetRelativePath(Transform target)
    {
        if (target == null || target == transform)
        {
            return string.Empty;
        }

        Stack<string> segments = new Stack<string>();
        Transform current = target;

        while (current != null && current != transform)
        {
            segments.Push(current.name);
            current = current.parent;
        }

        if (current != transform || segments.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("/", segments.ToArray());
    }

    private Transform FindRelativeTarget(string targetPath)
    {
        if (string.IsNullOrEmpty(targetPath))
        {
            return null;
        }

        return transform.Find(targetPath);
    }

    private static string GetLeafName(string targetPath)
    {
        if (string.IsNullOrEmpty(targetPath))
        {
            return string.Empty;
        }

        int lastSlashIndex = targetPath.LastIndexOf('/');
        return lastSlashIndex >= 0 ? targetPath.Substring(lastSlashIndex + 1) : targetPath;
    }

    private static Transform FindDescendantByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
        {
            return null;
        }

        foreach (Transform child in root)
        {
            if (child.name == targetName)
            {
                return child;
            }

            Transform nestedMatch = FindDescendantByName(child, targetName);
            if (nestedMatch != null)
            {
                return nestedMatch;
            }
        }

        return null;
    }
}
