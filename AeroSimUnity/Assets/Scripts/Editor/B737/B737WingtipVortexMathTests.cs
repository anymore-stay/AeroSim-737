using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class B737WingtipVortexMathTests
{
    private const string PrefabPath = "Assets/Aircraft/B737/Prefabs/B737.prefab";

    [Test]
    public void B737Prefab_BindsWingtipAnchorsAndExistingContrailSource()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);

        B737WingtipVortexController controller = prefab.GetComponent<B737WingtipVortexController>();
        Assert.That(controller, Is.Not.Null);

        SerializedObject serializedController = new SerializedObject(controller);
        Transform leftWingtip = serializedController.FindProperty("leftWingtip").objectReferenceValue as Transform;
        Transform rightWingtip = serializedController.FindProperty("rightWingtip").objectReferenceValue as Transform;
        ParticleSystem source = serializedController.FindProperty("sourceParticle").objectReferenceValue as ParticleSystem;
        ParticleSystem leftVortex = serializedController.FindProperty("leftVortex").objectReferenceValue as ParticleSystem;
        ParticleSystem rightVortex = serializedController.FindProperty("rightVortex").objectReferenceValue as ParticleSystem;
        float spacing = serializedController.FindProperty("particleSpacingMeters").floatValue;
        int maximumParticles = serializedController.FindProperty("maximumParticles").intValue;
        float minimumLifetime = serializedController.FindProperty("minimumLifetimeSeconds").floatValue;
        float maximumLifetime = serializedController.FindProperty("maximumLifetimeSeconds").floatValue;
        float recenterThreshold = serializedController.FindProperty("minimumRecenteringDistanceMeters").floatValue;

        Assert.That(leftWingtip, Is.Not.Null);
        Assert.That(leftWingtip.name, Is.EqualTo("STROBE_LeftWing_Movable"));
        Assert.That(rightWingtip, Is.Not.Null);
        Assert.That(rightWingtip.name, Is.EqualTo("STROBE_RightWing_Movable"));
        Assert.That(source, Is.Not.Null);
        Assert.That(source.name, Does.Contain("B737RealisticContrail"));
        Assert.That(leftVortex, Is.Not.Null);
        Assert.That(leftVortex.name, Is.EqualTo("B737WingtipVortex_Left"));
        Assert.That(leftVortex.transform.parent, Is.EqualTo(leftWingtip));
        Assert.That(rightVortex, Is.Not.Null);
        Assert.That(rightVortex.name, Is.EqualTo("B737WingtipVortex_Right"));
        Assert.That(rightVortex.transform.parent, Is.EqualTo(rightWingtip));
        Assert.That(leftVortex.GetComponent<ParticleSystemRenderer>().sharedMaterial,
            Is.EqualTo(source.GetComponent<ParticleSystemRenderer>().sharedMaterial));
        Assert.That(rightVortex.GetComponent<ParticleSystemRenderer>().sharedMaterial,
            Is.EqualTo(source.GetComponent<ParticleSystemRenderer>().sharedMaterial));
        Assert.That(spacing, Is.EqualTo(0.05f).Within(0.001f));
        Assert.That(maximumParticles, Is.EqualTo(1800));
        Assert.That(minimumLifetime, Is.EqualTo(0.4f).Within(0.001f));
        Assert.That(maximumLifetime, Is.EqualTo(0.7f).Within(0.001f));
        Assert.That(recenterThreshold, Is.LessThanOrEqualTo(5f));
    }

    [Test]
    public void LiftDemandLatch_FlapsLowerTheOnsetAngle()
    {
        bool cleanWing = B737WingtipVortexMath.UpdateLiftDemandLatch(false, 4f, 0f, 5.2f, 1.2f);
        bool fullFlaps = B737WingtipVortexMath.UpdateLiftDemandLatch(false, 4f, 1f, 5.2f, 1.2f);

        Assert.That(cleanWing, Is.False);
        Assert.That(fullFlaps, Is.True);
    }

    [Test]
    public void LiftDemandLatch_UsesReleaseHysteresis()
    {
        Assert.That(B737WingtipVortexMath.UpdateLiftDemandLatch(true, 4.2f, 0f, 5.2f, 1.2f), Is.True);
        Assert.That(B737WingtipVortexMath.UpdateLiftDemandLatch(true, 3.9f, 0f, 5.2f, 1.2f), Is.False);
    }

    [TestCase(false, 0, 0f)]
    [TestCase(false, 2, 0.55f)]
    [TestCase(false, 4, 1f)]
    [TestCase(true, 0, 1f)]
    public void MoistureFactor_MapsWeatherToCondensationPotential(bool precipitation, int clouds, float expected)
    {
        float moisture = B737WingtipVortexMath.CalculateMoistureFactor(precipitation, clouds);

        Assert.That(moisture, Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void VisibleStrength_RequiresAirborneHighLiftAndMoistAir()
    {
        float visible = B737WingtipVortexMath.CalculateVisibleStrength(
            true, 150f, 6f, 0.4f, true, 0.82f, 105f, 225f, 5.2f);
        float onGround = B737WingtipVortexMath.CalculateVisibleStrength(
            false, 150f, 6f, 0.4f, true, 0.82f, 105f, 225f, 5.2f);
        float dryAir = B737WingtipVortexMath.CalculateVisibleStrength(
            true, 150f, 6f, 0.4f, true, 0f, 105f, 225f, 5.2f);

        Assert.That(visible, Is.GreaterThan(0.35f));
        Assert.That(onGround, Is.Zero);
        Assert.That(dryAir, Is.Zero);
    }

    [Test]
    public void VisibleStrength_FadesOutAboveTheLowSpeedLiftEnvelope()
    {
        float strength = B737WingtipVortexMath.CalculateVisibleStrength(
            true, 230f, 7f, 0.4f, true, 1f, 105f, 225f, 5.2f);

        Assert.That(strength, Is.Zero);
    }
}
