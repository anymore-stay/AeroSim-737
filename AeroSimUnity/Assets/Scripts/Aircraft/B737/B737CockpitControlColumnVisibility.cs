using UnityEngine;

/// <summary>
/// 仅在驾驶舱相机启用时，使用数字键 1 切换操纵杆模型的显示状态。
/// </summary>
public class B737CockpitControlColumnVisibility : MonoBehaviour
{
    private const string TargetObjectName = "ImpEmpty.001_x24e_47969";

    [Header("操纵杆目标")]
    [SerializeField] private GameObject controlColumnTarget;

    [Header("驾驶舱相机")]
    [SerializeField] private CockpitCameraController cockpitCameraController;

    [Header("快捷键")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Alpha1;

    private bool missingTargetWarningLogged;

    public bool IsControlColumnVisible => controlColumnTarget != null && controlColumnTarget.activeSelf;

    private void Awake()
    {
        EnsureBindings();
        if (controlColumnTarget != null)
        {
            // 进入 Play 时始终从显示状态开始。
            controlColumnTarget.SetActive(true);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            TryToggle(IsCockpitCameraActive());
        }
    }

    /// <summary>
    /// 设置目标和相机引用，供 Prefab 配置与自动测试使用。
    /// </summary>
    public void SetBindings(GameObject target, CockpitCameraController cameraController)
    {
        controlColumnTarget = target;
        cockpitCameraController = cameraController;
        missingTargetWarningLogged = false;
    }

    /// <summary>
    /// 尝试切换操纵杆显示。只有驾驶舱相机处于活动状态时才执行。
    /// </summary>
    public bool TryToggle(bool cameraIsActive)
    {
        EnsureBindings();
        if (!cameraIsActive
            || cockpitCameraController == null
            || cockpitCameraController.cameraMode != CockpitCameraController.CameraMode.Cockpit
            || controlColumnTarget == null)
        {
            return false;
        }

        controlColumnTarget.SetActive(!controlColumnTarget.activeSelf);
        return true;
    }

    private bool IsCockpitCameraActive()
    {
        if (cockpitCameraController == null
            || cockpitCameraController.cameraMode != CockpitCameraController.CameraMode.Cockpit
            || !cockpitCameraController.isActiveAndEnabled
            || !cockpitCameraController.gameObject.activeInHierarchy)
        {
            return false;
        }

        Camera cameraComponent = cockpitCameraController.GetComponent<Camera>();
        return cameraComponent != null && cameraComponent.enabled;
    }

    private void EnsureBindings()
    {
        if (controlColumnTarget == null)
        {
            Transform target = FindDescendantByName(transform, TargetObjectName);
            if (target != null)
            {
                controlColumnTarget = target.gameObject;
            }
        }

        if (cockpitCameraController == null)
        {
            CockpitCameraController[] controllers =
                GetComponentsInChildren<CockpitCameraController>(true);
            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i].cameraMode == CockpitCameraController.CameraMode.Cockpit)
                {
                    cockpitCameraController = controllers[i];
                    break;
                }
            }
        }

        if (controlColumnTarget == null && !missingTargetWarningLogged)
        {
            missingTargetWarningLogged = true;
            Debug.LogWarning("[B737] 未找到操纵杆节点 ImpEmpty.001_x24e_47969，数字键 1 显隐功能不可用。", this);
        }
    }

    private static Transform FindDescendantByName(Transform root, string targetName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == targetName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDescendantByName(root.GetChild(i), targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
