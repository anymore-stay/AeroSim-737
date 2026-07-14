using UnityEngine;

/// <summary>
/// 飞机相机控制器。
///
/// 这个脚本同时支持三种相机模式：
/// 1. Cockpit：驾驶舱自由视角。按住鼠标右键转头，方向键移动，PageUp/PageDown 上下移动。
/// 2. Cabin：客舱自由视角。操作方式和驾驶舱一致，但使用客舱自己的活动范围。
/// 3. ThirdPerson：第三人称环绕视角。围绕飞机旋转，鼠标右键控制角度，滚轮控制距离，并自动避障。
///
/// Cockpit/Cabin 的移动范围：
/// - minX/maxX/minY/maxY/minZ/maxZ 都是“相对相机初始位置”的本地坐标偏移。
/// - 相机作为飞机 prefab 子物体时，范围会跟随飞机移动。
///
/// ThirdPerson 的移动方式：
/// - 不使用方向键自由飞行。
/// - 以飞机整体 Renderer Bounds 的中心作为默认观察点。
/// - 鼠标右键拖动改变水平角和俯仰角。
/// - 鼠标滚轮改变相机到观察点的距离。
/// - 如果相机目标位置进入飞机整体包围盒，会被推出到外部，避免看进机体内部。
/// - 如果场景里有 Collider，会用 SphereCast 做自动避障，防止相机穿墙或穿进机身。
/// </summary>
public class CockpitCameraController : MonoBehaviour
{
    public enum CameraMode
    {
        Cockpit,
        Cabin,
        ThirdPerson
    }

    [Header("相机模式")]
    [Tooltip("驾驶舱、客舱、第三人称使用同一个脚本，但控制逻辑和限制规则不同。")]
    public CameraMode cameraMode = CameraMode.Cockpit;

    [Header("驾驶舱/客舱移动速度")]
    [Tooltip("Cockpit/Cabin 模式下方向键和 PageUp/PageDown 的基础移动速度。ThirdPerson 模式不使用这个移动值。")]
    public float moveSpeed = 1.5f;

    [Tooltip("Cockpit/Cabin 模式下按住 Shift 时的速度倍率。")]
    public float shiftSpeedMultiplier = 2f;

    [Header("驾驶舱/客舱视角灵敏度")]
    [Tooltip("Cockpit/Cabin 模式下，按住鼠标右键时鼠标控制视角旋转的灵敏度。")]
    public float lookSensitivity = 2f;

    [Tooltip("Cockpit/Cabin 模式下允许向下看的最大角度。")]
    public float minPitch = -80f;

    [Tooltip("Cockpit/Cabin 模式下允许向上看的最大角度。")]
    public float maxPitch = 80f;

    [Header("POV 帽视角控制")]
    [Tooltip("为空时自动从飞机根节点查找图马思特侧杆输入组件。")]
    [InspectorName("侧杆输入组件")]
    public ThrustmasterA320SidestickInput sidestickInput;

    [InspectorName("启用 POV 帽控制视角")]
    public bool enablePovLook = true;

    [Tooltip("按住 POV 帽时每秒旋转的角度。")]
    [InspectorName("POV 视角旋转速度")]
    [Min(0f)]
    public float povLookSpeed = 90f;

    [Header("驾驶舱/客舱活动范围（相对初始位置，本地坐标）")]
    [Tooltip("相对初始位置，本地 X 负方向最远距离。")]
    public float minX = -1f;

    [Tooltip("相对初始位置，本地 X 正方向最远距离。")]
    public float maxX = 0.3f;

    [Tooltip("相对初始位置，本地 Y 负方向最远距离。")]
    public float minY = -0.25f;

    [Tooltip("相对初始位置，本地 Y 正方向最远距离。")]
    public float maxY = 0.35f;

    [Tooltip("相对初始位置，本地 Z 负方向最远距离。")]
    public float minZ = -0.6f;

    [Tooltip("相对初始位置，本地 Z 正方向最远距离。")]
    public float maxZ = 0.8f;

    [Header("第三人称目标")]
    [Tooltip("第三人称观察目标。为空时自动使用飞机整体 Renderer Bounds 的中心。")]
    public Transform orbitTarget;

    [Tooltip("第三人称观察点偏移。默认观察飞机中心；如果想看机头或机尾，可调整这个偏移。")]
    public Vector3 orbitTargetOffset = Vector3.zero;

    [Tooltip("用于计算飞机包围盒和防穿模的飞机根节点。为空时自动使用本相机所在 prefab 的根节点。")]
    public Transform aircraftRoot;

