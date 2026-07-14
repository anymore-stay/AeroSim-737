using NUnit.Framework;
using UnityEngine;

public class CameraManagerTests
{
    [TestCase(7)]
    [TestCase(8)]
    [TestCase(9)]
    public void SwitchingCameraEnablesOnlySelectedSlot(int selectedHotkey)
    {
        GameObject aircraft = new GameObject("Aircraft");

        try
        {
            CameraManager manager = aircraft.AddComponent<CameraManager>();
            Camera[] cameras = new Camera[3];
            int[] hotkeys = { 7, 8, 9 };
            CockpitCameraController.CameraMode[] modes =
            {
                CockpitCameraController.CameraMode.Cabin,
                CockpitCameraController.CameraMode.Cockpit,
                CockpitCameraController.CameraMode.ThirdPerson
            };

            for (int i = 0; i < cameras.Length; i++)
            {
                GameObject cameraObject = new GameObject("Camera" + hotkeys[i], typeof(Camera));
                cameraObject.transform.SetParent(aircraft.transform, false);
                CockpitCameraController controller = cameraObject.AddComponent<CockpitCameraController>();
                controller.cameraMode = modes[i];
                cameras[i] = cameraObject.GetComponent<Camera>();
                manager.RegisterCamera(hotkeys[i], cameraObject, cameraObject.name);
                controller.SetActive(true);
            }

            manager.SwitchTo(selectedHotkey);

            for (int i = 0; i < cameras.Length; i++)
            {
                Assert.That(
                    cameras[i].enabled,
                    Is.EqualTo(hotkeys[i] == selectedHotkey),
                    "Camera " + hotkeys[i] + " active state is incorrect.");
            }

            Assert.That(manager.ActiveCamera, Is.EqualTo(cameras[System.Array.IndexOf(hotkeys, selectedHotkey)]));
        }
        finally
        {
            Object.DestroyImmediate(aircraft);
        }
    }
}
