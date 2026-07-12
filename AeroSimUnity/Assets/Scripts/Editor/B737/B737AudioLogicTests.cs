using System;
using System.Reflection;
using NUnit.Framework;

public class B737AudioLogicTests
{
    private static readonly int[] CalloutThresholds =
    {
        1000, 500, 400, 300, 200, 100, 50, 40, 30, 20, 10
    };

    [Test]
    public void OverspeedRequiresAValueStrictlyAboveAnEntryLimit()
    {
        object latch = Create("OverspeedLatch");

        Assert.That(UpdateBool(latch, 350d, 0.82d), Is.False);
    }

    [Test]
    public void OverspeedIgnoresAnAlreadyExceededInitialSampleUntilRearmed()
    {
        object latch = Create("OverspeedLatch");

        Assert.That(UpdateBool(latch, 380d, 0.58d), Is.False);
        Assert.That(UpdateBool(latch, 380d, 0.58d), Is.False);
        Assert.That(UpdateBool(latch, 344d, 0.80d), Is.False);
        Assert.That(UpdateBool(latch, 351d, 0.80d), Is.True);
    }

    [TestCase(350.01d, 0.7d)]
    [TestCase(300d, 0.821d)]
    public void OverspeedEntersWhenEitherEntryLimitIsExceeded(double kias, double mach)
    {
        object latch = Create("OverspeedLatch");

        Assert.That(UpdateBool(latch, 300d, 0.7d), Is.False);
        Assert.That(UpdateBool(latch, kias, mach), Is.True);
    }

    [Test]
    public void OverspeedRemainsLatchedUntilBothValuesAreStrictlyBelowExitLimits()
    {
        object latch = Create("OverspeedLatch");

        Assert.That(UpdateBool(latch, 300d, 0.7d), Is.False);
        Assert.That(UpdateBool(latch, 351d, 0.7d), Is.True);
        Assert.That(UpdateBool(latch, 344d, 0.81d), Is.True);
        Assert.That(UpdateBool(latch, 345d, 0.80d), Is.True);
        Assert.That(UpdateBool(latch, 344d, 0.80d), Is.False);
    }

    [Test]
    public void MovementIgnoresTheFirstSampleAndChangesAtOrBelowEpsilon()
    {
        object detector = Create("MovementDetector", 0.1d, 1d);

        Assert.That(UpdateBool(detector, 10d, 20d, 30d, 0.5d), Is.False);
        Assert.That(UpdateBool(detector, 10.1d, 20d, 30d, 0.5d), Is.False);
    }

    [Test]
    public void MovementStopsOnlyAfterTheConfiguredContinuousStableTime()
    {
        object detector = Create("MovementDetector", 0.1d, 1d);

        Assert.That(UpdateBool(detector, 0d, 0d, 0d, 0d), Is.False);
        Assert.That(UpdateBool(detector, 0.2d, 0d, 0d, 0.1d), Is.True);
        Assert.That(UpdateBool(detector, 0.2d, 0d, 0d, 0.5d), Is.True);
        Assert.That(UpdateBool(detector, 0.2d, 0d, 0d, 0.5d), Is.False);
    }

    [Test]
    public void MovementResetsTheStableTimerWhenMotionResumes()
    {
        object detector = Create("MovementDetector", 0.1d, 1d);

        UpdateBool(detector, 0d, 0d, 0d, 0d);
        Assert.That(UpdateBool(detector, 0.2d, 0d, 0d, 0d), Is.True);
        Assert.That(UpdateBool(detector, 0.2d, 0d, 0d, 0.75d), Is.True);
        Assert.That(UpdateBool(detector, 0.4d, 0d, 0d, 0.25d), Is.True);
        Assert.That(UpdateBool(detector, 0.4d, 0d, 0d, 0.5d), Is.True);
        Assert.That(UpdateBool(detector, 0.4d, 0d, 0d, 0.5d), Is.False);
    }

