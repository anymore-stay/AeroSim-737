using NUnit.Framework;
using UnityEngine;

public class StandbyDemoPixelPerfectCameraTests
{
    [Test]
    public void CameraSizeIsCorrectedImmediatelyBeforeRendering()
    {
        GameObject cameraObject = new GameObject(
            "Standby像素相机测试",
            typeof(Camera),
            typeof(StandbyDemoPixelPerfectCamera));
        RenderTexture targetTexture = new RenderTexture(320, 600, 0);

        try
        {
            Camera targetCamera = cameraObject.GetComponent<Camera>();
            targetCamera.targetTexture = targetTexture;
            targetCamera.orthographicSize = 1f;

            cameraObject.SendMessage("OnPreCull", SendMessageOptions.DontRequireReceiver);

            Assert.That(targetCamera.orthographicSize, Is.EqualTo(300f).Within(0.001f));
        }
        finally
        {
            cameraObject.GetComponent<Camera>().targetTexture = null;
            targetTexture.Release();
            Object.DestroyImmediate(targetTexture);
            Object.DestroyImmediate(cameraObject);
        }
    }
}