    [Tooltip("第三人称视角为空 Orbit Target 时，启动时固定一次观察点，避免轮子等动画部件改变 Renderer Bounds 后造成相机抖动。")]
    public bool useStableInitialBoundsTarget = true;

    [Header("第三人称环绕")]
    [Tooltip("第三人称初始距离。进入视角时会夹在最小/最大距离之间。")]
    public float thirdPersonDistance = 48f;

    [Tooltip("第三人称初始水平角。0 通常表示从飞机后方/局部 Z 正方向观察，180 表示反方向。")]
    public float thirdPersonInitialYaw = 0f;

    [Tooltip("第三人称初始俯仰角。在当前环绕公式下，负数表示相机在观察点上方。")]
    public float thirdPersonInitialPitch = -8f;

    [Tooltip("启用后，第三人称每次激活都使用上面的初始角度和距离；关闭后才会从当前 Transform 反推角度。")]
    public bool useConfiguredInitialOrbit = true;

    [Tooltip("第三人称允许的最近距离。距离太近容易穿进机身或看到内部。")]
    public float thirdPersonMinDistance = 18f;

    [Tooltip("第三人称允许的最远距离。")]
    public float thirdPersonMaxDistance = 95f;

    [Tooltip("鼠标滚轮缩放速度。")]
    public float thirdPersonZoomSpeed = 10f;

    [Tooltip("按住右键拖动时，第三人称水平/俯仰旋转灵敏度。")]
    public float thirdPersonOrbitSensitivity = 3f;

    [Tooltip("第三人称最低俯仰角。-89.5 可让相机到飞机正上方并向下观察。")]
    public float thirdPersonMinPitch = -89.5f;

    [Tooltip("第三人称最高俯仰角。")]
    public float thirdPersonMaxPitch = 70f;

    [Header("第三人称低空视角限制")]
    [Tooltip("开启后，飞机低于指定离地高度时，第三人称相机不能旋转到机身下方。")]
    public bool restrictBelowAircraftNearGround = true;

    [Tooltip("低于该离地高度时启用机身下方视角限制，单位为英尺。")]
    [Min(0f)]
    public float thirdPersonBelowAircraftRestrictionAglFt = 100f;

    [Tooltip("低空限制生效时允许的最大第三人称 Pitch。0 表示相机最低只能与机身观察点水平，不能到机身下方。")]
    [Range(-89.5f, 0f)]
    public float thirdPersonLowAltitudeMaxPitch = 0f;

    [Header("第三人称自动避障")]
    [Tooltip("第三人称相机离飞机整体包围盒的最小安全距离。越大越不容易穿进机身，但也越不能贴近飞机。")]
    public float thirdPersonKeepoutPadding = 1.5f;

    [Tooltip("是否用飞机包围盒防止第三人称相机进入机体内部。")]
    public bool preventThirdPersonInsideAircraft = true;

    [Tooltip("是否使用 Physics.SphereCast 做自动避障。需要飞机或环境物体有 Collider 才会生效。")]
    public bool usePhysicsObstruction = true;

    [Tooltip("自动避障检测时把相机看成多大的球。半径越大，越不容易贴穿模型。")]
    public float thirdPersonCollisionRadius = 0.45f;

    [Tooltip("SphereCast 命中障碍物后，额外向外退开的距离。")]
    public float thirdPersonCollisionPadding = 0.25f;

    [Tooltip("自动避障检测的 Layer。默认 Everything。")]
    public LayerMask thirdPersonObstructionMask = ~0;

    [Header("近裁剪面")]
    [Tooltip("相机近裁剪面。驾驶舱/客舱可以较小；第三人称建议 0.1 以上，避免贴近模型时把机身裁开。")]
    public float nearClipPlane = 0.01f;

    private Rigidbody rb;
    private Camera cam;
    private Vector3 startLocalPos;
    private float yaw;
    private float pitch;
    private bool isLooking;
    private bool hasMoveInput;
    private bool hasLookInput;
    private float moveInputX;
    private float moveInputY;
    private float moveInputZ;
    private float speedMultiplier = 1f;

    private bool hasAircraftBounds;
    private Bounds aircraftBounds;
    private bool hasStableOrbitTarget;
    private Transform stableOrbitRoot;
    private Vector3 stableOrbitTargetLocalPoint;
    private float orbitYaw;
    private float orbitPitch;
    private float orbitDistance;
    private bool orbitDirty = true;