    [Test]
    public void MovementDetectsCumulativeSubEpsilonMotionAtSixtyHertz()
    {
        object detector = Create("MovementDetector", 0.1d, 1d);
        const double frameSeconds = 1d / 60d;

        UpdateBool(detector, 0d, 0d, 0d, frameSeconds);
        for (int frame = 1; frame <= 60; frame++)
        {
            bool moving = UpdateBool(
                detector,
                frame * 0.05d,
                0d,
                0d,
                frameSeconds);

            if (frame >= 3)
            {
                Assert.That(moving, Is.True);
            }
        }

        bool finalState = true;
        for (int frame = 0; frame < 60; frame++)
        {
            finalState = UpdateBool(detector, 3d, 0d, 0d, frameSeconds);
        }

        Assert.That(finalState, Is.False);
    }

    [Test]
    public void MovementTreatsTwoSecondsOfSubEpsilonJitterAsStable()
    {
        object detector = Create("MovementDetector", 0.1d, 1d);
        const double frameSeconds = 1d / 60d;

        UpdateBool(detector, 0d, 0d, 0d, 0d);
        Assert.That(UpdateBool(detector, 0.2d, 0d, 0d, 0d), Is.True);

        bool finalState = true;
        for (int frame = 0; frame < 120; frame++)
        {
            double jitter = frame % 2 == 0 ? 0.00001d : -0.00001d;
            finalState = UpdateBool(
                detector,
                0.2d + jitter,
                0d,
                0d,
                frameSeconds);
        }

        Assert.That(finalState, Is.False);
    }

    [Test]
    public void MovementIgnoresNaNPositionWithoutAdvancingItsAnchor()
    {
        object detector = Create("MovementDetector", 0.1d, 1d);

        Assert.That(UpdateBool(detector, 0d, 0d, 0d, 0d), Is.False);
        Assert.That(UpdateBool(detector, double.NaN, 0d, 0d, 0.5d), Is.False);
        Assert.That(UpdateBool(detector, 0.2d, 0d, 0d, 0d), Is.True);
    }

    [Test]
    public void MovementTreatsNaNDeltaTimeAsZeroAndCanStillStop()
    {
        object detector = Create("MovementDetector", 0.1d, 1d);

        UpdateBool(detector, 0d, 0d, 0d, 0d);
        Assert.That(UpdateBool(detector, 0.2d, 0d, 0d, 0d), Is.True);
        Assert.That(UpdateBool(detector, 0.2d, 0d, 0d, double.NaN), Is.True);
        Assert.That(UpdateBool(detector, 0.2d, 0d, 0d, 0.5d), Is.True);
        Assert.That(UpdateBool(detector, 0.2d, 0d, 0d, 0.5d), Is.False);
    }

    [Test]
    public void CalloutsDoNotFireOnTheFirstSample()
    {
        object tracker = Create("CalloutTracker");

        Assert.That(UpdateInts(tracker, 5d), Is.Empty);
        Assert.That(UpdateInts(tracker, 0d), Is.Empty);
    }

    [Test]
    public void CalloutsFireOnceAsEachThresholdIsCrossedInDescent()
    {
        object tracker = Create("CalloutTracker");
        Assert.That(UpdateInts(tracker, 1200d), Is.Empty);

        for (int index = 0; index < CalloutThresholds.Length; index++)
        {
            int threshold = CalloutThresholds[index];
            Assert.That(UpdateInts(tracker, threshold), Is.EqualTo(new[] { threshold }));
            Assert.That(UpdateInts(tracker, threshold - 1d), Is.Empty);
        }

        Assert.That(UpdateInts(tracker, 0d), Is.Empty);
    }

    [Test]
    public void FastDescentReturnsAllCrossedCalloutsFromHighToLow()
    {
        object tracker = Create("CalloutTracker");
        UpdateInts(tracker, 1200d);

        Assert.That(UpdateInts(tracker, 5d), Is.EqualTo(CalloutThresholds));
        Assert.That(UpdateInts(tracker, 0d), Is.Empty);
    }

    [Test]
    public void CalloutRearmsOnlyAboveThresholdPlusTwentyFootMargin()
    {
        object tracker = Create("CalloutTracker");
        UpdateInts(tracker, 600d);

        Assert.That(UpdateInts(tracker, 490d), Is.EqualTo(new[] { 500 }));
        UpdateInts(tracker, 520d);
        Assert.That(UpdateInts(tracker, 490d), Is.Empty);
        UpdateInts(tracker, 521d);
        Assert.That(UpdateInts(tracker, 490d), Is.EqualTo(new[] { 500 }));
    }

