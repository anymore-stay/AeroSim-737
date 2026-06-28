using System;
using UnityEngine;

[DisallowMultipleComponent]
public class B737FlapController : MonoBehaviour
{
    [Serializable]
    public class FlapPart
    {
        public string label;
        public Transform target;
        [HideInInspector] public string targetPath;
        public Vector3 deployedLocalPositionOffset;
        public Vector3 deployedLocalEuler;
        public AnimationCurve motionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [HideInInspector] public Vector3 neutralLocalPosition;
        [HideInInspector] public Quaternion neutralLocalRotation;
    }

    [Header("Input")]
    [SerializeField] private bool useKeyboardInput = true;
    [SerializeField] private KeyCode extendKey = KeyCode.F;
    [SerializeField] private KeyCode retractKey = KeyCode.V;
    [SerializeField] private float flapMoveSpeed = 0.6f;
    [SerializeField] private bool startDeployed;

    [Header("Flaps")]
    [SerializeField] private FlapPart[] flapParts = Array.Empty<FlapPart>();

    [Header("Debug")]
    [SerializeField, Range(0f, 1f)] private float flapInput;

    public float FlapInput => flapInput;

    private void OnValidate()
    {
        SyncTargetBindings();
    }

    private void Awake()
    {
        SyncTargetBindings();
        CaptureNeutralPose();
        flapInput = startDeployed ? 1f : 0f;
        ApplyFlaps();
    }

    private void Update()
    {
        if (useKeyboardInput)
        {
            UpdateKeyboardInput();
        }

        ApplyFlaps();
    }

    [ContextMenu("Capture Neutral Pose")]
    public void CaptureNeutralPose()
    {
        SyncTargetBindings();

        if (flapParts == null)
        {
            return;
        }

        foreach (FlapPart part in flapParts)
        {
            if (part == null || part.target == null)
            {
                continue;
            }

            part.neutralLocalPosition = part.target.localPosition;
            part.neutralLocalRotation = part.target.localRotation;
        }
    }

    public void SetFlapInput(float value, bool snapImmediately = false)
    {
        flapInput = Mathf.Clamp01(value);

        if (snapImmediately)
        {
            ApplyFlaps();
        }
    }

    public void SetFlapExtended(bool extended, bool snapImmediately = false)
    {
        SetFlapInput(extended ? 1f : 0f, snapImmediately);
    }

    private void UpdateKeyboardInput()
    {
        float direction = 0f;

        if (Input.GetKey(extendKey) && !Input.GetKey(retractKey))
        {
            direction = 1f;
        }
        else if (Input.GetKey(retractKey) && !Input.GetKey(extendKey))
        {
            direction = -1f;
        }

        flapInput = Mathf.Clamp01(flapInput + direction * flapMoveSpeed * Time.deltaTime);
    }

    private void ApplyFlaps()
    {
        if (flapParts == null)
        {
            return;
        }

        foreach (FlapPart part in flapParts)
        {
            if (part == null || part.target == null)
            {
                continue;
            }

            float blend = part.motionCurve == null ? flapInput : part.motionCurve.Evaluate(flapInput);
            part.target.localPosition = part.neutralLocalPosition + part.deployedLocalPositionOffset * blend;
            part.target.localRotation = part.neutralLocalRotation * Quaternion.Euler(part.deployedLocalEuler * blend);
        }
    }

    [ContextMenu("Rebind Targets From Saved Paths")]
    private void RebindTargetsFromSavedPaths()
    {
        SyncTargetBindings();
    }

    private void SyncTargetBindings()
    {
        if (flapParts == null)
        {
            return;
        }

        foreach (FlapPart part in flapParts)
        {
            if (part == null)
            {
                continue;
            }

            if (part.target != null)
            {
                string relativePath = GetRelativePath(part.target);
                if (!string.IsNullOrEmpty(relativePath))
                {
                    part.targetPath = relativePath;
                }

                continue;
            }

            if (string.IsNullOrEmpty(part.targetPath))
            {
                continue;
            }

            Transform resolvedTarget = transform.Find(part.targetPath);
            if (resolvedTarget == null)
            {
                resolvedTarget = FindDescendantByName(transform, GetLeafName(part.targetPath));
            }

            if (resolvedTarget == null)
            {
                continue;
            }

            part.target = resolvedTarget;
            part.targetPath = GetRelativePath(resolvedTarget);
        }
    }

    private string GetRelativePath(Transform target)
    {
        if (target == null || target == transform)
        {
            return string.Empty;
        }

        System.Collections.Generic.Stack<string> segments = new System.Collections.Generic.Stack<string>();
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
