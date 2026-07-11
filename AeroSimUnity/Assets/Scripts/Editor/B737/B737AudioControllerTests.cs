using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;
using UnityEngine;

public class B737AudioControllerTests
{
    private static readonly OpCode[] OneByteOpCodes = new OpCode[256];
    private static readonly OpCode[] TwoByteOpCodes = new OpCode[256];

    private static readonly string[] SourceNames =
    {
        "Left Engine Loop",
        "Left Engine Starter",
        "Right Engine Loop",
        "Right Engine Starter",
        "Gear",
        "Flaps",
        "Runway Roll",
        "Touchdown",
        "Stall",
        "Overspeed",
        "Radio Altitude"
    };

    private static readonly string[] RequiredResourcePaths =
    {
        "Audio/B737/Engine/engine_loop",
        "Audio/B737/Engine/starter",
        "Audio/B737/Systems/gear",
        "Audio/B737/Systems/flap",
        "Audio/B737/Ground/runway_roll",
        "Audio/B737/Ground/touchdown_1",
        "Audio/B737/Ground/touchdown_2",
        "Audio/B737/Ground/touchdown_3",
        "Audio/B737/Ground/touchdown_4",
        "Audio/B737/Alerts/stall",
        "Audio/B737/Alerts/overspeed",
        "Audio/B737/Callouts/1000",
        "Audio/B737/Callouts/500",
        "Audio/B737/Callouts/400",
        "Audio/B737/Callouts/300",
        "Audio/B737/Callouts/200",
        "Audio/B737/Callouts/100",
        "Audio/B737/Callouts/50",
        "Audio/B737/Callouts/40",
        "Audio/B737/Callouts/30",
        "Audio/B737/Callouts/20",
        "Audio/B737/Callouts/10"
    };

    private readonly List<GameObject> objectsToDestroy = new List<GameObject>();

    static B737AudioControllerTests()
    {
        FieldInfo[] fields = typeof(OpCodes).GetFields(
            BindingFlags.Public | BindingFlags.Static);
        for (int index = 0; index < fields.Length; index++)
        {
            OpCode code = (OpCode)fields[index].GetValue(null);
            ushort value = unchecked((ushort)code.Value);
            if (value < 256)
            {
                OneByteOpCodes[value] = code;
            }
            else if ((value & 0xff00) == 0xfe00)
            {
                TwoByteOpCodes[value & 0xff] = code;
            }
        }
    }