    [Test]
    public void CalloutsIgnoreNonFiniteAltitudesWithoutAdvancingHistory()
    {
        double[] invalidAltitudes =
        {
            double.NaN, double.PositiveInfinity, double.NegativeInfinity
        };

        for (int index = 0; index < invalidAltitudes.Length; index++)
        {
            object tracker = Create("CalloutTracker");
            UpdateInts(tracker, 600d);

            Assert.That(UpdateInts(tracker, invalidAltitudes[index]), Is.Empty);
            Assert.That(UpdateInts(tracker, 490d), Is.EqualTo(new[] { 500 }));
        }
    }

    [Test]
    public void InitialGroundSampleDoesNotReportTouchdown()
    {
        object detector = Create("TouchdownDetector");

        Assert.That(UpdateInt(detector, 0, true, 4d, -4d), Is.Zero);
        Assert.That(UpdateInt(detector, 0, true, 4d, -4d), Is.Zero);
    }

    [Test]
    public void EachWheelGroupReportsItsOwnWeightOnWheelsRisingEdgeOnce()
    {
        object detector = Create("TouchdownDetector");
        UpdateInt(detector, 0, false, 0d, 0d);
        UpdateInt(detector, 1, false, 0d, 0d);

        Assert.That(UpdateInt(detector, 0, true, 0.4d, -0.5d), Is.EqualTo(1));
        Assert.That(UpdateInt(detector, 0, true, 3.5d, -3.5d), Is.Zero);
        Assert.That(UpdateInt(detector, 1, true, 1.2d, -0.5d), Is.EqualTo(1));
    }

    [Test]
    public void WheelGroupRearmsAfterWeightOnWheelsClears()
    {
        object detector = Create("TouchdownDetector");
        UpdateInt(detector, 2, false, 0d, 0d);

        Assert.That(UpdateInt(detector, 2, true, 0.2d, -0.2d), Is.EqualTo(1));
        Assert.That(UpdateInt(detector, 2, false, 0d, 0d), Is.Zero);
        Assert.That(UpdateInt(detector, 2, true, 8.1d, -0.2d), Is.EqualTo(5));
    }

    [TestCase(0.6d, -0.8d, 1)]
    [TestCase(1.2d, 0.2d, 1)]
    [TestCase(1.25d, 0.2d, 2)]
    [TestCase(0.5d, -2.0d, 3)]
    [TestCase(3.048d, 0d, 3)]
    [TestCase(5.0d, 0d, 4)]
    [TestCase(-5.1d, -0.4d, 4)]
    [TestCase(8.0d, 0d, 5)]
    [TestCase(-8.1d, -0.4d, 5)]
    public void TouchdownBoomUsesMaximumAbsoluteMetersPerSecond(
        double compressionVelocity,
        double descentRate,
        int expectedBoom)
    {
        object detector = Create("TouchdownDetector");
        UpdateInt(detector, 0, false, 0d, 0d);

        Assert.That(
            UpdateInt(detector, 0, true, compressionVelocity, descentRate),
            Is.EqualTo(expectedBoom));
    }

    [Test]
    public void TenFeetPerSecondTouchdownIsHardLandingNotExplosionBoom()
    {
        object detector = Create("TouchdownDetector");
        UpdateInt(detector, 0, false, 0d, 0d);

        Assert.That(
            UpdateInt(detector, 0, true, 10d * 0.3048d, 0d),
            Is.EqualTo(3));
    }

    [Test]
    public void ShallowRollingTouchdownUsesNormalContactBelowTwoMetersPerSecond()
    {
        object detector = Create("TouchdownDetector");
        UpdateInt(detector, 0, false, 0d, 0d, 0d);

        Assert.That(
            UpdateInt(detector, 0, true, 1.8d, -0.7d, 70d),
            Is.EqualTo(1));
    }

    [Test]
    public void SteepOrSlowTouchdownDoesNotUseRollingAngleCap()
    {
        object detector = Create("TouchdownDetector");
        UpdateInt(detector, 0, false, 0d, 0d, 0d);

        Assert.That(
            UpdateInt(detector, 0, true, 1.8d, -0.7d, 2d),
            Is.EqualTo(2));
    }

