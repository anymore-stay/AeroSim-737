using System;
using System.Collections.Generic;

public sealed class OverspeedLatch
{
    public const double EntryKias = 350d;
    public const double EntryMach = 0.82d;
    public const double ExitKias = 345d;
    public const double ExitMach = 0.81d;

    private bool hasSample;
    private bool isArmed;

    public bool IsActive { get; private set; }

    public bool Update(double kias, double mach)
    {
        if (!hasSample)
        {
            hasSample = true;
            isArmed = kias <= EntryKias && mach <= EntryMach;
            return false;
        }

        if (IsActive)
        {
            if (kias < ExitKias && mach < ExitMach)
            {
                IsActive = false;
            }
        }
        else if (!isArmed)
        {
            isArmed = kias < ExitKias && mach < ExitMach;
        }
        else if (kias > EntryKias || mach > EntryMach)
        {
            IsActive = true;
        }

        return IsActive;
    }
}

public sealed class MovementDetector
{
    private readonly double epsilonSquared;
    private readonly double stableSeconds;

    private bool hasMovementAnchor;
    private double movementAnchorX;
    private double movementAnchorY;
    private double movementAnchorZ;
    private double stableElapsedSeconds;

    public MovementDetector(double epsilon, double stableSeconds)
    {
        double nonNegativeEpsilon = Math.Max(0d, epsilon);
        epsilonSquared = nonNegativeEpsilon * nonNegativeEpsilon;
        this.stableSeconds = Math.Max(0d, stableSeconds);
    }

    public bool IsMoving { get; private set; }

    public bool Update(double x, double y, double z, double deltaSeconds)
    {
        if (!B737AudioLogicMath.IsFinite(x) ||
            !B737AudioLogicMath.IsFinite(y) ||
            !B737AudioLogicMath.IsFinite(z))
        {
            return IsMoving;
        }

        if (!hasMovementAnchor)
        {
            hasMovementAnchor = true;
            SetMovementAnchor(x, y, z);
            return false;
        }

        double deltaX = x - movementAnchorX;
        double deltaY = y - movementAnchorY;
        double deltaZ = z - movementAnchorZ;
        bool moved = deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ > epsilonSquared;

        if (moved)
        {
            SetMovementAnchor(x, y, z);
            IsMoving = true;
            stableElapsedSeconds = 0d;
        }
        else if (IsMoving)
        {
            double safeDeltaSeconds = B737AudioLogicMath.IsFinite(deltaSeconds)
                ? Math.Max(0d, deltaSeconds)
                : 0d;
            stableElapsedSeconds += safeDeltaSeconds;

            if (stableElapsedSeconds >= stableSeconds)
            {
                IsMoving = false;
                stableElapsedSeconds = 0d;
                SetMovementAnchor(x, y, z);
            }
        }

        return IsMoving;
    }

    private void SetMovementAnchor(double x, double y, double z)
    {
        movementAnchorX = x;
        movementAnchorY = y;
        movementAnchorZ = z;
    }
}

public sealed class CalloutTracker
{
    public const double DefaultRearmMarginFeet = 20d;

    private static readonly int[] Thresholds =
    {
        1000, 500, 400, 300, 200, 100, 50, 40, 30, 20, 10
    };

    private readonly bool[] armed = new bool[Thresholds.Length];
    private readonly double rearmMarginFeet;

    private bool hasPreviousAltitude;
    private double previousAltitudeFeet;

    public CalloutTracker()
        : this(DefaultRearmMarginFeet)
    {
    }

    public CalloutTracker(double rearmMarginFeet)
    {
        this.rearmMarginFeet = Math.Max(0d, rearmMarginFeet);
    }