    [TearDown]
    public void TearDown()
    {
        for (int index = objectsToDestroy.Count - 1; index >= 0; index--)
        {
            if (objectsToDestroy[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(objectsToDestroy[index]);
            }
        }

        objectsToDestroy.Clear();
    }

    [Test]
    public void RequiredAudioResourcesExist()
    {
        for (int index = 0; index < RequiredResourcePaths.Length; index++)
        {
            Assert.That(
                Resources.Load<AudioClip>(RequiredResourcePaths[index]),
                Is.Not.Null,
                RequiredResourcePaths[index]);
        }
    }

    [Test]
    public void LoopedAudioResourcesHaveQuietBoundaries()
    {
        string[] loopedPaths =
        {
            "Audio/B737/Engine/engine_loop",
            "Audio/B737/Ground/runway_roll",
            "Audio/B737/Systems/gear",
            "Audio/B737/Systems/flap",
            "Audio/B737/Alerts/overspeed"
        };

        for (int index = 0; index < loopedPaths.Length; index++)
        {
            Assert.That(
                ReadLoopBoundaryJump(loopedPaths[index]),
                Is.LessThan(0.02f),
                loopedPaths[index]);
        }
    }

    [Test]
    public void ControllerExposesTheRequiredPublicApiAndLoadsEveryClip()
    {
        Type type = ControllerType();
        Assert.That(Attribute.IsDefined(type, typeof(DisallowMultipleComponent)), Is.True);

        string[] methodNames =
        {
            "InitializeAudio",
            "SetOfflineEngineN1",
            "ClearOfflineEngineOverride",
            "PreviewEngineStart",
            "PreviewGear",
            "PreviewFlaps",
            "PreviewRunwayRoll",
            "PreviewTouchdown",
            "PreviewStall",
            "PreviewOverspeed",
            "PreviewCallout",
            "StopAllPreviewAudio"
        };

        for (int index = 0; index < methodNames.Length; index++)
        {
            Assert.That(
                type.GetMethod(methodNames[index], BindingFlags.Public | BindingFlags.Instance),
                Is.Not.Null,
                methodNames[index]);
        }

        Component controller = CreateController();
        Assert.That(ReadBoolProperty(controller, "IsInitialized"), Is.True);
        Assert.That(ReadBoolProperty(controller, "HasAllRequiredClips"), Is.True);
    }

    [Test]
    public void InitializeCreatesExactlyElevenStableSourcesAndIsIdempotent()
    {
        Component controller = CreateController();

        AssertNamedSources(controller.gameObject);
        Invoke(controller, "InitializeAudio");
        Invoke(controller, "InitializeAudio");
        AssertNamedSources(controller.gameObject);
    }

    [Test]
    public void SourcesHaveTheRequiredSpatialLoopAndAwakeConfiguration()
    {
        Component controller = CreateController();
        HashSet<string> looping = new HashSet<string>
        {
            "Left Engine Loop",
            "Right Engine Loop",
            "Gear",
            "Flaps",
            "Runway Roll",
            "Stall",
            "Overspeed"
        };

        for (int index = 0; index < SourceNames.Length; index++)
        {
            AudioSource source = FindSource(controller.gameObject, SourceNames[index]);
            Assert.That(source.playOnAwake, Is.False, SourceNames[index]);
            Assert.That(source.loop, Is.EqualTo(looping.Contains(SourceNames[index])), SourceNames[index]);
            Assert.That(
                source.spatialBlend,
                Is.EqualTo(index < 8 ? 1f : 0f).Within(0.0001f),
                SourceNames[index]);
            Assert.That(source.dopplerLevel, Is.Zero.Within(0.0001f), SourceNames[index]);
            Assert.That(source.clip, Is.Not.Null, SourceNames[index]);
        }
    }

    [Test]
    public void RuntimeAudioObjectsAreConfiguredToHideFromPlayHierarchy()
    {
        FieldInfo field = ControllerType().GetField(
            "RuntimeAudioObjectHideFlags",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(field, Is.Not.Null);

        HideFlags flags = (HideFlags)field.GetValue(null);
        Assert.That((flags & HideFlags.HideInHierarchy), Is.Not.EqualTo(0));
        Assert.That((flags & HideFlags.DontSaveInEditor), Is.Not.EqualTo(0));
        Assert.That((flags & HideFlags.DontSaveInBuild), Is.Not.EqualTo(0));
    }

    [Test]
    public void PrimaryMixKeepsGearClearAboveEnginesAndRunway()
    {
        Component controller = CreateController();
        AudioSource leftEngine = FindSource(controller.gameObject, "Left Engine Loop");
        AudioSource gear = FindSource(controller.gameObject, "Gear");
        AudioSource runway = FindSource(controller.gameObject, "Runway Roll");

        Invoke(controller, "SetOfflineEngineN1", 100f, 100f);
        Invoke(controller, "PreviewGear", true);
        Invoke(controller, "PreviewRunwayRoll", 1f);

        Assert.That(leftEngine.volume, Is.LessThanOrEqualTo(0.45f));
        Assert.That(runway.volume, Is.LessThanOrEqualTo(0.35f));
        Assert.That(gear.volume, Is.GreaterThan(leftEngine.volume));
        Assert.That(gear.volume, Is.GreaterThan(runway.volume));
    }

    [Test]
    public void OfflineEngineN1ControlsLeftAndRightIndependentlyAndRemainsFinite()
    {
        Component controller = CreateController();
        AudioSource left = FindSource(controller.gameObject, "Left Engine Loop");
        AudioSource right = FindSource(controller.gameObject, "Right Engine Loop");

        Invoke(controller, "SetOfflineEngineN1", 30f, 80f);
        float originalRightVolume = right.volume;
        float originalRightPitch = right.pitch;
        Assert.That(left.volume, Is.LessThan(right.volume));
        Assert.That(left.pitch, Is.LessThan(right.pitch));

        Invoke(controller, "SetOfflineEngineN1", 70f, 80f);
        Assert.That(left.volume, Is.GreaterThan(0f));
        Assert.That(right.volume, Is.EqualTo(originalRightVolume).Within(0.0001f));
        Assert.That(right.pitch, Is.EqualTo(originalRightPitch).Within(0.0001f));

        Invoke(controller, "SetOfflineEngineN1", float.NaN, float.PositiveInfinity);
        AssertFinite(left.volume, left.pitch, right.volume, right.pitch);
    }

    [Test]
    public void OfflineAudioSilencesFlightInputThrottleWithoutExplicitOverride()
    {
        Component flightInput;
        Component controller = CreateControllerWithDependency("FlightInput", out flightInput);
        SetPrivateField(flightInput, "throttle", 0.75f);

        InvokePrivate(controller, "UpdateOfflineAudio", 0.02f);

        AudioSource left = FindSource(controller.gameObject, "Left Engine Loop");
        AudioSource right = FindSource(controller.gameObject, "Right Engine Loop");
        Assert.That(left.volume, Is.Zero.Within(0.0001f));
        Assert.That(right.volume, Is.Zero.Within(0.0001f));
    }

    [Test]
    public void ClearOfflineEngineOverrideReturnsToSilenceUntilFreshBridgeState()
    {
        Component flightInput;
        Component controller = CreateControllerWithDependency("FlightInput", out flightInput);
        SetPrivateField(flightInput, "throttle", 0.6f);

        Invoke(controller, "SetOfflineEngineN1", 30f, 90f);
        Invoke(controller, "ClearOfflineEngineOverride");

        AudioSource left = FindSource(controller.gameObject, "Left Engine Loop");
        AudioSource right = FindSource(controller.gameObject, "Right Engine Loop");
        Assert.That(left.volume, Is.Zero.Within(0.0001f));
        Assert.That(right.volume, Is.Zero.Within(0.0001f));
    }

    [TestCase("OnDisable")]
    [TestCase("OnDestroy")]
    public void LifecycleShutdownSilencesAllSourcesWithoutClearingPreviewState(
        string lifecycleMethod)
    {
        Component controller = CreateController();
        Invoke(controller, "SetOfflineEngineN1", 80f, 80f);
        Invoke(controller, "PreviewEngineStart", 0);
        Invoke(controller, "PreviewEngineStart", 1);
        Invoke(controller, "PreviewGear", true);
        Invoke(controller, "PreviewFlaps", true);
        Invoke(controller, "PreviewRunwayRoll", 0.75f);
        Invoke(controller, "PreviewTouchdown", 4);
        Invoke(controller, "PreviewStall", true);
        Invoke(controller, "PreviewOverspeed", true);
        Invoke(controller, "PreviewCallout", 500);

        InvokePrivate(controller, lifecycleMethod);

        AudioSource[] sources = controller.GetComponentsInChildren<AudioSource>(true);
        Assert.That(sources, Has.Length.EqualTo(SourceNames.Length));
        for (int index = 0; index < sources.Length; index++)
        {
            Assert.That(sources[index].volume, Is.Zero.Within(0.0001f), sources[index].name);
        }

        if (lifecycleMethod == "OnDisable")
        {
            InvokePrivate(controller, "UpdateOfflineAudio", 0.02f);
            Assert.That(FindSource(controller.gameObject, "Left Engine Loop").volume, Is.GreaterThan(0f));
            Assert.That(FindSource(controller.gameObject, "Gear").volume, Is.GreaterThan(0f));
            Assert.That(FindSource(controller.gameObject, "Flaps").volume, Is.GreaterThan(0f));
            Assert.That(FindSource(controller.gameObject, "Runway Roll").volume, Is.GreaterThan(0f));
            Assert.That(FindSource(controller.gameObject, "Stall").volume, Is.GreaterThan(0f));
            Assert.That(FindSource(controller.gameObject, "Overspeed").volume, Is.GreaterThan(0f));
        }
    }

    [TestCase("OnDisable")]
    [TestCase("OnDestroy")]
    public void LifecycleShutdownClearsPendingCallouts(string lifecycleMethod)
    {
        Component controller = CreateController();
        Queue<int> pendingCallouts = (Queue<int>)GetPrivateField(
            controller,
            "pendingCallouts");
        pendingCallouts.Enqueue(500);

        InvokePrivate(controller, lifecycleMethod);

        Assert.That(pendingCallouts, Is.Empty);
    }

    [Test]
    public void StaleBridgeStateSilencesEnginesUntilANewStateEventArrives()
    {
        Component flightInput;
        Component controller = CreateControllerWithDependency("FlightInput", out flightInput);
        Component bridge = flightInput.GetComponent(RuntimeType("JsbsimBridge"));
        Assert.That(bridge, Is.Not.Null);
        SetPrivateField(flightInput, "throttle", 0.3f);
        SetPrivateField(bridge, "<HasState>k__BackingField", true);

        Dictionary<string, float> latest = GetBridgeLatest(bridge);
        latest.Clear();
        latest["propulsion_engine_n1"] = 90f;
        latest["propulsion_engine_1_n1"] = 90f;
        latest["propulsion_engine_n2"] = 60f;
        latest["propulsion_engine_1_n2"] = 60f;

        InvokePrivate(controller, "OnEnable");
        InvokePrivate(controller, "OnEnable");
        Assert.That(CountBridgeSubscriptionsFor(bridge, controller), Is.EqualTo(1));
        SetPrivateField(controller, "hasReceivedBridgeState", true);
        SetPrivateField(
            controller,
            "lastBridgeStateRealtime",
            Time.realtimeSinceStartup - 2f);
        SetPrivateField(controller, "stateFreshnessTimeoutSeconds", 1f);

        InvokePrivate(controller, "Update");
        AudioSource left = FindSource(controller.gameObject, "Left Engine Loop");
        AudioSource right = FindSource(controller.gameObject, "Right Engine Loop");
        float staleVolume = left.volume;
        Assert.That(staleVolume, Is.Zero.Within(0.0001f));
        Assert.That(right.volume, Is.Zero.Within(0.0001f));

        RaiseBridgeStateUpdated(bridge);
        InvokePrivate(controller, "Update");

        Assert.That(left.volume, Is.GreaterThan(0f));
        Assert.That(right.volume, Is.EqualTo(left.volume).Within(0.0001f));
    }

    [Test]
    public void ReconnectFirstPacketRebaselinesEdgesBeforeLaterEventsCanPlay()
    {
        Component flightInput;
        Component controller = CreateControllerWithDependency("FlightInput", out flightInput);
        Component bridge = flightInput.GetComponent(RuntimeType("JsbsimBridge"));
        Assert.That(bridge, Is.Not.Null);
        SetPrivateField(flightInput, "throttle", 0.2f);
        SetPrivateField(bridge, "<HasState>k__BackingField", true);
        SetPrivateField(bridge, "<AglFt>k__BackingField", 1200f);
        SetPrivateField(bridge, "<VerticalSpeedFps>k__BackingField", 0f);
        SetPrivateField(controller, "stateFreshnessTimeoutSeconds", 1f);

        Dictionary<string, float> latest = GetBridgeLatest(bridge);
        latest.Clear();
        latest["propulsion_engine_n1"] = 30f;
        latest["propulsion_engine_1_n1"] = 30f;
        latest["propulsion_engine_n2"] = 0f;
        latest["propulsion_engine_1_n2"] = 0f;
        latest["propulsion_starter_cmd"] = 0f;
        latest["propulsion_active_engine"] = -1f;
        latest["gear_gear_pos_norm"] = 0f;
        latest["fcs_flap_pos_norm"] = 0f;
        latest["gear_unit_wow"] = 0f;
        latest["gear_unit_1_wow"] = 0f;
        latest["gear_unit_2_wow"] = 0f;
        latest["gear_wow"] = 0f;
        latest["gear_unit_compression_velocity_fps"] = 0f;
        latest["gear_unit_1_compression_velocity_fps"] = 0f;
        latest["gear_unit_2_compression_velocity_fps"] = 0f;

        RaiseBridgeStateUpdated(bridge);
        InvokePrivate(controller, "Update");

        SetPrivateField(
            controller,
            "lastBridgeStateRealtime",
            Time.realtimeSinceStartup - 2f);
        InvokePrivate(controller, "Update");

        SetPrivateField(bridge, "<AglFt>k__BackingField", 0f);
        latest["propulsion_engine_n1"] = 70f;
        latest["propulsion_engine_1_n1"] = 70f;
        latest["propulsion_engine_n2"] = 6f;
        latest["propulsion_engine_1_n2"] = 6f;
        latest["propulsion_starter_cmd"] = 1f;
        latest["gear_gear_pos_norm"] = 1f;
        latest["fcs_flap_pos_norm"] = 1f;
        latest["gear_unit_wow"] = 1f;
        latest["gear_unit_1_wow"] = 1f;
        latest["gear_unit_2_wow"] = 1f;
        latest["gear_wow"] = 1f;
        latest["gear_unit_compression_velocity_fps"] = 10f;
        latest["gear_unit_1_compression_velocity_fps"] = 10f;
        latest["gear_unit_2_compression_velocity_fps"] = 10f;

        AudioSource leftStarter = FindSource(controller.gameObject, "Left Engine Starter");
        AudioSource rightStarter = FindSource(controller.gameObject, "Right Engine Starter");
        AudioSource touchdown = FindSource(controller.gameObject, "Touchdown");
        AudioSource gear = FindSource(controller.gameObject, "Gear");
        AudioSource flaps = FindSource(controller.gameObject, "Flaps");
        AudioSource radio = FindSource(controller.gameObject, "Radio Altitude");
        Queue<int> pendingCallouts = (Queue<int>)GetPrivateField(
            controller,
            "pendingCallouts");
        leftStarter.volume = 0f;
        rightStarter.volume = 0f;
        touchdown.volume = 0f;
        gear.volume = 0f;
        flaps.volume = 0f;
        radio.volume = 0f;
        pendingCallouts.Clear();

        RaiseBridgeStateUpdated(bridge);
        InvokePrivate(controller, "Update");

        Assert.That(leftStarter.volume, Is.Zero.Within(0.0001f));
        Assert.That(rightStarter.volume, Is.Zero.Within(0.0001f));
        Assert.That(touchdown.volume, Is.Zero.Within(0.0001f));
        Assert.That(gear.volume, Is.Zero.Within(0.0001f));
        Assert.That(flaps.volume, Is.Zero.Within(0.0001f));
        Assert.That(radio.volume, Is.Zero.Within(0.0001f));
        Assert.That(pendingCallouts, Is.Empty);

        SetPrivateField(bridge, "<AglFt>k__BackingField", 1200f);
        latest["propulsion_engine_n2"] = 0f;
        latest["propulsion_engine_1_n2"] = 0f;
        latest["propulsion_starter_cmd"] = 0f;
        latest["gear_unit_wow"] = 0f;
        latest["gear_unit_1_wow"] = 0f;
        latest["gear_unit_2_wow"] = 0f;
        latest["gear_wow"] = 0f;
        latest["gear_unit_compression_velocity_fps"] = 0f;
        latest["gear_unit_1_compression_velocity_fps"] = 0f;
        latest["gear_unit_2_compression_velocity_fps"] = 0f;
        RaiseBridgeStateUpdated(bridge);
        InvokePrivate(controller, "Update");

        leftStarter.volume = 0f;
        rightStarter.volume = 0f;
        touchdown.volume = 0f;
        radio.volume = 0f;
        pendingCallouts.Clear();
        SetPrivateField(bridge, "<AglFt>k__BackingField", 0f);
        latest["propulsion_engine_n2"] = 6f;
        latest["propulsion_engine_1_n2"] = 6f;
        latest["propulsion_starter_cmd"] = 1f;
        latest["gear_unit_wow"] = 1f;
        latest["gear_unit_1_wow"] = 1f;
        latest["gear_unit_2_wow"] = 1f;
        latest["gear_wow"] = 1f;
        latest["gear_unit_compression_velocity_fps"] = 10f;
        latest["gear_unit_1_compression_velocity_fps"] = 10f;
        latest["gear_unit_2_compression_velocity_fps"] = 10f;
        RaiseBridgeStateUpdated(bridge);
        InvokePrivate(controller, "Update");

        Assert.That(leftStarter.volume, Is.GreaterThan(0f));
        Assert.That(rightStarter.volume, Is.GreaterThan(0f));
        Assert.That(touchdown.volume, Is.GreaterThan(0f));
        Assert.That(pendingCallouts, Is.Not.Empty);
    }

    [Test]
    public void ConnectedGroundUpdateDoesNotAllocateAfterWarmup()
    {
        Component bridge;
        Component controller = CreateControllerWithDependency("JsbsimBridge", out bridge);
        Dictionary<string, float> latest = GetBridgeLatest(bridge);
        latest.Clear();
        latest["gear_unit_wow"] = 0f;
        latest["gear_unit_1_wow"] = 0f;
        latest["gear_unit_2_wow"] = 0f;
        latest["gear_wow"] = 0f;
        latest["gear_unit_wheel_speed_fps"] = 0f;
        latest["gear_unit_1_wheel_speed_fps"] = 0f;
        latest["gear_unit_2_wheel_speed_fps"] = 0f;
        latest["gear_unit_compression_velocity_fps"] = 0f;
        latest["gear_unit_1_compression_velocity_fps"] = 0f;
        latest["gear_unit_2_compression_velocity_fps"] = 0f;

        MethodInfo method = controller.GetType().GetMethod(
            "UpdateConnectedGroundSounds",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null);
        Action<float> updateGround = (Action<float>)Delegate.CreateDelegate(
            typeof(Action<float>),
            controller,
            method);

        for (int index = 0; index < 10; index++)
        {
            updateGround(1f / 60f);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 120; index++)
        {
            updateGround(1f / 60f);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.That(
            allocated,
            Is.LessThanOrEqualTo(128L),
            "Ground audio allocated " + allocated + " bytes after warmup.");
    }

    [Test]
    public void GroundAudioHotPathContainsNoPerCallManagedAllocationSites()
    {
        Type type = ControllerType();
        string[] methodNames =
        {
            "UpdateConnectedGroundSounds",
            "ReadMaximumWheelSpeed",
            "ReadCompressionVelocity"
        };

        for (int index = 0; index < methodNames.Length; index++)
        {
            MethodInfo method = type.GetMethod(
                methodNames[index],
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, methodNames[index]);
            Assert.That(
                HasPerCallManagedAllocationSite(method),
                Is.False,
                methodNames[index] + " still contains newarr, newobj, or string.Concat.");
        }
    }

    [Test]
    public void PartialIndividualWowStillUsesAggregateTouchdownEdge()
    {
        Component bridge;
        Component controller = CreateControllerWithDependency("JsbsimBridge", out bridge);
        Dictionary<string, float> latest = GetBridgeLatest(bridge);
        latest.Clear();
        latest["gear_unit_wow"] = 0f;
        latest["gear_wow"] = 0f;
        latest["gear_unit_1_compression_velocity_fps"] = 10f;
        InvokePrivate(controller, "UpdateConnectedGroundSounds", 0.02f);

        AudioSource touchdown = FindSource(controller.gameObject, "Touchdown");
        touchdown.volume = 0f;
        latest["gear_wow"] = 1f;
        InvokePrivate(controller, "UpdateConnectedGroundSounds", 0.02f);

        Assert.That(touchdown.volume, Is.GreaterThan(0f));
        Assert.That(
            touchdown.clip,
            Is.SameAs(Resources.Load<AudioClip>("Audio/B737/Ground/touchdown_4")));
    }

    [Test]
    public void GearMovementKeepsPlayingLongEnoughAfterInstantPositionStep()
    {
        Component bridge;
        Component controller = CreateControllerWithDependency("JsbsimBridge", out bridge);
        Dictionary<string, float> latest = GetBridgeLatest(bridge);
        latest.Clear();
        latest["gear_gear_pos_norm"] = 1f;
        InvokePrivate(controller, "UpdateConnectedMechanicalSounds", 0.02f);

        latest["gear_gear_pos_norm"] = 0f;
        InvokePrivate(controller, "UpdateConnectedMechanicalSounds", 0.02f);

        AudioSource gear = FindSource(controller.gameObject, "Gear");
        Assert.That(gear.volume, Is.GreaterThan(0f));

        InvokePrivate(controller, "UpdateConnectedMechanicalSounds", 1f);
        Assert.That(gear.volume, Is.GreaterThan(0f));

        InvokePrivate(controller, "UpdateConnectedMechanicalSounds", 4f);
        Assert.That(gear.volume, Is.Zero.Within(0.0001f));
    }

    [Test]
    public void UnrelatedDescendantWithReservedNameIsNotClaimedAsAControlledSource()
    {
        GameObject root = new GameObject("B737 Audio Name Collision Test Root");
        objectsToDestroy.Add(root);
        GameObject unrelatedParent = new GameObject("Unrelated Audio");
        unrelatedParent.transform.SetParent(root.transform, false);
        GameObject collision = new GameObject("Gear");
        collision.transform.SetParent(unrelatedParent.transform, false);
        AudioSource unrelatedSource = collision.AddComponent<AudioSource>();
        AudioClip unrelatedClip = Resources.Load<AudioClip>("Audio/B737/Engine/starter");
        unrelatedSource.clip = unrelatedClip;
        unrelatedSource.loop = false;
        unrelatedSource.playOnAwake = true;
        unrelatedSource.spatialBlend = 0.25f;

        Component controller = root.AddComponent(ControllerType());
        Invoke(controller, "InitializeAudio");

        Transform controlledGear = root.transform.Find("Gear");
        Assert.That(controlledGear, Is.Not.Null);
        Assert.That(controlledGear, Is.Not.SameAs(collision.transform));
        Assert.That(collision.transform.parent, Is.SameAs(unrelatedParent.transform));
        Assert.That(unrelatedSource.clip, Is.SameAs(unrelatedClip));
        Assert.That(unrelatedSource.loop, Is.False);
        Assert.That(unrelatedSource.playOnAwake, Is.True);
        Assert.That(unrelatedSource.spatialBlend, Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(controlledGear.GetComponent<AudioSource>(), Is.Not.Null);
    }

    [Test]
    public void ActiveEngineMinusOneRoutesStarterToBothEngines()
    {
        AssertStarterRouting(true, -1f, true, true);
    }

    [Test]
    public void ActiveEngineZeroRoutesStarterOnlyToLeftEngine()
    {
        AssertStarterRouting(true, 0f, true, false);
    }

    [Test]
    public void MissingActiveEngineDoesNotSuppressSimultaneousN2Starts()
    {
        AssertStarterRouting(false, 0f, true, true);
    }

    [Test]
    public void PreviewTouchdownSelectsEachOfTheFourBoomClips()
    {
        Component controller = CreateController();
        AudioSource source = FindSource(controller.gameObject, "Touchdown");

        for (int severity = 1; severity <= 4; severity++)
        {
            Invoke(controller, "PreviewTouchdown", severity);
            Assert.That(
                source.clip,
                Is.SameAs(Resources.Load<AudioClip>("Audio/B737/Ground/touchdown_" + severity)));
        }
    }

    [Test]
    public void PreviewCalloutSelectsTheRequestedFiveHundredFootClip()
    {
        Component controller = CreateController();
        AudioSource source = FindSource(controller.gameObject, "Radio Altitude");

        Invoke(controller, "PreviewCallout", 500);

        Assert.That(
            source.clip,
            Is.SameAs(Resources.Load<AudioClip>("Audio/B737/Callouts/500")));
    }

    [Test]
    public void MechanicalAndWarningPreviewsUpdateTheirSourceState()
    {
        Component controller = CreateController();
        AssertPreviewToggle(controller, "PreviewGear", "Gear");
        AssertPreviewToggle(controller, "PreviewFlaps", "Flaps");
        AssertPreviewToggle(controller, "PreviewStall", "Stall");
        AssertPreviewToggle(controller, "PreviewOverspeed", "Overspeed");

        AudioSource runway = FindSource(controller.gameObject, "Runway Roll");
        Invoke(controller, "PreviewRunwayRoll", 0.75f);
        Assert.That(runway.volume, Is.GreaterThan(0f));
        AssertFinite(runway.volume, runway.pitch);

        Invoke(controller, "PreviewRunwayRoll", 0f);
        Assert.That(runway.volume, Is.Zero.Within(0.0001f));
    }

    [Test]
    public void InvalidPreviewInputsAreClampedOrIgnoredWithoutThrowing()
    {
        Component controller = CreateController();

        Assert.DoesNotThrow(() => Invoke(controller, "PreviewEngineStart", -100));
        Assert.DoesNotThrow(() => Invoke(controller, "PreviewEngineStart", 100));
        Assert.DoesNotThrow(() => Invoke(controller, "PreviewTouchdown", int.MinValue));
        Assert.That(
            FindSource(controller.gameObject, "Touchdown").clip,
            Is.SameAs(Resources.Load<AudioClip>("Audio/B737/Ground/touchdown_1")));
        Assert.DoesNotThrow(() => Invoke(controller, "PreviewTouchdown", int.MaxValue));
        Assert.That(
            FindSource(controller.gameObject, "Touchdown").clip,
            Is.SameAs(Resources.Load<AudioClip>("Audio/B737/Ground/touchdown_4")));
        Assert.DoesNotThrow(() => Invoke(controller, "PreviewRunwayRoll", float.NaN));
        Assert.DoesNotThrow(() => Invoke(controller, "PreviewRunwayRoll", float.PositiveInfinity));
        Assert.DoesNotThrow(() => Invoke(controller, "PreviewCallout", -1));
        Assert.DoesNotThrow(() => Invoke(controller, "PreviewCallout", 123));
        Assert.DoesNotThrow(() => Invoke(controller, "StopAllPreviewAudio"));

        AudioSource[] sources = controller.GetComponentsInChildren<AudioSource>(true);
        for (int index = 0; index < sources.Length; index++)
        {
            AssertFinite(sources[index].volume, sources[index].pitch);
        }
    }

    private Component CreateController()
    {
        GameObject root = new GameObject("B737 Audio Controller Test Root");
        objectsToDestroy.Add(root);
        Component controller = root.AddComponent(ControllerType());
        Assert.That(controller, Is.Not.Null);
        Invoke(controller, "InitializeAudio");
        return controller;
    }

    private Component CreateControllerWithDependency(
        string dependencyTypeName,
        out Component dependency)
    {
        GameObject root = new GameObject("B737 Audio Controller Dependency Test Root");
        objectsToDestroy.Add(root);
        dependency = root.AddComponent(RuntimeType(dependencyTypeName));
        Assert.That(dependency, Is.Not.Null);

        Component controller = root.AddComponent(ControllerType());
        Assert.That(controller, Is.Not.Null);
        Invoke(controller, "InitializeAudio");
        return controller;
    }

    private void AssertStarterRouting(
        bool includeActiveEngine,
        float activeEngine,
        bool expectLeft,
        bool expectRight)
    {
        Component bridge;
        Component controller = CreateControllerWithDependency("JsbsimBridge", out bridge);
        Invoke(controller, "ArmEngineAudio");
        SetBridgeState(bridge, includeActiveEngine, activeEngine, 0f, 0f, 0f);
        InvokePrivate(controller, "UpdateConnectedEngines");

        AudioSource left = FindSource(controller.gameObject, "Left Engine Starter");
        AudioSource right = FindSource(controller.gameObject, "Right Engine Starter");
        left.volume = 0f;
        right.volume = 0f;

        SetBridgeState(bridge, includeActiveEngine, activeEngine, 1f, 6f, 6f);
        InvokePrivate(controller, "UpdateConnectedEngines");

        Assert.That(left.volume, Is.EqualTo(expectLeft ? 1f : 0f).Within(0.0001f));
        Assert.That(right.volume, Is.EqualTo(expectRight ? 1f : 0f).Within(0.0001f));
    }

    private static void SetBridgeState(
        Component bridge,
        bool includeActiveEngine,
        float activeEngine,
        float starterCommand,
        float leftN2,
        float rightN2)
    {
        Dictionary<string, float> latest = GetBridgeLatest(bridge);
        latest.Clear();
        latest["propulsion_starter_cmd"] = starterCommand;
        latest["propulsion_engine_n2"] = leftN2;
        latest["propulsion_engine_1_n2"] = rightN2;
        if (includeActiveEngine)
        {
            latest["propulsion_active_engine"] = activeEngine;
        }
    }

    private static Dictionary<string, float> GetBridgeLatest(Component bridge)
    {
        FieldInfo latestField = bridge.GetType().GetField(
            "latest",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(latestField, Is.Not.Null);
        return (Dictionary<string, float>)latestField.GetValue(bridge);
    }

    private static float ReadLoopBoundaryJump(string resourcePath)
    {
        string assetPath = Path.Combine(Application.dataPath, "Resources");
        string[] parts = resourcePath.Split('/');
        for (int index = 0; index < parts.Length; index++)
        {
            assetPath = Path.Combine(assetPath, parts[index]);
        }

        assetPath += ".wav";
        byte[] bytes = File.ReadAllBytes(assetPath);
        int channels = BitConverter.ToInt16(bytes, 22);
        int bitsPerSample = BitConverter.ToInt16(bytes, 34);
        Assert.That(bitsPerSample, Is.EqualTo(16), resourcePath);

        int dataOffset = FindWaveDataOffset(bytes);
        int sampleBytes = bitsPerSample / 8;
        int frameBytes = channels * sampleBytes;
        int dataSize = BitConverter.ToInt32(bytes, dataOffset - 4);
        Assert.That(dataSize, Is.GreaterThanOrEqualTo(frameBytes * 2), resourcePath);

        short first = BitConverter.ToInt16(bytes, dataOffset);
        short last = BitConverter.ToInt16(bytes, dataOffset + dataSize - frameBytes);
        return Mathf.Abs(first - last) / 32768f;
    }

    private static int FindWaveDataOffset(byte[] bytes)
    {
        for (int index = 12; index < bytes.Length - 8; index++)
        {
            if (bytes[index] == 'd' &&
                bytes[index + 1] == 'a' &&
                bytes[index + 2] == 't' &&
                bytes[index + 3] == 'a')
            {
                return index + 8;
            }
        }

        Assert.Fail("WAV data chunk is missing.");
        return 0;
    }

    private static int CountBridgeSubscriptionsFor(Component bridge, object target)
    {
        Delegate handlers = GetBridgeStateUpdatedHandlers(bridge);
        if (handlers == null)
        {
            return 0;
        }

        int count = 0;
        Delegate[] invocationList = handlers.GetInvocationList();
        for (int index = 0; index < invocationList.Length; index++)
        {
            if (ReferenceEquals(invocationList[index].Target, target))
            {
                count++;
            }
        }

        return count;
    }

    private static void RaiseBridgeStateUpdated(Component bridge)
    {
        Delegate handlers = GetBridgeStateUpdatedHandlers(bridge);
        Assert.That(handlers, Is.Not.Null);
        ((Action)handlers).Invoke();
    }

    private static Delegate GetBridgeStateUpdatedHandlers(Component bridge)
    {
        FieldInfo eventField = bridge.GetType().GetField(
            "OnStateUpdated",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(eventField, Is.Not.Null);
        return (Delegate)eventField.GetValue(bridge);
    }

    private static Type ControllerType()
    {
        Type type = RuntimeType("B737AudioController");
        Assert.That(type, Is.Not.Null, "B737AudioController is missing from Assembly-CSharp.");
        return type;
    }

    private static Type RuntimeType(string typeName)
    {
        Type type = Type.GetType(typeName + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, typeName + " is missing from Assembly-CSharp.");
        return type;
    }

    private static void AssertNamedSources(GameObject root)
    {
        AudioSource[] sources = root.GetComponentsInChildren<AudioSource>(true);
        Assert.That(sources, Has.Length.EqualTo(SourceNames.Length));

        for (int index = 0; index < SourceNames.Length; index++)
        {
            Assert.That(FindSource(root, SourceNames[index]), Is.Not.Null, SourceNames[index]);
        }
    }

    private static AudioSource FindSource(GameObject root, string name)
    {
        AudioSource[] sources = root.GetComponentsInChildren<AudioSource>(true);
        for (int index = 0; index < sources.Length; index++)
        {
            if (sources[index].gameObject.name == name)
            {
                return sources[index];
            }
        }

        Assert.Fail("AudioSource child is missing: " + name);
        return null;
    }

    private static void AssertPreviewToggle(Component controller, string methodName, string sourceName)
    {
        AudioSource source = FindSource(controller.gameObject, sourceName);
        Invoke(controller, methodName, true);
        Assert.That(source.clip, Is.Not.Null, sourceName);
        Assert.That(source.volume, Is.GreaterThan(0f), sourceName);

        Invoke(controller, methodName, false);
        Assert.That(source.volume, Is.Zero.Within(0.0001f), sourceName);
    }

    private static bool ReadBoolProperty(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null, propertyName);
        Assert.That(property.CanWrite, Is.False, propertyName + " must be read-only.");
        return (bool)property.GetValue(target, null);
    }

    private static object Invoke(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, methodName);
        return method.Invoke(target, arguments);
    }

    private static object InvokePrivate(
        object target,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, methodName);
        return method.Invoke(target, arguments);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, fieldName);
        return field.GetValue(target);
    }

    private static bool HasPerCallManagedAllocationSite(MethodInfo method)
    {
        MethodBody body = method.GetMethodBody();
        Assert.That(body, Is.Not.Null, method.Name);
        byte[] il = body.GetILAsByteArray();
        int offset = 0;

        while (offset < il.Length)
        {
            OpCode code = ReadOpCode(il, ref offset);
            int operandOffset = offset;
            int operandSize = GetOperandSize(code.OperandType, il, operandOffset);

            if (code == OpCodes.Newarr || code == OpCodes.Newobj)
            {
                return true;
            }

            if ((code == OpCodes.Call || code == OpCodes.Callvirt) && operandSize == 4)
            {
                int metadataToken = BitConverter.ToInt32(il, operandOffset);
                MethodBase calledMethod = method.Module.ResolveMethod(metadataToken);
                if (calledMethod.DeclaringType == typeof(string) &&
                    calledMethod.Name == "Concat")
                {
                    return true;
                }
            }

            offset += operandSize;
        }

        return false;
    }

    private static OpCode ReadOpCode(byte[] il, ref int offset)
    {
        byte first = il[offset++];
        if (first != 0xfe)
        {
            return OneByteOpCodes[first];
        }

        return TwoByteOpCodes[il[offset++]];
    }

    private static int GetOperandSize(
        OperandType operandType,
        byte[] il,
        int operandOffset)
    {
        switch (operandType)
        {
            case OperandType.InlineNone:
                return 0;
            case OperandType.ShortInlineBrTarget:
            case OperandType.ShortInlineI:
            case OperandType.ShortInlineVar:
                return 1;
            case OperandType.InlineVar:
                return 2;
            case OperandType.InlineBrTarget:
            case OperandType.InlineField:
            case OperandType.InlineI:
            case OperandType.InlineMethod:
            case OperandType.InlineSig:
            case OperandType.InlineString:
            case OperandType.InlineTok:
            case OperandType.InlineType:
            case OperandType.ShortInlineR:
                return 4;
            case OperandType.InlineI8:
            case OperandType.InlineR:
                return 8;
            case OperandType.InlineSwitch:
                int branchCount = BitConverter.ToInt32(il, operandOffset);
                return 4 + branchCount * 4;
            default:
                throw new InvalidOperationException(
                    "Unknown IL operand type: " + operandType);
        }
    }

    private static void AssertFinite(params float[] values)
    {
        for (int index = 0; index < values.Length; index++)
        {
            Assert.That(float.IsNaN(values[index]), Is.False);
            Assert.That(float.IsInfinity(values[index]), Is.False);
        }
    }
}