    void Awake()
    {
        EnsurePhysicsComponents();
        EnsureCameraSettings();
        ResolveSidestickInput();

        startLocalPos = transform.localPosition;
        // 用本地欧拉角初始化(相机是飞机子物体,视角在机体坐标系内表示)
        yaw = transform.localEulerAngles.y;
        pitch = transform.localEulerAngles.x;

        CacheAircraftBounds();

        if (cameraMode == CameraMode.ThirdPerson)
        {
            CaptureStableOrbitTarget();
            InitializeThirdPersonOrbit();
        }

        if (CameraManager.Instance == null)
        {
            SetActive(true);
        }
    }

    void Update()
    {
        if (cameraMode == CameraMode.ThirdPerson)
        {
            ReadThirdPersonInput();
            ApplyThirdPersonOrbit();
            return;
        }

        ReadLookInput();
        ReadMoveInput();
        ApplyLook();
        ApplyMove();
    }

    private void EnsurePhysicsComponents()
    {
        // 这些相机是高速飞行的飞机(被 JsbsimBridge 用 Transform 直接赋值驱动)的子物体。
        // 绝对不能挂 Rigidbody:运动学刚体的世界位姿由 PhysX 接管,不会跟随父物体的
        // Transform 直接赋值,飞机一高速移动相机就被锁在世界坐标、被甩开几千米。
        // 因此这里主动移除可能存在的 Rigidbody 和自动加的 SphereCollider,改用纯 Transform 控制。
        Rigidbody existingRb = GetComponent<Rigidbody>();
        if (existingRb != null)
        {
            Destroy(existingRb);
        }
        rb = null;

        SphereCollider existingCollider = GetComponent<SphereCollider>();
        if (existingCollider != null)
        {
            Destroy(existingCollider);
        }
    }