    [Test]
    public void ShallowRollingTouchdownCanReduceHighBoomButNotSevereCrash()
    {
        object detector = Create("TouchdownDetector");
        UpdateInt(detector, 0, false, 0d, 0d, 0d);

        Assert.That(
            UpdateInt(detector, 0, true, 3.5d, -1d, 70d),
            Is.EqualTo(2));

        UpdateInt(detector, 0, false, 0d, 0d, 70d);
        Assert.That(
            UpdateInt(detector, 0, true, 5.1d, -1d, 70d),
            Is.EqualTo(3));

        UpdateInt(detector, 0, false, 0d, 0d, 70d);
        Assert.That(
            UpdateInt(detector, 0, true, 8.2d, -1d, 70d),
            Is.EqualTo(5));
    }

    [TestCase(double.NaN, 8.2d, 5)]
    [TestCase(8.2d, double.NaN, 5)]
    [TestCase(double.PositiveInfinity, 0.4d, 1)]
    [TestCase(double.PositiveInfinity, double.NegativeInfinity, 1)]
    public void TouchdownBoomIgnoresNonFiniteVelocityInputs(
        double compressionVelocity,
        double descentRate,
        int expectedBoom)
    {
        object detector = Create("TouchdownDetector");
        UpdateInt(detector, 0, false, 0d, 0d);

        Assert.That(
            UpdateInt(detector, 0, true, compressionVelocity, descentRate),
            Is.EqualTo(expectedBoom));
    }

    [Test]
    public void EngineSoundGainAndPitchClampOutsideTheN1Range()
    {
        Assert.That(InvokeStaticDouble("EngineSoundModel", "EvaluateGain", -10d), Is.Zero);
        Assert.That(InvokeStaticDouble("EngineSoundModel", "EvaluateGain", 120d), Is.EqualTo(1d));
        Assert.That(InvokeStaticDouble("EngineSoundModel", "EvaluatePitch", -10d), Is.EqualTo(0.65d).Within(0.0001d));
        Assert.That(InvokeStaticDouble("EngineSoundModel", "EvaluatePitch", 120d), Is.EqualTo(1.35d).Within(0.0001d));
    }

    [Test]
    public void EngineSoundIsEssentiallySilentAtLowN1()
    {
        Assert.That(InvokeStaticDouble("EngineSoundModel", "EvaluateGain", 10d), Is.Zero);
        Assert.That(InvokeStaticDouble("EngineSoundModel", "EvaluateGain", 20d), Is.Zero);
        Assert.That(InvokeStaticDouble("EngineSoundModel", "EvaluateGain", 25d), Is.LessThanOrEqualTo(0.1d));
    }

    [Test]
    public void EngineSoundGainAndPitchAreMonotonicAcrossN1Range()
    {
        double previousGain = -1d;
        double previousPitch = -1d;

        for (int n1 = 0; n1 <= 100; n1 += 10)
        {
            double gain = InvokeStaticDouble("EngineSoundModel", "EvaluateGain", (double)n1);
            double pitch = InvokeStaticDouble("EngineSoundModel", "EvaluatePitch", (double)n1);

            Assert.That(gain, Is.InRange(0d, 1d));
            Assert.That(pitch, Is.InRange(0.65d, 1.35d));
            Assert.That(gain, Is.GreaterThanOrEqualTo(previousGain));
            Assert.That(pitch, Is.GreaterThanOrEqualTo(previousPitch));

            previousGain = gain;
            previousPitch = pitch;
        }
    }

    [Test]
    public void EngineSoundUsesSafeDefaultsForNonFiniteN1()
    {
        double[] invalidN1Values =
        {
            double.NaN, double.PositiveInfinity, double.NegativeInfinity
        };

        for (int index = 0; index < invalidN1Values.Length; index++)
        {
            Assert.That(
                InvokeStaticDouble("EngineSoundModel", "EvaluateGain", invalidN1Values[index]),
                Is.Zero);
            Assert.That(
                InvokeStaticDouble("EngineSoundModel", "EvaluatePitch", invalidN1Values[index]),
                Is.EqualTo(0.65d).Within(0.0001d));
        }
    }

