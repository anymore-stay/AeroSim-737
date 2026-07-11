using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class B737StandbyDisplayRigTests
{
    [Test]
    public void 正式备用仪表具备独立渲染链路和真实数据驱动()
    {
        RenderTexture texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(
            "Assets/Aircraft/B737/Textures/Standby.renderTexture");
        Assert.That(texture, Is.Not.Null);

        B737FmsDisplayRig.SuppressEditorRebuild = true;
        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents("Assets/Aircraft/B737/Prefabs/B737.prefab");
            Transform rig = FindByName(root.transform, "B737_Standby_Rig");
            Transform display = FindByName(rig, "Standby_Display_Runtime");
            Transform cameraTransform = FindByName(rig, "Standby_Camera");
            Transform plane = FindByName(root.transform, "Standby_Plane");
            Assert.That(rig, Is.Not.Null);
            Assert.That(display, Is.Not.Null);
            Assert.That(cameraTransform, Is.Not.Null);
            Assert.That(plane, Is.Not.Null);

            Camera camera = cameraTransform.GetComponent<Camera>();
            Canvas canvas = display.GetComponentInChildren<Canvas>(true);
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            Assert.That(camera.targetTexture, Is.SameAs(texture));
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(278f, 278f)));
            Assert.That(display.GetComponent<StandbyJsbsimDataDriver>(), Is.Not.Null);
            StandbyDemoDataSource demoData = display.GetComponent<StandbyDemoDataSource>();
            Assert.That(demoData, Is.Null, "正式备用仪表不应携带模拟数据组件。");
            Assert.That(plane.GetComponent<MeshRenderer>().sharedMaterial.mainTexture, Is.SameAs(texture));
            Material standbyMaterial = plane.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(standbyMaterial.GetFloat("_Smoothness"), Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(standbyMaterial.GetFloat("_EnvironmentReflections"), Is.EqualTo(1f));
            Assert.That(standbyMaterial.GetFloat("_SpecularHighlights"), Is.EqualTo(1f));
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            B737FmsDisplayRig.SuppressEditorRebuild = false;
        }
    }

    private static Transform FindByName(Transform root, string name)
    {
        if (root == null || root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindByName(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
