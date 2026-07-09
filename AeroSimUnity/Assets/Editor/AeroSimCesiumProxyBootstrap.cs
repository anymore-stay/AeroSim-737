using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AeroSim.Editor
{
    [InitializeOnLoad]
    internal static class AeroSimCesiumProxyBootstrap
    {
        private const string ProxyOverrideVariable = "AEROSIM_PROXY";
        private const string DefaultProxy = "http://127.0.0.1:7897";
        private const int ProbeTimeoutMilliseconds = 200;

        static AeroSimCesiumProxyBootstrap()
        {
            ApplyProxy(false);
            EditorApplication.delayCall += ApplyProxyDelayed;
        }

        private static void ApplyProxyDelayed()
        {
            if (ApplyProxy(false))
            {
                RecreateLoadedCesiumTilesets();
            }
        }

        [MenuItem("AeroSim/Cesium/Apply Network Proxy")]
        private static void ApplyProxyFromMenu()
        {
            if (ApplyProxy(true))
            {
                RecreateLoadedCesiumTilesets(true);
            }
        }

        private static bool ApplyProxy(bool logResult)
        {
            string proxy = Environment.GetEnvironmentVariable(ProxyOverrideVariable, EnvironmentVariableTarget.Process);
            if (string.IsNullOrWhiteSpace(proxy))
            {
                proxy = Environment.GetEnvironmentVariable(ProxyOverrideVariable, EnvironmentVariableTarget.User);
            }
            if (string.IsNullOrWhiteSpace(proxy))
            {
                proxy = Environment.GetEnvironmentVariable(ProxyOverrideVariable, EnvironmentVariableTarget.Machine);
            }
            if (string.IsNullOrWhiteSpace(proxy))
            {
                proxy = DefaultProxy;
            }

            if (!Uri.TryCreate(proxy, UriKind.Absolute, out Uri proxyUri))
            {
                if (logResult)
                {
                    Debug.LogWarning($"[AeroSim] Ignoring invalid {ProxyOverrideVariable}: {proxy}");
                }
                return false;
            }

            if (!HasExplicitProxyOverride() && IsLoopback(proxyUri) && !CanConnect(proxyUri))
            {
                if (logResult)
                {
                    Debug.LogWarning($"[AeroSim] Local proxy is not reachable: {proxy}");
                }
                return false;
            }

            SetProcessEnvironmentVariable("HTTP_PROXY", proxy);
            SetProcessEnvironmentVariable("HTTPS_PROXY", proxy);
            SetProcessEnvironmentVariable("ALL_PROXY", proxy);
            SetProcessEnvironmentVariable("http_proxy", proxy);
            SetProcessEnvironmentVariable("https_proxy", proxy);
            SetProcessEnvironmentVariable("all_proxy", proxy);

            const string noProxy = "localhost,127.0.0.1,::1";
            SetProcessEnvironmentVariable("NO_PROXY", noProxy);
            SetProcessEnvironmentVariable("no_proxy", noProxy);

            if (logResult)
            {
                Debug.Log($"[AeroSim] Applied Cesium network proxy: {proxy}");
            }

            return true;
        }

        private static bool HasExplicitProxyOverride()
        {
            return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ProxyOverrideVariable, EnvironmentVariableTarget.Process))
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ProxyOverrideVariable, EnvironmentVariableTarget.User))
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ProxyOverrideVariable, EnvironmentVariableTarget.Machine));
        }

        private static void SetProcessEnvironmentVariable(string key, string value)
        {
            Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.Process);
        }

        private static bool IsLoopback(Uri uri)
        {
            return uri.IsLoopback
                || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                || IPAddress.TryParse(uri.Host, out IPAddress address) && IPAddress.IsLoopback(address);
        }

        private static bool CanConnect(Uri uri)
        {
            int port = uri.Port > 0 ? uri.Port : uri.Scheme == "https" ? 443 : 80;

            try
            {
                using TcpClient client = new TcpClient();
                IAsyncResult result = client.BeginConnect(uri.Host, port, null, null);
                bool connected = result.AsyncWaitHandle.WaitOne(ProbeTimeoutMilliseconds);
                if (!connected)
                {
                    return false;
                }

                client.EndConnect(result);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void RecreateLoadedCesiumTilesets(bool logResult = false)
        {
            Type tilesetType = FindType("CesiumForUnity.Cesium3DTileset");
            if (tilesetType == null)
            {
                return;
            }

            MethodInfo recreateMethod = tilesetType.GetMethod("RecreateTileset", BindingFlags.Instance | BindingFlags.Public);
            if (recreateMethod == null)
            {
                return;
            }

            int refreshedCount = 0;
            foreach (UnityEngine.Object candidate in Resources.FindObjectsOfTypeAll(tilesetType))
            {
                Component component = candidate as Component;
                if (component == null || EditorUtility.IsPersistent(component))
                {
                    continue;
                }

                try
                {
                    recreateMethod.Invoke(candidate, null);
                    refreshedCount++;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AeroSim] Failed to refresh Cesium tileset '{component.name}': {ex.Message}");
                }
            }

            if (logResult)
            {
                Debug.Log($"[AeroSim] Refreshed {refreshedCount} Cesium tileset(s).");
            }
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
