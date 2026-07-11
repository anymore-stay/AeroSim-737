using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class B737CockpitControlColumnVisibilityTests
{
    private const string B737PrefabPath = "Assets/Aircraft/B737/Prefabs/B737.prefab";

    [Test]
    public void 控制组件默认显示并仅允许驾驶舱模式切换()
    {
        Type controllerType = Type.GetType("B737CockpitControlColumnVisibility, Assembly-CSharp");
        Assert.That(controllerType, Is.Not.Null, "尚未实现 B737CockpitControlColumnVisibility。");

        GameObject root = new GameObject("操纵杆显隐测试");
        GameObject controlColumn = new GameObject("ImpEmpty.001_x24e_47969");
        GameObject cameraObject = new GameObject("CockpitCamera", typeof(Camera), typeof(CockpitCameraController));
        controlColumn.transform.SetParent(root.transform, false);
        cameraObject.transform.SetParent(root.transform, false);

        try
        {
            Component controller = root.AddComponent(controllerType);
            CockpitCameraController cameraController = cameraObject.GetComponent<CockpitCameraController>();
            MethodInfo setBindings = controllerType.GetMethod("SetBindings", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo tryToggle = controllerType.GetMethod("TryToggle", BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo visible = controllerType.GetProperty("IsControlColumnVisible", BindingFlags.Public | BindingFlags.Instance);

            Assert.That(setBindings, Is.Not.Null);
            Assert.That(tryToggle, Is.Not.Null);
            Assert.That(visible, Is.Not.Null);
            setBindings.Invoke(controller, new object[] { controlColumn, cameraController });

            Assert.That(controlColumn.activeSelf, Is.True, "进入 Play 时操纵杆应默认显示。");
            Assert.That((bool)visible.GetValue(controller), Is.True);

            cameraController.cameraMode = CockpitCameraController.CameraMode.Cabin;
            Assert.That((bool)tryToggle.Invoke(controller, new object[] { true }), Is.False);
            Assert.That(controlColumn.activeSelf, Is.True, "客舱模式不得切换操纵杆。");

            cameraController.cameraMode = CockpitCameraController.CameraMode.Cockpit;
            Assert.That((bool)tryToggle.Invoke(controller, new object[] { true }), Is.True);
            Assert.That(controlColumn.activeSelf, Is.False);
            Assert.That((bool)tryToggle.Invoke(controller, new object[] { true }), Is.True);
            Assert.That(controlColumn.activeSelf, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void B737Prefab绑定目标且Hud包含快捷键说明()
    {
        Type controllerType = Type.GetType("B737CockpitControlColumnVisibility, Assembly-CSharp");
        Assert.That(controllerType, Is.Not.Null, "尚未实现 B737CockpitControlColumnVisibility。");

        B737FmsDisplayRig.SuppressEditorRebuild = true;
        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(B737PrefabPath);
            Component controller = root.GetComponent(controllerType);
            Assert.That(controller, Is.Not.Null, "B737 根节点缺少操纵杆显隐组件。");

            SerializedObject serialized = new SerializedObject(controller);
            GameObject target = serialized.FindProperty("controlColumnTarget").objectReferenceValue as GameObject;
            Assert.That(target, Is.Not.Null, "操纵杆目标没有绑定。");
            Assert.That(target.name, Is.EqualTo("ImpEmpty.001_x24e_47969"));
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            B737FmsDisplayRig.SuppressEditorRebuild = false;
        }

        string hudSource = File.ReadAllText("Assets/Scripts/Aircraft/B737/FlightHud.cs");
        StringAssert.Contains("1 操纵杆显示/隐藏（仅驾驶舱）", hudSource);
    }
}
