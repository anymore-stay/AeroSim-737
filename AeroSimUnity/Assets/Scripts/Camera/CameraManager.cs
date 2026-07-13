using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 相机管理器。
///
/// 作用：
/// - 统一管理飞机上的多个相机。
/// - 使用 Shift + 数字键切换到指定相机。
/// - 切换时自动关闭旧相机的 Camera、AudioListener 和控制脚本，再启用新相机。
///
/// 使用方式：
/// - 在 Inspector 的 Camera Slots 列表中添加相机槽位。
/// - hotkeyNumber = 7 表示 Shift+7。
/// - hotkeyNumber = 9 表示 Shift+9。
/// - cameraObject 指向对应的相机 GameObject。
/// - displayName 只用于日志显示，方便调试。
///
/// 为什么要有 RebuildSlotMap：
/// Unity 中脚本重编译、Prefab 加载、运行时注册相机、以及组件启用顺序都可能让运行时字典为空。
/// 所以这里在 Awake、OnEnable、Start 和手动切换时都允许重建热键表，保证按键切换稳定。
/// </summary>
public class CameraManager : MonoBehaviour
{
    [Serializable]
    public class CameraSlot
    {
        [Tooltip("Shift + 这个数字键会激活此相机。例如填 7，就是 Shift+7。")]
        [Range(0, 9)]
        public int hotkeyNumber;

        [Tooltip("要切换到的相机物体。推荐挂 CockpitCameraController，这样切换时能正确启用/禁用控制逻辑。")]
        public GameObject cameraObject;

        [Tooltip("显示名称，只用于 Console 日志，不影响功能。")]
        public string displayName;
    }

    [Header("相机注册")]
    [Tooltip("所有可切换相机都登记在这里。")]
    [SerializeField]
    private List<CameraSlot> cameraSlots = new List<CameraSlot>();

    [Header("起始相机")]
    [Tooltip("进入 Play Mode 后默认启用的槽位索引。填 0 表示列表第一个；填 -1 表示不自动启用任何相机。")]
    [SerializeField]
    private int defaultSlotIndex = 0;

    private CameraSlot currentSlot;
    private Dictionary<int, CameraSlot> slotMap = new Dictionary<int, CameraSlot>();

    private static CameraManager instance;
    public static CameraManager Instance { get { return instance; } }
    public Camera ActiveCamera { get; private set; }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            // 这里不能 Destroy(gameObject)。
            // CameraManager 挂在飞机 prefab 根节点上，如果场景里已经有一个管理器，
            // 再拖入一架带 CameraManager 的飞机时，销毁 gameObject 会把整架飞机删掉。
            // Unity 编辑器拖拽流程还会继续访问这个刚被销毁的 GameObject，
            // 从而触发 MissingReferenceException。
            //
            // 正确做法是只停用重复的管理器组件，让飞机模型本身继续存在。
            enabled = false;
            return;
        }

        instance = this;
        RebuildSlotMap();
    }

    void OnEnable()
    {
        if (instance != null && instance != this)
        {
            enabled = false;
            return;
        }

        if (instance == null)
        {
            instance = this;
        }

        RebuildSlotMap();
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    void Start()
    {
        RebuildSlotMap();
        DisableAllRegisteredCameras();
        ActiveCamera = null;

        if (defaultSlotIndex >= 0 && defaultSlotIndex < cameraSlots.Count)
        {
            SwitchToSlot(cameraSlots[defaultSlotIndex]);
        }
    }

    void Update()
    {
        if (slotMap.Count == 0)
        {
            RebuildSlotMap();
        }

        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
        {
            return;
        }

        for (int i = 0; i <= 9; i++)
        {
            if (!Input.GetKeyDown(KeyCode.Alpha0 + i) && !Input.GetKeyDown(KeyCode.Keypad0 + i))
            {
                continue;
            }

            SwitchTo(i);
            break;
        }
    }

    /// <summary>
    /// 从 Inspector 的 cameraSlots 列表重建运行时字典。
    /// 字典用于把数字键快速映射到相机槽位。
    /// 如果出现重复热键，保留第一个有效槽位，忽略后面的重复项。
    /// </summary>
    private void RebuildSlotMap()
    {
        slotMap.Clear();

        for (int i = 0; i < cameraSlots.Count; i++)
        {
            CameraSlot slot = cameraSlots[i];
            if (slot == null || slot.cameraObject == null)
            {
                continue;
            }

            if (slotMap.ContainsKey(slot.hotkeyNumber))
            {
                continue;
            }

            slotMap.Add(slot.hotkeyNumber, slot);
        }
    }

    private void DisableAllRegisteredCameras()
    {
        for (int i = 0; i < cameraSlots.Count; i++)
        {
            CameraSlot slot = cameraSlots[i];
            if (slot == null || slot.cameraObject == null)
            {
                continue;
            }

            SetCameraSlotActive(slot, false);
        }
    }

    private void SwitchToSlot(CameraSlot slot)
    {
        if (slot == null || slot.cameraObject == null)
        {
            return;
        }

        if (currentSlot != null && currentSlot.cameraObject != null)
        {
            SetCameraSlotActive(currentSlot, false);
        }

        SetCameraSlotActive(slot, true);
        currentSlot = slot;
        ActiveCamera = slot.cameraObject.GetComponent<Camera>();

        Debug.Log("[CameraManager] 切换到: " + slot.displayName + " (Shift+" + slot.hotkeyNumber + ")");
    }

    private void SetCameraSlotActive(CameraSlot slot, bool active)
    {
        CockpitCameraController controller = slot.cameraObject.GetComponent<CockpitCameraController>();
        if (controller != null)
        {
            controller.SetActive(active);
            return;
        }

        Camera cameraComponent = slot.cameraObject.GetComponent<Camera>();
        if (cameraComponent != null)
        {
            cameraComponent.enabled = active;
        }

        AudioListener audioListener = slot.cameraObject.GetComponent<AudioListener>();
        if (audioListener != null)
        {
            audioListener.enabled = active;
        }
    }

    /// <summary>
    /// 手动切换到指定热键对应的相机。
    /// 例如 SwitchTo(7) 等价于玩家按 Shift+7。
    /// </summary>
    public void SwitchTo(int hotkeyNumber)
    {
        if (slotMap.Count == 0)
        {
            RebuildSlotMap();
        }

        CameraSlot slot;
        if (slotMap.TryGetValue(hotkeyNumber, out slot))
        {
            SwitchToSlot(slot);
        }
    }

    /// <summary>
    /// 运行时注册新相机。
    /// 这个方法适合代码动态生成相机时使用；Inspector 里已经填好的相机不需要调用它。
    /// </summary>
    public void RegisterCamera(int hotkeyNumber, GameObject cameraObject, string displayName = "")
    {
        if (cameraObject == null)
        {
            return;
        }

        if (slotMap.Count == 0)
        {
            RebuildSlotMap();
        }

        if (slotMap.ContainsKey(hotkeyNumber))
        {
            Debug.LogWarning("[CameraManager] 热键 " + hotkeyNumber + " 已被占用");
            return;
        }

        CameraSlot slot = new CameraSlot
        {
            hotkeyNumber = hotkeyNumber,
            cameraObject = cameraObject,
            displayName = string.IsNullOrEmpty(displayName) ? cameraObject.name : displayName
        };

        cameraSlots.Add(slot);
        slotMap.Add(hotkeyNumber, slot);
        SetCameraSlotActive(slot, false);
    }
}