    [Test]
    public void InitiallyRunningEngineDoesNotReportAStartAndRearmsAfterStopping()
    {
        object detector = Create("EngineStartDetector");

        Assert.That(UpdateBool(detector, 0, false, 60d), Is.False);
        Assert.That(UpdateBool(detector, 0, false, 65d), Is.False);
        Assert.That(UpdateBool(detector, 0, false, 0d), Is.False);
        Assert.That(UpdateBool(detector, 0, true, 0d), Is.True);
    }

    [Test]
    public void StarterRisingEdgesAreTrackedOncePerEngine()
    {
        object detector = Create("EngineStartDetector");
        UpdateBool(detector, 0, false, 0d);
        UpdateBool(detector, 1, false, 0d);

        Assert.That(UpdateBool(detector, 0, true, 0d), Is.True);
        Assert.That(UpdateBool(detector, 0, true, 10d), Is.False);
        Assert.That(UpdateBool(detector, 1, true, 0d), Is.True);
    }

    [Test]
    public void N2RisingFromStoppedReportsOneStartAndStoppingRearmsIt()
    {
        object detector = Create("EngineStartDetector");
        UpdateBool(detector, 1, false, 0d);

        Assert.That(UpdateBool(detector, 1, false, 5d), Is.False);
        Assert.That(UpdateBool(detector, 1, false, 6d), Is.True);
        Assert.That(UpdateBool(detector, 1, false, 20d), Is.False);
        Assert.That(UpdateBool(detector, 1, false, 0d), Is.False);
        Assert.That(UpdateBool(detector, 1, false, 6d), Is.True);
    }

    [Test]
    public void FirstStarterSampleDoesNotInventAnUnknownRisingEdge()
    {
        object detector = Create("EngineStartDetector");

        Assert.That(UpdateBool(detector, 0, true, 0d), Is.False);
        Assert.That(UpdateBool(detector, 0, true, 6d), Is.False);
        Assert.That(UpdateBool(detector, 0, false, 0d), Is.False);
        Assert.That(UpdateBool(detector, 0, true, 0d), Is.True);
    }

    [Test]
    public void EngineStartIgnoresNonFiniteN2WithoutAdvancingHistory()
    {
        double[] invalidN2Values =
        {
            double.NaN, double.PositiveInfinity, double.NegativeInfinity
        };

        for (int index = 0; index < invalidN2Values.Length; index++)
        {
            object detector = Create("EngineStartDetector");
            UpdateBool(detector, 0, false, 0d);

            Assert.That(UpdateBool(detector, 0, true, invalidN2Values[index]), Is.False);
            Assert.That(UpdateBool(detector, 0, true, 0d), Is.True);
        }
    }

    private static object Create(string typeName, params object[] arguments)
    {
        Type type = RuntimeType(typeName);
        object instance = Activator.CreateInstance(type, arguments);
        Assert.That(instance, Is.Not.Null);
        return instance;
    }

    private static bool UpdateBool(object target, params object[] arguments)
    {
        return Invoke<bool>(target, "Update", arguments);
    }

    private static int UpdateInt(object target, params object[] arguments)
    {
        return Invoke<int>(target, "Update", arguments);
    }

    private static int[] UpdateInts(object target, params object[] arguments)
    {
        return Invoke<int[]>(target, "Update", arguments);
    }

    private static double InvokeStaticDouble(string typeName, string methodName, params object[] arguments)
    {
        Type type = RuntimeType(typeName);
        MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        return (double)method.Invoke(null, arguments);
    }

    private static T Invoke<T>(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = null;
        MethodInfo[] methods = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
        for (int index = 0; index < methods.Length; index++)
        {
            MethodInfo candidate = methods[index];
            if (candidate.Name == methodName &&
                candidate.GetParameters().Length == arguments.Length)
            {
                method = candidate;
                break;
            }
        }

        Assert.That(method, Is.Not.Null);
        return (T)method.Invoke(target, arguments);
    }

    private static Type RuntimeType(string typeName)
    {
        Type type = Type.GetType(typeName + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, typeName + " is missing from Assembly-CSharp.");
        return type;
    }
}
