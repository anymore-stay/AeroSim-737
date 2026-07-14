using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class CockpitCameraControllerTests
{
    [Test]
    public void ActivatingCockpitRestoresInitialLocalPose()
    {
        GameObject parent = new GameObject("Aircraft");
        GameObject cameraObject = new GameObject("CockpitCamera", typeof(Camera));

        try
        {
            cameraObject.transform.SetParent(parent.transform, false);
            CockpitCameraController controller = cameraObject.AddComponent<CockpitCameraController>();
            Vector3 initialPosition = new Vector3(1f, 2f, 3f);
            Quaternion initialRotation = Quaternion.Euler(10f, 20f, 0f);
            SetInitialPose(controller, initialPosition, initialRotation);

            cameraObject.transform.localPosition = new Vector3(4f, 5f, 6f);
            cameraObject.transform.localRotation = Quaternion.Euler(30f, 40f, 0f);
            controller.cameraMode = CockpitCameraController.CameraMode.Cockpit;

            controller.SetActive(true);

            Assert.That(cameraObject.transform.localPosition, Is.EqualTo(initialPosition));
            Assert.That(Quaternion.Angle(cameraObject.transform.localRotation, initialRotation), Is.LessThan(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(parent);
        }
    }

    [Test]
    public void ActivatingCabinPreservesCurrentLocalPose()
    {
        GameObject parent = new GameObject("Aircraft");
        GameObject cameraObject = new GameObject("CabinCamera", typeof(Camera));

        try
        {
            cameraObject.transform.SetParent(parent.transform, false);
            CockpitCameraController controller = cameraObject.AddComponent<CockpitCameraController>();
            SetInitialPose(controller, new Vector3(1f, 2f, 3f), Quaternion.Euler(10f, 20f, 0f));
            Vector3 currentPosition = new Vector3(4f, 5f, 6f);
            Quaternion currentRotation = Quaternion.Euler(30f, 40f, 0f);
            cameraObject.transform.localPosition = currentPosition;
            cameraObject.transform.localRotation = currentRotation;
            controller.cameraMode = CockpitCameraController.CameraMode.Cabin;

            controller.SetActive(true);

            Assert.That(cameraObject.transform.localPosition, Is.EqualTo(currentPosition));
            Assert.That(Quaternion.Angle(cameraObject.transform.localRotation, currentRotation), Is.LessThan(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(parent);
        }
    }

    [Test]
    public void AircraftColliderIsIgnoredWhenAircraftIsUnderCesiumGeoreference()
    {
        GameObject georeference = new GameObject("CesiumGeoreference");
        GameObject aircraft = new GameObject("B737");
        GameObject aircraftCollider = new GameObject("NoseCollider");
        GameObject sceneryCollider = new GameObject("AirportCollider");

        try
        {
            aircraft.transform.SetParent(georeference.transform, false);
            aircraftCollider.transform.SetParent(aircraft.transform, false);
            sceneryCollider.transform.SetParent(georeference.transform, false);

            Assert.That(IsPartOfAircraft(aircraft.transform, aircraft.transform), Is.True);
            Assert.That(IsPartOfAircraft(aircraftCollider.transform, aircraft.transform), Is.True);
            Assert.That(IsPartOfAircraft(sceneryCollider.transform, aircraft.transform), Is.False);
            Assert.That(IsPartOfAircraft(georeference.transform, aircraft.transform), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(georeference);
        }
    }

    private static void SetInitialPose(
        CockpitCameraController controller,
        Vector3 position,
        Quaternion rotation)
    {
        SetPrivateField(controller, "startLocalPos", position);
        SetPrivateField(controller, "startLocalRotation", rotation);
    }

    private static void SetPrivateField<T>(CockpitCameraController controller, string fieldName, T value)
    {
        FieldInfo field = typeof(CockpitCameraController).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing field: " + fieldName);
        field.SetValue(controller, value);
    }

    private static bool IsPartOfAircraft(Transform candidate, Transform aircraftRoot)
    {
        MethodInfo method = typeof(CockpitCameraController).GetMethod(
            "IsPartOfAircraft",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "Missing method: IsPartOfAircraft");
        return (bool)method.Invoke(null, new object[] { candidate, aircraftRoot });
    }
}