    public int[] Update(double radioAltitudeFeet)
    {
        if (!B737AudioLogicMath.IsFinite(radioAltitudeFeet))
        {
            return Array.Empty<int>();
        }

        if (!hasPreviousAltitude)
        {
            hasPreviousAltitude = true;
            previousAltitudeFeet = radioAltitudeFeet;

            for (int index = 0; index < Thresholds.Length; index++)
            {
                armed[index] = radioAltitudeFeet > Thresholds[index];
            }

            return Array.Empty<int>();
        }

        List<int> crossed = null;

        for (int index = 0; index < Thresholds.Length; index++)
        {
            int threshold = Thresholds[index];

            if (radioAltitudeFeet > threshold + rearmMarginFeet)
            {
                armed[index] = true;
            }

            if (armed[index] &&
                previousAltitudeFeet > threshold &&
                radioAltitudeFeet <= threshold)
            {
                if (crossed == null)
                {
                    crossed = new List<int>();
                }

                crossed.Add(threshold);
                armed[index] = false;
            }
        }

        previousAltitudeFeet = radioAltitudeFeet;
        return crossed == null ? Array.Empty<int>() : crossed.ToArray();
    }
}

public sealed class TouchdownDetector
{
    // Inputs are metres per second. Each boundary selects the next louder boom.
    public const double Boom2SpeedMetersPerSecond = 1.25d;
    public const double Boom3SpeedMetersPerSecond = 2d;
    public const double Boom4SpeedMetersPerSecond = 5d;
    public const double Boom5SpeedMetersPerSecond = 8d;
    public const double RollingLandingMinimumHorizontalSpeedMetersPerSecond = 20d;
    public const double ShallowLandingAngleDegrees = 4d;
    public const double ModerateLandingAngleDegrees = 7d;
    public const double DesignLandingSinkRateMetersPerSecond = 10d * 0.3048d;

    private const double MinimumAngleDescentMetersPerSecond = 0.05d;
    private const double RadiansToDegrees = 180d / Math.PI;

    private readonly Dictionary<int, bool> previousWeightOnWheels =
        new Dictionary<int, bool>();

    public int Update(
        int wheelGroup,
        bool weightOnWheels,
        double maximumCompressionVelocityMetersPerSecond,
        double descentRateMetersPerSecond)
    {
        return Update(
            wheelGroup,
            weightOnWheels,
            maximumCompressionVelocityMetersPerSecond,
            descentRateMetersPerSecond,
            0d);
    }

    public int Update(
        int wheelGroup,
        bool weightOnWheels,
        double maximumCompressionVelocityMetersPerSecond,
        double descentRateMetersPerSecond,
        double horizontalSpeedMetersPerSecond)
    {
        bool wasOnGround;
        if (!previousWeightOnWheels.TryGetValue(wheelGroup, out wasOnGround))
        {
            previousWeightOnWheels[wheelGroup] = weightOnWheels;
            return 0;
        }

        previousWeightOnWheels[wheelGroup] = weightOnWheels;
        if (wasOnGround || !weightOnWheels)
        {
            return 0;
        }

        double compressionSpeed = B737AudioLogicMath.IsFinite(
            maximumCompressionVelocityMetersPerSecond)
            ? Math.Abs(maximumCompressionVelocityMetersPerSecond)
            : 0d;
        double descentSpeed = B737AudioLogicMath.IsFinite(descentRateMetersPerSecond)
            ? Math.Abs(descentRateMetersPerSecond)
            : 0d;
        double horizontalSpeed = B737AudioLogicMath.IsFinite(horizontalSpeedMetersPerSecond)
            ? Math.Abs(horizontalSpeedMetersPerSecond)
            : 0d;
        double impactSpeed = Math.Max(compressionSpeed, descentSpeed);

        if (impactSpeed >= Boom5SpeedMetersPerSecond)
        {
            return ApplyRollingLandingCap(5, impactSpeed, descentSpeed, horizontalSpeed);
        }

        if (impactSpeed >= Boom4SpeedMetersPerSecond)
        {
            return ApplyRollingLandingCap(4, impactSpeed, descentSpeed, horizontalSpeed);
        }

        if (impactSpeed >= Boom3SpeedMetersPerSecond)
        {
            return ApplyRollingLandingCap(3, impactSpeed, descentSpeed, horizontalSpeed);
        }

        int severity = impactSpeed >= Boom2SpeedMetersPerSecond ? 2 : 1;
        return ApplyRollingLandingCap(severity, impactSpeed, descentSpeed, horizontalSpeed);
    }

