using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class JsbsimBridgeGroundClippingTests
{
    private const int TestGroundLayer = 31;
    private const int TestGroundMask = 1 << TestGroundLayer;

    private GameObject ground;
    private readonly List<GameObject> objectsToDestroy = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int index = objectsToDestroy.Count - 1; index >= 0; index--)
        {
            if (objectsToDestroy[index] != null)
                UnityEngine.Object.DestroyImmediate(objectsToDestroy[index]);
        }

        objectsToDestroy.Clear();
        ground = null;
    }

    [Test]
    public void ClampPositionAboveGroundRaisesAircraftWhenTargetWouldClipRunway()
    {
        CreateGroundWithTopAtZero();
        MethodInfo method = GetClampMethod();

        var desired = new Vector3(0f, -2f, 0f);
        var clamped = (Vector3)method.Invoke(
            null,
            new object[] { desired, true, TestGroundMask, 10f, 20f, 2.5f, null });

        Assert.That(clamped.x, Is.EqualTo(desired.x).Within(0.001f));
        Assert.That(clamped.z, Is.EqualTo(desired.z).Within(0.001f));
        Assert.That(clamped.y, Is.EqualTo(2.5f).Within(0.001f));
    }

    [Test]
    public void ClampPositionAboveGroundDoesNotLowerAircraftAlreadyAboveClearance()
    {
        CreateGroundWithTopAtZero();
        MethodInfo method = GetClampMethod();

        var desired = new Vector3(0f, 5f, 0f);
        var clamped = (Vector3)method.Invoke(
            null,
            new object[] { desired, true, TestGroundMask, 10f, 20f, 2.5f, null });

        Assert.That(clamped, Is.EqualTo(desired));
    }

    [Test]
    public void ClampPositionAboveGroundReturnsOriginalPositionWhenDisabled()
    {
        CreateGroundWithTopAtZero();
        MethodInfo method = GetClampMethod();

        var desired = new Vector3(0f, -2f, 0f);
        var clamped = (Vector3)method.Invoke(
            null,
            new object[] { desired, false, TestGroundMask, 10f, 20f, 2.5f, null });

        Assert.That(clamped, Is.EqualTo(desired));
    }

    [Test]
    public void ClampPositionAboveGroundIgnoresAircraftOwnColliders()
    {
        CreateGroundWithTopAtZero();
        var aircraft = new GameObject("Aircraft");
        objectsToDestroy.Add(aircraft);
        var ownCollider = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ownCollider.name = "Own Collider";
        ownCollider.layer = TestGroundLayer;
        ownCollider.transform.SetParent(aircraft.transform);
        ownCollider.transform.position = new Vector3(0f, 4.5f, 0f);
        ownCollider.transform.localScale = new Vector3(2f, 1f, 2f);
        objectsToDestroy.Add(ownCollider);
        Physics.SyncTransforms();

        MethodInfo method = GetClampMethod();

        var desired = new Vector3(0f, -2f, 0f);
        var clamped = (Vector3)method.Invoke(
            null,
            new object[] { desired, true, TestGroundMask, 10f, 20f, 2.5f, aircraft.transform });

        Assert.That(clamped.y, Is.EqualTo(2.5f).Within(0.001f));
    }

    [Test]
    public void ClampPositionAboveGroundRaisesAircraftWhenRotatedProbeWouldClipRunway()
    {
        CreateGroundWithTopAtZero();
        MethodInfo method = GetClampMethod(9);

        var desired = new Vector3(0f, 5f, 0f);
        Quaternion rotation = Quaternion.Euler(20f, 0f, 0f);
        var localProbes = new[] { new Vector3(0f, -4f, 10f) };

        var clamped = (Vector3)method.Invoke(
            null,
            new object[]
            {
                desired,
                true,
                TestGroundMask,
                10f,
                40f,
                2.5f,
                rotation,
                null,
                localProbes
            });

        Vector3 probeWorld = clamped + rotation * localProbes[0];
        Assert.That(clamped.y, Is.GreaterThan(desired.y));
        Assert.That(probeWorld.y, Is.GreaterThanOrEqualTo(0.049f));
    }

    [Test]
    public void DefaultGroundProbeFootprintProtectsLongTailOnPositiveZ()
    {
        CreateGroundWithTopAtZero();
        Type bridgeType = Type.GetType("JsbsimBridge, Assembly-CSharp");
        Assert.That(bridgeType, Is.Not.Null);
        object bridge = new GameObject("Bridge").AddComponent(bridgeType);
        objectsToDestroy.Add(((Component)bridge).gameObject);
        MethodInfo getProbes = bridgeType.GetMethod(
            "GetGroundProbeLocalPoints",
            BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo clamp = GetClampMethod(9);

        Vector3[] probes = (Vector3[])getProbes.Invoke(bridge, null);
        var desired = new Vector3(0f, 6.35f, 0f);
        Quaternion rotation = Quaternion.Euler(12f, 0f, 0f);

        var clamped = (Vector3)clamp.Invoke(
            null,
            new object[]
            {
                desired,
                true,
                TestGroundMask,
                20f,
                80f,
                6.35f,
                rotation,
                null,
                probes
            });

        Vector3 tailLowPoint = clamped + rotation * new Vector3(0f, -6.35f, 32f);
        Assert.That(tailLowPoint.y, Is.GreaterThanOrEqualTo(0.049f));
    }

    [Test]
    public void ApplyStateUsesCurrentPoseWhenClampingGroundProbes()
    {
        CreateGroundWithTopAtZero();
        Type bridgeType = Type.GetType("JsbsimBridge, Assembly-CSharp");
        Assert.That(bridgeType, Is.Not.Null);
        Component bridge = new GameObject("Bridge").AddComponent(bridgeType);
        objectsToDestroy.Add(bridge.gameObject);

        SetPrivateField(bridge, "aircraft", bridge.transform);
        SetPrivateField(bridge, "sceneStartPos", new Vector3(0f, 6.35f, 0f));
        SetPrivateField(bridge, "sceneStartRot", Quaternion.identity);
        SetPrivateField(bridge, "targetRot", Quaternion.Euler(20f, 0f, 0f));
        SetPrivateField(bridge, "groundRaycastStartHeight", 20f);
        SetPrivateField(bridge, "groundRaycastDistance", 80f);
        SetPrivateField(bridge, "groundCollisionMask", (LayerMask)TestGroundMask);

        var state = new Dictionary<string, float>
        {
            { "lat_deg", 0f },
            { "lon_deg", 0f },
            { "alt_ft", 0f },
            { "phi_rad", 0f },
            { "theta_rad", 0f },
            { "psi_rad", 0f }
        };
        MethodInfo applyState = bridgeType.GetMethod(
            "ApplyState",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(applyState, Is.Not.Null);

        applyState.Invoke(bridge, new object[] { state });

        Vector3 targetPosition = (Vector3)GetPrivateField(bridge, "targetPos");
        Assert.That(targetPosition.y, Is.InRange(6.39f, 6.41f));
    }

    [Test]
    public void MainSceneKeepsAircraftAndRunwayWithinGroundProbeRange()
    {
        bool openedForTest;
        Scene scene = OpenMainScene(out openedForTest);
        try
        {
            GameObject runway = FindSceneGameObject(scene, "Runway_Physics_Surface");
            Component bridge = FindSceneComponent(scene, "JsbsimBridge");
            Collider[] colliders = runway.GetComponentsInChildren<Collider>(true);
            Assert.That(colliders, Is.Not.Empty);

            float highestRunwayY = float.NegativeInfinity;
            for (int index = 0; index < colliders.Length; index++)
                highestRunwayY = Mathf.Max(highestRunwayY, colliders[index].bounds.max.y);

            float startHeight = (float)GetPrivateField(bridge, "groundRaycastStartHeight");
            float rayDistance = (float)GetPrivateField(bridge, "groundRaycastDistance");
            float verticalSeparation = Mathf.Abs(bridge.transform.position.y - highestRunwayY);
            Assert.That(verticalSeparation, Is.LessThan(startHeight + rayDistance));
        }
        finally
        {
            if (openedForTest)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void MainSceneRunwayCollidersUseDedicatedCollisionLayer()
    {
        bool openedForTest;
        Scene scene = OpenMainScene(out openedForTest);
        try
        {
            int runwayLayer = LayerMask.NameToLayer("Runway");
            Assert.That(runwayLayer, Is.GreaterThanOrEqualTo(0));

            GameObject runway = FindSceneGameObject(scene, "Runway_Physics_Surface");
            Transform[] runwayObjects = runway.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < runwayObjects.Length; index++)
                Assert.That(runwayObjects[index].gameObject.layer, Is.EqualTo(runwayLayer));

            Component bridge = FindSceneComponent(scene, "JsbsimBridge");
            LayerMask mask = (LayerMask)GetPrivateField(bridge, "groundCollisionMask");
            Assert.That(mask.value, Is.EqualTo(1 << runwayLayer));
        }
        finally
        {
            if (openedForTest)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [TestCase("Assets/Environment/Airport/6986/textures/JC-停车场_Normal.png", false)]
    [TestCase("Assets/Environment/Airport/6986/textures/JC-塔台_Normal.png", false)]
    [TestCase("Assets/Environment/Airport/6986/textures/JC-航站楼_Normal_DirectX.png", true)]
    [TestCase("Assets/Environment/Airport/6986/textures/JC-高速路_Normal.png", false)]
    public void AirportNormalTexturesUseNormalMapImportSettings(string path, bool flipGreen)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Assert.That(importer, Is.Not.Null, path);
        Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.NormalMap), path);
        Assert.That(importer.sRGBTexture, Is.False, path);
        Assert.That(importer.flipGreenChannel, Is.EqualTo(flipGreen), path);
    }

    [TestCase("Assets/Environment/Airport/6986/textures/JC-停车场_Roughness.png")]
    [TestCase("Assets/Environment/Airport/6986/textures/JC-塔台_Metallic.png")]
    [TestCase("Assets/Environment/Airport/6986/textures/JC-塔台_Roughness.png")]
    [TestCase("Assets/Environment/Airport/6986/textures/JC-航站楼_Metallic.png")]
    [TestCase("Assets/Environment/Airport/6986/textures/JC-航站楼_Roughness.png")]
    [TestCase("Assets/Environment/Airport/6986/textures/JC-高速路_Metallic.png")]
    [TestCase("Assets/Environment/Airport/6986/textures/JC-高速路_Roughness.png")]
    public void AirportDataTexturesUseLinearImportSettings(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Assert.That(importer, Is.Not.Null, path);
        Assert.That(importer.sRGBTexture, Is.False, path);
    }

    private void CreateGroundWithTopAtZero()
    {
        ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Test_Runway_Physics_Surface";
        ground.layer = TestGroundLayer;
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(100f, 1f, 100f);
        objectsToDestroy.Add(ground);
        Physics.SyncTransforms();
    }

    private static Scene OpenMainScene(out bool openedForTest)
    {
        const string scenePath = "Assets/Scenes/MainScene.unity";
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        openedForTest = !scene.IsValid() || !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        return scene;
    }

    private static GameObject FindSceneGameObject(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index].name == objectName)
                    return transforms[index].gameObject;
            }
        }

        Assert.Fail("MainScene should contain " + objectName + ".");
        return null;
    }

    private static Component FindSceneComponent(Scene scene, string typeName)
    {
        Type componentType = Type.GetType(typeName + ", Assembly-CSharp");
        Assert.That(componentType, Is.Not.Null);
        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
        {
            Component component = roots[index].GetComponentInChildren(componentType, true);
            if (component != null)
                return component;
        }

        Assert.Fail("MainScene should contain " + typeName + ".");
        return null;
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, fieldName);
        return field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    private static MethodInfo GetClampMethod()
    {
        return GetClampMethod(7);
    }

    private static MethodInfo GetClampMethod(int parameterCount)
    {
        Type bridgeType = Type.GetType("JsbsimBridge, Assembly-CSharp");
        Assert.That(bridgeType, Is.Not.Null, "JsbsimBridge should compile into Assembly-CSharp.");

        MethodInfo method = null;
        MethodInfo[] methods = bridgeType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static);
        for (int index = 0; index < methods.Length; index++)
        {
            MethodInfo candidate = methods[index];
            if (candidate.Name == "ClampPositionAboveGround" &&
                candidate.GetParameters().Length == parameterCount)
            {
                method = candidate;
                break;
            }
        }

        Assert.That(method, Is.Not.Null, "JsbsimBridge should expose a private static ClampPositionAboveGround helper for ground clipping protection.");
        return method;
    }
}