    private void EnsureCameraSettings()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = gameObject.AddComponent<Camera>();
        }

        if (cameraMode == CameraMode.ThirdPerson && nearClipPlane < 0.1f)
        {
            nearClipPlane = 0.1f;
        }

        cam.nearClipPlane = nearClipPlane;
    }

    private void ReadLookInput()
    {
        if (Input.GetMouseButtonDown(1))
        {
            BeginMouseControl();
        }

        if (Input.GetMouseButtonUp(1))
        {
            EndMouseControl();
        }

        Vector2 povLook = ReadPovLookDirection();
        bool hasPovLook = povLook.sqrMagnitude > 0.0001f;

        if (isLooking)
        {
            yaw += Input.GetAxis("Mouse X") * lookSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
        }

        if (hasPovLook)
        {
            float povDelta = povLookSpeed * Time.unscaledDeltaTime;
            yaw += povLook.x * povDelta;
            pitch -= povLook.y * povDelta;
        }

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        hasLookInput = isLooking || hasPovLook;
    }

    private void ApplyLook()
    {
        if (!hasLookInput)
        {
            return;
        }

        // 用本地旋转:相机是飞机子物体,yaw/pitch 表示相对机体的朝向。
        // 不能用 rb.MoveRotation(世界坐标),否则会和高速移动的父物体变换冲突。
        transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void ReadMoveInput()
    {
        float h = 0f;
        float v = 0f;
        float u = 0f;

        if (Input.GetKey(KeyCode.LeftArrow)) h = -1f;
        if (Input.GetKey(KeyCode.RightArrow)) h = 1f;
        if (Input.GetKey(KeyCode.UpArrow)) v = 1f;
        if (Input.GetKey(KeyCode.DownArrow)) v = -1f;
        if (Input.GetKey(KeyCode.PageUp)) u = 1f;
        if (Input.GetKey(KeyCode.PageDown)) u = -1f;

        moveInputX = h;
        moveInputY = u;
        moveInputZ = v;
        hasMoveInput = !Mathf.Approximately(h, 0f) || !Mathf.Approximately(v, 0f) || !Mathf.Approximately(u, 0f);

        speedMultiplier = 1f;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            speedMultiplier = shiftSpeedMultiplier;
        }
    }

    private void ApplyMove()
    {
        if (!hasMoveInput)
        {
            return;
        }

        // 用本地坐标移动:相机是飞机子物体,移动应在机体坐标系内进行。
        // 不能用 rb.MovePosition(世界坐标),否则高速移动的父物体会把移动量淹没。
        // 移动方向基于相机当前本地朝向,让"前后左右"跟随视角。
        Vector3 localDir = transform.localRotation * new Vector3(moveInputX, moveInputY, moveInputZ);
        if (localDir.sqrMagnitude > 1f) localDir.Normalize();

        float speed = moveSpeed * speedMultiplier;
        Vector3 targetLocal = transform.localPosition + localDir * speed * Time.deltaTime;

        // 限制在活动范围内(相对初始本地位置)
        Vector3 offset = targetLocal - startLocalPos;
        offset.x = Mathf.Clamp(offset.x, minX, maxX);
        offset.y = Mathf.Clamp(offset.y, minY, maxY);
        offset.z = Mathf.Clamp(offset.z, minZ, maxZ);

        transform.localPosition = startLocalPos + offset;
    }

    private void ReadThirdPersonInput()
    {
        if (Input.GetMouseButtonDown(1))
        {
            BeginMouseControl();
        }

        if (Input.GetMouseButtonUp(1))
        {
            EndMouseControl();
        }

        if (isLooking)
        {
            orbitYaw += Input.GetAxis("Mouse X") * thirdPersonOrbitSensitivity;
            orbitPitch -= Input.GetAxis("Mouse Y") * thirdPersonOrbitSensitivity;
            orbitDirty = true;
        }

        Vector2 povLook = ReadPovLookDirection();
        if (povLook.sqrMagnitude > 0.0001f)
        {
            float povDelta = povLookSpeed * Time.unscaledDeltaTime;
            orbitYaw += povLook.x * povDelta;
            orbitPitch -= povLook.y * povDelta;
            orbitDirty = true;
        }

        ClampThirdPersonPitch();

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (!Mathf.Approximately(scroll, 0f))
        {
            orbitDistance -= scroll * thirdPersonZoomSpeed;
            orbitDistance = Mathf.Clamp(orbitDistance, thirdPersonMinDistance, thirdPersonMaxDistance);
            orbitDirty = true;
        }
    }

    private void ApplyThirdPersonOrbit()
    {
        // 每帧都要更新:飞机在持续高速移动,环绕目标点(飞机包围盒中心)随之变化,
        // 不能因为没有鼠标输入(orbitDirty=false)就跳过,否则相机会被飞机甩开。
        CacheAircraftBounds();
        ClampThirdPersonPitch();

        Vector3 targetPoint = GetOrbitTargetPoint();
        Quaternion orbitRotation = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
        Vector3 desiredPosition = targetPoint + orbitRotation * Vector3.forward * orbitDistance;
        desiredPosition = ResolveThirdPersonObstruction(targetPoint, desiredPosition);

        // 直接设世界坐标 transform,不用 rb.MovePosition:
        // 运动学刚体的物理移动在高速父物体下会有一帧滞后并被锁住。
        transform.position = desiredPosition;

        Vector3 lookDirection = targetPoint - desiredPosition;
        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }

        orbitDirty = false;
    }

    private void InitializeThirdPersonOrbit()
    {
        CacheAircraftBounds();

        if (useConfiguredInitialOrbit)
        {
            orbitYaw = thirdPersonInitialYaw;
            orbitPitch = Mathf.Clamp(thirdPersonInitialPitch, thirdPersonMinPitch, GetEffectiveThirdPersonMaxPitch());
            orbitDistance = Mathf.Clamp(thirdPersonDistance, thirdPersonMinDistance, thirdPersonMaxDistance);
            orbitDirty = true;
            return;
        }

        Vector3 targetPoint = GetOrbitTargetPoint();
        Vector3 toCamera = transform.position - targetPoint;

        if (toCamera.sqrMagnitude < 0.0001f)
        {
            toCamera = Vector3.back * thirdPersonDistance;
        }

        orbitDistance = Mathf.Clamp(toCamera.magnitude, thirdPersonMinDistance, thirdPersonMaxDistance);
        orbitYaw = Mathf.Atan2(toCamera.x, toCamera.z) * Mathf.Rad2Deg;
        orbitPitch = Mathf.Asin(Mathf.Clamp(toCamera.y / toCamera.magnitude, -1f, 1f)) * Mathf.Rad2Deg;
        ClampThirdPersonPitch();
        orbitDirty = true;
    }

    private void ClampThirdPersonPitch()
    {
        float effectiveMaxPitch = Mathf.Max(thirdPersonMinPitch, GetEffectiveThirdPersonMaxPitch());
        float clampedPitch = Mathf.Clamp(orbitPitch, thirdPersonMinPitch, effectiveMaxPitch);
        if (!Mathf.Approximately(orbitPitch, clampedPitch))
        {
            orbitPitch = clampedPitch;
            orbitDirty = true;
        }
    }

    private float GetEffectiveThirdPersonMaxPitch()
    {
        if (!restrictBelowAircraftNearGround)
            return thirdPersonMaxPitch;

        JsbsimBridge bridge = JsbsimBridge.Instance;
        bool hasUsableAgl = bridge != null &&
                            bridge.HasState &&
                            !float.IsNaN(bridge.AglFt) &&
                            !float.IsInfinity(bridge.AglFt);
        bool isNearGround = !hasUsableAgl ||
                            bridge.AglFt < Mathf.Max(0f, thirdPersonBelowAircraftRestrictionAglFt);

        return isNearGround
            ? Mathf.Min(thirdPersonMaxPitch, thirdPersonLowAltitudeMaxPitch)
            : thirdPersonMaxPitch;
    }

    private Vector3 GetOrbitTargetPoint()
    {
        if (orbitTarget != null)
        {
            return orbitTarget.TransformPoint(orbitTargetOffset);
        }

        if (useStableInitialBoundsTarget && hasStableOrbitTarget && stableOrbitRoot != null)
        {
            return stableOrbitRoot.TransformPoint(stableOrbitTargetLocalPoint) + orbitTargetOffset;
        }

        if (!hasAircraftBounds)
        {
            CacheAircraftBounds();
        }

        if (hasAircraftBounds)
        {
            return aircraftBounds.center + orbitTargetOffset;
        }

        Transform root = aircraftRoot != null ? aircraftRoot : transform.root;
        if (root != null)
        {
            return root.TransformPoint(orbitTargetOffset);
        }

        return transform.position + orbitTargetOffset;
    }

    private void CaptureStableOrbitTarget()
    {
        if (!useStableInitialBoundsTarget || orbitTarget != null)
        {
            return;
        }

        Transform root = aircraftRoot != null ? aircraftRoot : transform.root;
        if (root == null)
        {
            return;
        }

        if (!hasAircraftBounds)
        {
            CacheAircraftBounds();
        }

        if (!hasAircraftBounds)
        {
            return;
        }

        stableOrbitRoot = root;
        stableOrbitTargetLocalPoint = root.InverseTransformPoint(aircraftBounds.center);
        hasStableOrbitTarget = true;
    }

    private Vector3 ResolveThirdPersonObstruction(Vector3 targetPoint, Vector3 desiredPosition)
    {
        Vector3 safePosition = desiredPosition;

        if (usePhysicsObstruction)
        {
            Vector3 toDesired = desiredPosition - targetPoint;
            float distance = toDesired.magnitude;

            if (distance > 0.001f)
            {
                Vector3 direction = toDesired / distance;
                Transform rootToIgnore = aircraftRoot != null ? aircraftRoot : transform.root;
                RaycastHit[] hits = Physics.SphereCastAll(
                    targetPoint,
                    thirdPersonCollisionRadius,
                    direction,
                    distance,
                    thirdPersonObstructionMask,
                    QueryTriggerInteraction.Ignore);

                System.Array.Sort(hits, delegate (RaycastHit a, RaycastHit b)
                {
                    return a.distance.CompareTo(b.distance);
                });

                for (int i = 0; i < hits.Length; i++)
                {
                    RaycastHit hit = hits[i];
                    if (hit.collider == null)
                    {
                        continue;
                    }

                    Transform hitTransform = hit.collider.transform;
                    if (hitTransform == transform || hitTransform.IsChildOf(transform))
                    {
                        continue;
                    }

                    if (rootToIgnore != null && hitTransform.root == rootToIgnore)
                    {
                        continue;
                    }

                    safePosition = hit.point - direction * thirdPersonCollisionPadding;
                    break;
                }
            }
        }

        if (preventThirdPersonInsideAircraft)
        {
            safePosition = KeepOutsideAircraft(safePosition);
        }

        return safePosition;
    }

    private Vector3 ClampToLocalMovementBox(Vector3 targetWorld)
    {
        Vector3 targetLocal = GetLocalPosition(targetWorld);
        Vector3 offset = targetLocal - startLocalPos;

        offset.x = Mathf.Clamp(offset.x, minX, maxX);
        offset.y = Mathf.Clamp(offset.y, minY, maxY);
        offset.z = Mathf.Clamp(offset.z, minZ, maxZ);

        return GetWorldPosition(startLocalPos + offset);
    }

    private Vector3 GetLocalPosition(Vector3 worldPosition)
    {
        if (transform.parent == null)
        {
            return worldPosition;
        }

        return transform.parent.InverseTransformPoint(worldPosition);
    }

    private Vector3 GetWorldPosition(Vector3 localPosition)
    {
        if (transform.parent == null)
        {
            return localPosition;
        }

        return transform.parent.TransformPoint(localPosition);
    }

    private void CacheAircraftBounds()
    {
        Transform root = aircraftRoot;
        if (root == null)
        {
            root = transform.root;
        }

        if (root == null)
        {
            hasAircraftBounds = false;
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        hasAircraftBounds = false;

        foreach (Renderer rendererComponent in renderers)
        {
            if (rendererComponent == null || rendererComponent is ParticleSystemRenderer || rendererComponent is TrailRenderer || rendererComponent is LineRenderer)
            {
                continue;
            }

            if (!hasAircraftBounds)
            {
                aircraftBounds = rendererComponent.bounds;
                hasAircraftBounds = true;
            }
            else
            {
                aircraftBounds.Encapsulate(rendererComponent.bounds);
            }
        }
    }

    private Vector3 KeepOutsideAircraft(Vector3 targetWorld)
    {
        if (!hasAircraftBounds)
        {
            CacheAircraftBounds();
        }

        if (!hasAircraftBounds)
        {
            return targetWorld;
        }

        Bounds keepout = aircraftBounds;
        keepout.Expand(thirdPersonKeepoutPadding * 2f);

        if (!keepout.Contains(targetWorld))
        {
            return targetWorld;
        }

        return ProjectPointOutwardFromBoundsCenter(targetWorld, keepout);
    }

    /// <summary>
    /// 把进入飞机包围盒的第三人称相机点，沿“包围盒中心 -> 相机点”的方向推出去。
    ///
    /// 之前如果使用“最近盒面”推出，相机在机身附近时可能会被推到包围盒底面，
    /// 看起来就像第三视角突然跑到飞机下面。环绕相机更合理的做法是保留当前环绕方向，
    /// 只把距离拉到包围盒外侧。
    /// </summary>
    private Vector3 ProjectPointOutwardFromBoundsCenter(Vector3 point, Bounds bounds)
    {
        Vector3 center = bounds.center;
        Vector3 direction = point - center;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.back;
        }

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        float scale = float.PositiveInfinity;

        if (!Mathf.Approximately(direction.x, 0f))
        {
            float xScale = direction.x > 0f ? (max.x - center.x) / direction.x : (min.x - center.x) / direction.x;
            if (xScale > 0f) scale = Mathf.Min(scale, xScale);
        }

        if (!Mathf.Approximately(direction.y, 0f))
        {
            float yScale = direction.y > 0f ? (max.y - center.y) / direction.y : (min.y - center.y) / direction.y;
            if (yScale > 0f) scale = Mathf.Min(scale, yScale);
        }

        if (!Mathf.Approximately(direction.z, 0f))
        {
            float zScale = direction.z > 0f ? (max.z - center.z) / direction.z : (min.z - center.z) / direction.z;
            if (zScale > 0f) scale = Mathf.Min(scale, zScale);
        }

        if (float.IsInfinity(scale))
        {
            return point;
        }

        return center + direction * (scale + 0.02f);
    }

    private void BeginMouseControl()
    {
        isLooking = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private Vector2 ReadPovLookDirection()
    {
        if (!enablePovLook)
            return Vector2.zero;

        if (sidestickInput == null)
            ResolveSidestickInput();

        return sidestickInput != null && sidestickInput.ControlActive
            ? sidestickInput.PovLookDirection
            : Vector2.zero;
    }

    private void ResolveSidestickInput()
    {
        if (sidestickInput != null)
            return;

        Transform root = aircraftRoot != null ? aircraftRoot : transform.root;
        if (root != null)
            sidestickInput = root.GetComponentInChildren<ThrustmasterA320SidestickInput>(true);
    }

    private void EndMouseControl()
    {
        isLooking = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void SetActive(bool active)
    {
        if (cam == null)
        {
            cam = GetComponent<Camera>();
        }

        if (cam != null)
        {
            cam.enabled = active;
        }

        AudioListener audioListener = GetComponent<AudioListener>();
        if (audioListener != null)
        {
            audioListener.enabled = active;
        }

        enabled = active;

        if (active && cameraMode == CameraMode.ThirdPerson)
        {
            InitializeThirdPersonOrbit();
            ApplyThirdPersonOrbit();
        }

        if (!active && isLooking)
        {
            EndMouseControl();
        }
    }
}