    private static int ApplyRollingLandingCap(
        int severity,
        double impactSpeed,
        double descentSpeed,
        double horizontalSpeed)
    {
        if (severity <= 1 ||
            horizontalSpeed < RollingLandingMinimumHorizontalSpeedMetersPerSecond ||
            descentSpeed < MinimumAngleDescentMetersPerSecond)
        {
            return severity;
        }

        double angleDegrees = Math.Atan2(descentSpeed, horizontalSpeed) * RadiansToDegrees;
        if (angleDegrees <= ShallowLandingAngleDegrees)
        {
            if (impactSpeed < Boom3SpeedMetersPerSecond)
            {
                return 1;
            }

            if (impactSpeed < Boom4SpeedMetersPerSecond)
            {
                return Math.Min(severity, 2);
            }

            if (descentSpeed <= DesignLandingSinkRateMetersPerSecond &&
                impactSpeed < Boom5SpeedMetersPerSecond)
            {
                return Math.Min(severity, 3);
            }
        }

        if (angleDegrees <= ModerateLandingAngleDegrees &&
            descentSpeed <= DesignLandingSinkRateMetersPerSecond &&
            impactSpeed < Boom4SpeedMetersPerSecond)
        {
            return Math.Min(severity, 2);
        }

        return severity;
    }
}

public static class EngineSoundModel
{
    public const double SilentBelowN1Percent = 20d;
    public const double FullGainN1Percent = 100d;
    public const double MinimumPitch = 0.65d;
    public const double MaximumPitch = 1.35d;

    public static double EvaluateGain(double n1Percent)
    {
        double clampedN1 = Clamp(n1Percent, SilentBelowN1Percent, FullGainN1Percent);
        return (clampedN1 - SilentBelowN1Percent) /
            (FullGainN1Percent - SilentBelowN1Percent);
    }

    public static double EvaluatePitch(double n1Percent)
    {
        double normalizedN1 = Clamp(n1Percent, 0d, 100d) / 100d;
        return MinimumPitch + (MaximumPitch - MinimumPitch) * normalizedN1;
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        if (!B737AudioLogicMath.IsFinite(value))
        {
            return minimum;
        }

        if (value < minimum)
        {
            return minimum;
        }

        return value > maximum ? maximum : value;
    }
}

public sealed class EngineStartDetector
{
    public const double StoppedN2Percent = 5d;

    private readonly Dictionary<int, EngineState> engines =
        new Dictionary<int, EngineState>();

    public bool Update(int engineIndex, bool starterCommanded, double n2Percent)
    {
        if (!B737AudioLogicMath.IsFinite(n2Percent))
        {
            return false;
        }

        EngineState state;
        if (!engines.TryGetValue(engineIndex, out state))
        {
            state = new EngineState
            {
                Armed = !starterCommanded && n2Percent <= StoppedN2Percent,
                PreviousStarterCommanded = starterCommanded,
                PreviousN2Percent = n2Percent
            };
            engines.Add(engineIndex, state);
            return false;
        }

        if (!state.Armed && !starterCommanded && n2Percent <= StoppedN2Percent)
        {
            state.Armed = true;
        }

        bool starterRising = starterCommanded && !state.PreviousStarterCommanded;
        bool n2RisingFromStopped =
            state.PreviousN2Percent <= StoppedN2Percent &&
            n2Percent > StoppedN2Percent;
        bool started = state.Armed && (starterRising || n2RisingFromStopped);

        if (started)
        {
            state.Armed = false;
        }

        state.PreviousStarterCommanded = starterCommanded;
        state.PreviousN2Percent = n2Percent;
        return started;
    }

    private sealed class EngineState
    {
        public bool Armed;
        public bool PreviousStarterCommanded;
        public double PreviousN2Percent;
    }
}

internal static class B737AudioLogicMath
{
    public static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
