using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

internal sealed class JsbsimProcessLauncher : IDisposable
{
    private Process process;

    public void Start(string repositoryRelativeScriptPath)
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (IsProcessRunning())
            return;

        string scriptPath = FindFileInParentDirectories(
            Application.dataPath,
            repositoryRelativeScriptPath);
        if (string.IsNullOrEmpty(scriptPath))
        {
            UnityEngine.Debug.LogError(
                "[JSBSim] 未找到启动脚本。仓库相对路径: " +
                repositoryRelativeScriptPath);
            return;
        }

        string commandInterpreter = Environment.GetEnvironmentVariable("ComSpec");
        if (string.IsNullOrEmpty(commandInterpreter))
            commandInterpreter = "cmd.exe";

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = commandInterpreter,
                Arguments = "/d /s /c \"\"" + scriptPath + "\"\"",
                WorkingDirectory = Path.GetDirectoryName(scriptPath),
                UseShellExecute = false,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            };

            process = Process.Start(startInfo);
            if (process == null)
            {
                UnityEngine.Debug.LogError("[JSBSim] 启动 bat 脚本失败。");
                return;
            }

            UnityEngine.Debug.Log("[JSBSim] 已启动: " + scriptPath);
        }
        catch (Exception exception)
        {
            DisposeProcess();
            UnityEngine.Debug.LogError("[JSBSim] 启动 bat 脚本失败: " + exception.Message);
        }
#else
        UnityEngine.Debug.LogWarning("[JSBSim] bat 自动启动功能仅支持 Windows。");
#endif
    }

    public void Stop()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        Process ownedProcess = process;
        process = null;
        if (ownedProcess == null)
            return;

        try
        {
            if (!ownedProcess.HasExited)
            {
                // bat 会同步启动 JSBSim，必须结束整个进程树，避免退出游戏后残留进程。
                ProcessStartInfo stopInfo = new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = "/PID " + ownedProcess.Id + " /T /F",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process stopProcess = Process.Start(stopInfo))
                {
                    stopProcess?.WaitForExit(5000);
                }

                if (!ownedProcess.HasExited)
                    ownedProcess.Kill();
            }
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogWarning("[JSBSim] 关闭进程时发生错误: " + exception.Message);
        }
        finally
        {
            ownedProcess.Dispose();
        }
#endif
    }

    public void Dispose()
    {
        Stop();
    }

    internal static string FindFileInParentDirectories(
        string startDirectory,
        string relativePath)
    {
        if (string.IsNullOrWhiteSpace(startDirectory) ||
            string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath))
        {
            return null;
        }

        DirectoryInfo directory;
        try
        {
            directory = new DirectoryInfo(startDirectory);
        }
        catch (Exception)
        {
            return null;
        }

        while (directory != null)
        {
            string candidatePath;
            try
            {
                candidatePath = Path.GetFullPath(Path.Combine(directory.FullName, relativePath));
            }
            catch (Exception)
            {
                return null;
            }

            if (File.Exists(candidatePath))
                return candidatePath;

            directory = directory.Parent;
        }

        return null;
    }

    private bool IsProcessRunning()
    {
        if (process == null)
            return false;

        try
        {
            if (!process.HasExited)
                return true;
        }
        catch (InvalidOperationException)
        {
        }

        DisposeProcess();
        return false;
    }

    private void DisposeProcess()
    {
        process?.Dispose();
        process = null;
    }
}
