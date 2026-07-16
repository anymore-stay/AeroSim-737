using CesiumForUnity;
using UniStorm;
using UnityEngine;

[DefaultExecutionOrder(8990)]
public class B737WingtipVortexController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private JsbsimBridge bridge;
    [SerializeField] private FlightInput flightInput;
    [SerializeField] private Transform aircraft;
    [SerializeField] private Transform leftWingtip;
    [SerializeField] private Transform rightWingtip;
    [SerializeField] private ParticleSystem sourceParticle;
    [SerializeField] private ParticleSystem leftVortex;
    [SerializeField] private ParticleSystem rightVortex;
    [SerializeField] private UniStormSystem uniStormSystem;

    [Header("Realistic conditions")]
    [SerializeField, Min(0f)] private float minimumAglFt = 15f;
    [SerializeField, Min(0f)] private float minimumSpeedKts = 105f;
    [SerializeField, Min(0f)] private float maximumSpeedKts = 225f;
    [SerializeField] private float angleOfAttackOnsetDeg = 5.2f;
    [SerializeField, Min(0.1f)] private float angleOfAttackHysteresisDeg = 1.2f;
    [SerializeField, Range(0f, 1f)] private float minimumVisibleStrength = 0.12f;

    [Header("Particle tuning")]
    [SerializeField, Min(0.1f)] private float minimumLifetimeSeconds = 0.4f;
    [SerializeField, Min(0.1f)] private float maximumLifetimeSeconds = 0.7f;
    [SerializeField, Min(0.02f)] private float particleSizeMeters = 0.42f;
    [SerializeField, Min(0.05f)] private float particleSpacingMeters = 0.05f;
    [SerializeField, Min(1)] private int maximumParticles = 1800;
    [SerializeField, Min(0f)] private float wakeSinkMetersPerSecond = 0.22f;
    [SerializeField, Min(0f)] private float wakeInwardMetersPerSecond = 0.12f;
    [SerializeField, Min(0.01f)] private float fadeInSeconds = 0.2f;
    [SerializeField, Min(0.01f)] private float fadeOutSeconds = 0.4f;
    [SerializeField] private int sortingOrder = 21;

    [Header("Coordinate recentering")]
    [SerializeField, Min(0f)] private float minimumRecenteringDistanceMeters = 5f;
    [SerializeField, Min(10f)] private float transformJumpDistanceMeters = 300f;
    [SerializeField] private bool logStateChanges;

    private static readonly string[] WeightOnWheelsKeys =
    {
        "gear_wow",
        "gear_unit_wow",
        "gear_unit_1_wow",
        "gear_unit_2_wow"
    };

    private CesiumGeoreference georeference;
    private Vector3 leftLastEmissionPosition;
    private Vector3 rightLastEmissionPosition;
    private Vector3 lastAircraftPosition;
    private float visibleStrength;
    private float strengthVelocity;
    private bool liftDemandLatched;
    private bool emissionPositionsInitialized;
    private bool hasAircraftPosition;
    private bool wasVisible;
    private ParticleSystem.Particle[] particleBuffer;

    private void Awake()
    {
        ResolveReferences();
        PrepareVortexSystems();
        CaptureAircraftPosition();
    }

    private void OnEnable()
    {
        ResolveReferences();
        PrepareVortexSystems();
        SubscribeToGeoreference();
        CaptureAircraftPosition();
    }

    private void OnDisable()
    {
        UnsubscribeFromGeoreference();
        visibleStrength = 0f;
        strengthVelocity = 0f;
        liftDemandLatched = false;
        emissionPositionsInitialized = false;
        StopEmitting(leftVortex);
        StopEmitting(rightVortex);
    }

    private void LateUpdate()
    {
        ResolveReferences();
        SubscribeToGeoreference();
        DetectTransformRecentering();
        FollowWingtips();
        UpdateVisibleStrength();
        EmitVorticesByDistance();
        CaptureAircraftPosition();
    }

    private void ResolveReferences()
    {
        if (bridge == null) bridge = JsbsimBridge.Instance;
        if (aircraft == null && bridge != null) aircraft = bridge.Aircraft;
        if (aircraft == null) aircraft = transform;
        if (flightInput == null) flightInput = GetComponent<FlightInput>();
        if (uniStormSystem == null) uniStormSystem = FindFirstObjectByType<UniStormSystem>();
        if (leftWingtip == null) leftWingtip = FindDescendant("STROBE_LeftWing_Movable");
        if (rightWingtip == null) rightWingtip = FindDescendant("STROBE_RightWing_Movable");

        if (leftVortex == null) leftVortex = FindParticleSystem("B737WingtipVortex_Left");
        if (rightVortex == null) rightVortex = FindParticleSystem("B737WingtipVortex_Right");

        if (sourceParticle == null)
        {
            ParticleSystem[] candidates = GetComponentsInChildren<ParticleSystem>(true);
            for (int index = 0; index < candidates.Length; index++)
            {
                ParticleSystem candidate = candidates[index];
                if (candidate != null && candidate.name.Contains("B737RealisticContrail"))
                {
                    sourceParticle = candidate;
                    break;
                }
            }
        }
    }

    private ParticleSystem FindParticleSystem(string objectName)
    {
        ParticleSystem[] particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        for (int index = 0; index < particleSystems.Length; index++)
        {
            if (particleSystems[index].name == objectName)
                return particleSystems[index];
        }

        return null;
    }

    private Transform FindDescendant(string objectName)
    {
        Transform[] descendants = GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < descendants.Length; index++)
        {
            if (descendants[index].name == objectName)
                return descendants[index];
        }

        return null;
    }

    private void PrepareVortexSystems()
    {
        PrepareVortexSystem(leftVortex);
        PrepareVortexSystem(rightVortex);
    }

    private void PrepareVortexSystem(ParticleSystem particleSystem)
    {
        if (particleSystem == null) return;

        ConfigureParticleSystem(particleSystem);
        particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void ConfigureParticleSystem(ParticleSystem particleSystem)
    {
        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(minimumLifetimeSeconds, maximumLifetimeSeconds);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(particleSizeMeters * 0.75f, particleSizeMeters);
        main.startColor = Color.white;
        main.gravityModifier = 0f;
        main.maxParticles = maximumParticles;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = false;

        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = sortingOrder;
            renderer.sortingFudge = 5f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private void FollowWingtips()
    {
        FollowWingtip(leftVortex, leftWingtip);
        FollowWingtip(rightVortex, rightWingtip);
    }

    private static void FollowWingtip(ParticleSystem particleSystem, Transform wingtip)
    {
        if (particleSystem == null || wingtip == null) return;
        particleSystem.transform.SetPositionAndRotation(wingtip.position, wingtip.rotation);
    }

    private void UpdateVisibleStrength()
    {
        float flaps = flightInput != null ? flightInput.Flaps : 0f;
        float angleOfAttack = bridge != null ? bridge.AngleOfAttackDeg : 0f;
        liftDemandLatched = B737WingtipVortexMath.UpdateLiftDemandLatch(
            liftDemandLatched,
            angleOfAttack,
            flaps,
            angleOfAttackOnsetDeg,
            angleOfAttackHysteresisDeg);

        bool airborne = IsAirborne();
        float moisture = GetMoistureFactor();
        float targetStrength = bridge != null && bridge.HasState
            ? B737WingtipVortexMath.CalculateVisibleStrength(
                airborne,
                bridge.SpeedKts,
                angleOfAttack,
                flaps,
                liftDemandLatched,
                moisture,
                minimumSpeedKts,
                maximumSpeedKts,
                angleOfAttackOnsetDeg)
            : 0f;

        if (targetStrength < minimumVisibleStrength)
            targetStrength = 0f;

        float smoothTime = targetStrength > visibleStrength ? fadeInSeconds : fadeOutSeconds;
        visibleStrength = Mathf.SmoothDamp(
            visibleStrength,
            targetStrength,
            ref strengthVelocity,
            Mathf.Max(0.01f, smoothTime),
            Mathf.Infinity,
            Time.deltaTime);

        bool isVisible = visibleStrength >= 0.02f;
        if (isVisible != wasVisible)
        {
            wasVisible = isVisible;
            emissionPositionsInitialized = false;
            if (logStateChanges)
            {
                Debug.Log(
                    "[WingtipVortex] " + (isVisible ? "visible" : "hidden") +
                    " | AoA " + angleOfAttack.ToString("F1") +
                    " | speed " + (bridge != null ? bridge.SpeedKts.ToString("F0") : "0") +
                    " | moisture " + moisture.ToString("F2"),
                    this);
            }
        }

        if (!isVisible)
        {
            emissionPositionsInitialized = false;
            StopEmitting(leftVortex);
            StopEmitting(rightVortex);
        }
    }

    private bool IsAirborne()
    {
        if (bridge == null || !bridge.HasState || bridge.AglFt < minimumAglFt)
            return false;

        for (int index = 0; index < WeightOnWheelsKeys.Length; index++)
        {
            if (bridge.TryGetValue(WeightOnWheelsKeys[index], out float weightOnWheels) && weightOnWheels > 0.5f)
                return false;
        }

        return true;
    }

    private float GetMoistureFactor()
    {
        WeatherType weather = uniStormSystem != null ? uniStormSystem.CurrentWeatherType : null;
        if (weather == null) return 0f;

        bool precipitation = weather.PrecipitationWeatherType == WeatherType.Yes_No.Yes;
        return B737WingtipVortexMath.CalculateMoistureFactor(precipitation, (int)weather.CloudLevel);
    }

    private void EmitVorticesByDistance()
    {
        if (visibleStrength < 0.02f || leftVortex == null || rightVortex == null)
            return;

        EnsurePlaying(leftVortex);
        EnsurePlaying(rightVortex);

        if (!emissionPositionsInitialized)
        {
            leftLastEmissionPosition = leftVortex.transform.position;
            rightLastEmissionPosition = rightVortex.transform.position;
            emissionPositionsInitialized = true;
            return;
        }

        float spacing = Mathf.Lerp(particleSpacingMeters * 1.35f, particleSpacingMeters, visibleStrength);
        EmitAlongSegment(leftVortex, leftWingtip, ref leftLastEmissionPosition, spacing);
        EmitAlongSegment(rightVortex, rightWingtip, ref rightLastEmissionPosition, spacing);
    }

    private void EmitAlongSegment(
        ParticleSystem particleSystem,
        Transform wingtip,
        ref Vector3 previousPosition,
        float spacing)
    {
        Vector3 currentPosition = particleSystem.transform.position;
        Vector3 segment = currentPosition - previousPosition;
        float distance = segment.magnitude;
        if (distance < spacing) return;

        if (distance > transformJumpDistanceMeters)
        {
            previousPosition = currentPosition;
            return;
        }

        Vector3 direction = segment / distance;
        int emitCount = Mathf.Min(Mathf.FloorToInt(distance / spacing), 128);
        Vector3 inward = GetInwardDirection(wingtip);
        Vector3 wakeVelocity = Vector3.down * wakeSinkMetersPerSecond + inward * wakeInwardMetersPerSecond;
        float alpha = Mathf.Lerp(0.16f, 0.52f, visibleStrength);
        float size = particleSizeMeters * Mathf.Lerp(0.72f, 1f, visibleStrength);

        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
        {
            applyShapeToPosition = false,
            startColor = new Color(1f, 1f, 1f, alpha),
            startSize = size
        };

        for (int particleIndex = 1; particleIndex <= emitCount; particleIndex++)
        {
            emitParams.position = previousPosition + direction * spacing * particleIndex;
            emitParams.velocity = wakeVelocity + Random.insideUnitSphere * 0.035f;
            emitParams.startLifetime = Random.Range(minimumLifetimeSeconds, maximumLifetimeSeconds);
            particleSystem.Emit(emitParams, 1);
        }

        previousPosition += direction * spacing * emitCount;
    }

    private Vector3 GetInwardDirection(Transform wingtip)
    {
        if (aircraft == null || wingtip == null) return Vector3.zero;

        Vector3 inward = Vector3.ProjectOnPlane(aircraft.position - wingtip.position, Vector3.up);
        return inward.sqrMagnitude > 0.0001f ? inward.normalized : Vector3.zero;
    }

    private static void EnsurePlaying(ParticleSystem particleSystem)
    {
        if (particleSystem != null && !particleSystem.isPlaying)
            particleSystem.Play(false);
    }

    private static void StopEmitting(ParticleSystem particleSystem)
    {
        if (particleSystem != null && particleSystem.isPlaying)
            particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    private void CaptureAircraftPosition()
    {
        if (aircraft == null) return;
        lastAircraftPosition = aircraft.position;
        hasAircraftPosition = true;
    }

    private void SubscribeToGeoreference()
    {
        if (georeference != null || aircraft == null) return;

        georeference = aircraft.GetComponentInParent<CesiumGeoreference>();
        if (georeference != null)
            georeference.changed += HandleGeoreferenceChanged;
    }

    private void UnsubscribeFromGeoreference()
    {
        if (georeference == null) return;
        georeference.changed -= HandleGeoreferenceChanged;
        georeference = null;
    }

    private void HandleGeoreferenceChanged()
    {
        if (aircraft == null) return;

        Vector3 currentPosition = aircraft.position;
        if (!hasAircraftPosition)
        {
            lastAircraftPosition = currentPosition;
            hasAircraftPosition = true;
            return;
        }

        Vector3 displacement = currentPosition - lastAircraftPosition;
        lastAircraftPosition = currentPosition;
        float threshold = Mathf.Max(0f, minimumRecenteringDistanceMeters);
        if (displacement.sqrMagnitude < threshold * threshold) return;

        ApplyRecenteringOffset(displacement, "Cesium");
    }

    private void DetectTransformRecentering()
    {
        if (aircraft == null || !hasAircraftPosition) return;

        Vector3 displacement = aircraft.position - lastAircraftPosition;
        float threshold = Mathf.Max(10f, transformJumpDistanceMeters);
        if (displacement.sqrMagnitude < threshold * threshold) return;

        lastAircraftPosition = aircraft.position;
        ApplyRecenteringOffset(displacement, "Transform");
    }

    private void ApplyRecenteringOffset(Vector3 displacement, string source)
    {
        ShiftParticles(leftVortex, displacement);
        ShiftParticles(rightVortex, displacement);
        if (emissionPositionsInitialized)
        {
            leftLastEmissionPosition += displacement;
            rightLastEmissionPosition += displacement;
        }

        if (logStateChanges)
            Debug.Log("[WingtipVortex] Compensated " + source + " recenter by " + displacement.ToString("F2"), this);
    }

    private void ShiftParticles(ParticleSystem particleSystem, Vector3 displacement)
    {
        if (particleSystem == null) return;

        int maxParticles = particleSystem.main.maxParticles;
        if (particleBuffer == null || particleBuffer.Length < maxParticles)
            particleBuffer = new ParticleSystem.Particle[maxParticles];

        int particleCount = particleSystem.GetParticles(particleBuffer);
        for (int index = 0; index < particleCount; index++)
            particleBuffer[index].position += displacement;

        if (particleCount > 0)
            particleSystem.SetParticles(particleBuffer, particleCount);
    }

}

public static class B737WingtipVortexMath
{
    public static bool UpdateLiftDemandLatch(
        bool currentlyLatched,
        float angleOfAttackDeg,
        float flaps,
        float cleanOnsetDeg,
        float hysteresisDeg)
    {
        float flapAmount = Mathf.Clamp01(flaps);
        float onset = cleanOnsetDeg - 1.6f * flapAmount;
        float release = onset - Mathf.Max(0.1f, hysteresisDeg);
        return angleOfAttackDeg >= (currentlyLatched ? release : onset);
    }

    public static float CalculateMoistureFactor(bool precipitation, int cloudLevel)
    {
        if (precipitation) return 1f;

        switch (cloudLevel)
        {
            case 0: return 0f;
            case 1: return 0.08f;
            case 2: return 0.55f;
            case 3: return 0.82f;
            case 4: return 1f;
            case 5: return 0.45f;
            default: return 0f;
        }
    }

    public static float CalculateVisibleStrength(
        bool airborne,
        float speedKts,
        float angleOfAttackDeg,
        float flaps,
        bool liftDemandLatched,
        float moisture,
        float minimumSpeedKts,
        float maximumSpeedKts,
        float cleanAngleOfAttackOnsetDeg)
    {
        if (!airborne || !liftDemandLatched || moisture <= 0f || maximumSpeedKts <= minimumSpeedKts)
            return 0f;

        float lowerRampEnd = Mathf.Min(minimumSpeedKts + 20f, maximumSpeedKts);
        float upperRampStart = Mathf.Max(maximumSpeedKts - 25f, minimumSpeedKts);
        float lowSpeedFactor = Smooth01(Mathf.InverseLerp(minimumSpeedKts, lowerRampEnd, speedKts));
        float highSpeedFactor = 1f - Smooth01(Mathf.InverseLerp(upperRampStart, maximumSpeedKts, speedKts));
        float speedFactor = Mathf.Clamp01(lowSpeedFactor * highSpeedFactor);

        float flapAmount = Mathf.Clamp01(flaps);
        float onset = cleanAngleOfAttackOnsetDeg - 1.6f * flapAmount;
        float angleFactor = Mathf.InverseLerp(onset - 0.4f, onset + 4f, angleOfAttackDeg);
        float flapFactor = Smooth01(Mathf.InverseLerp(0.15f, 0.65f, flapAmount));
        float liftStrength = 0.35f + 0.65f * Mathf.Max(angleFactor, flapFactor * 0.65f);

        return Mathf.Clamp01(Mathf.Clamp01(moisture) * speedFactor * liftStrength);
    }

    private static float Smooth01(float value)
    {
        float clamped = Mathf.Clamp01(value);
        return clamped * clamped * (3f - 2f * clamped);
    }
}
