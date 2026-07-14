using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(FlightInput))]
public sealed class B737VoiceCommandController : MonoBehaviour
{
    private const int SampleRate = 16000;
    private const int AudioChunkBytes = 1280;

    [Header("按键说话")]
    [SerializeField] private KeyCode pushToTalkKey = KeyCode.Y;
    [SerializeField, Min(1f)] private float maximumRecordingSeconds = 30f;
    [SerializeField] private string microphoneDeviceName;

    [Header("引用")]
    [SerializeField] private FlightInput flightInput;

    private readonly ConcurrentQueue<ClientEvent> clientEvents = new ConcurrentQueue<ClientEvent>();
    private readonly List<byte> pendingPcm = new List<byte>(AudioChunkBytes * 2);

    private XfyunIatClient iatClient;
    private Task iatTask;
    private AudioClip microphoneClip;
    private int microphoneReadPosition;
    private int sessionId;
    private bool recording;
    private bool processing;
    private float recordingStartedAt;
    private string statusText = string.Empty;
    private float statusVisibleUntil;
    private GUIStyle statusStyle;

    private struct ClientEvent
    {
        public int SessionId;
        public bool Failed;
        public string Text;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachToAircraft()
    {
        FlightInput[] inputs = FindObjectsOfType<FlightInput>(true);
        foreach (FlightInput input in inputs)
        {
            if (input != null && input.GetComponent<B737VoiceCommandController>() == null)
            {
                input.gameObject.AddComponent<B737VoiceCommandController>();
            }
        }
    }

    private void Awake()
    {
        if (flightInput == null)
        {
            flightInput = GetComponent<FlightInput>();
        }
    }

    private void Update()
    {
        ProcessClientEvents();

        if (!recording && !processing && Input.GetKeyDown(pushToTalkKey))
        {
            BeginPushToTalk();
        }

        if (!recording)
        {
            return;
        }

        CaptureMicrophoneData();
        if (Input.GetKeyUp(pushToTalkKey) ||
            Time.realtimeSinceStartup - recordingStartedAt >= maximumRecordingSeconds)
        {
            EndPushToTalk();
        }
    }

    private void BeginPushToTalk()
    {
        if (!XfyunIatCredentials.TryLoadFromEnvironment(out XfyunIatCredentials credentials, out string error))
        {
            SetStatus(error + "，设置后请重启 Unity", 8f);
            Debug.LogError("[语音控制] " + error, this);
            return;
        }

        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            SetStatus("没有检测到可用麦克风", 6f);
            Debug.LogError("[语音控制] 没有检测到可用麦克风。", this);
            return;
        }

        string device = string.IsNullOrWhiteSpace(microphoneDeviceName) ? null : microphoneDeviceName;
        try
        {
            microphoneClip = Microphone.Start(device, true, 10, SampleRate);
        }
        catch (Exception exception)
        {
            SetStatus("麦克风启动失败：" + exception.Message, 8f);
            Debug.LogError("[语音控制] 麦克风启动失败：" + exception.Message, this);
            return;
        }

        if (microphoneClip == null)
        {
            SetStatus("麦克风启动失败", 6f);
            return;
        }

        int currentSession = ++sessionId;
        iatClient = new XfyunIatClient(credentials);
        iatClient.RecognitionCompleted += text => clientEvents.Enqueue(new ClientEvent
        {
            SessionId = currentSession,
            Failed = false,
            Text = text
        });
        iatClient.RecognitionFailed += message => clientEvents.Enqueue(new ClientEvent
        {
            SessionId = currentSession,
            Failed = true,
            Text = message
        });
        iatTask = iatClient.StartAsync();

        pendingPcm.Clear();
        microphoneReadPosition = 0;
        recordingStartedAt = Time.realtimeSinceStartup;
        recording = true;
        processing = false;
        SetStatus("正在聆听...", float.PositiveInfinity);
        Debug.Log("[语音控制] 开始录音。", this);
    }

    private void EndPushToTalk()
    {
        if (!recording)
        {
            return;
        }

        CaptureMicrophoneData();
        recording = false;
        StopMicrophone();
        FlushPcm(true);
        processing = true;
        iatClient?.CompleteInput();
        SetStatus("正在识别...", float.PositiveInfinity);
        Debug.Log("[语音控制] 录音结束，等待讯飞返回结果。", this);
    }

    private void CaptureMicrophoneData()
    {
        if (microphoneClip == null)
        {
            return;
        }

        string device = string.IsNullOrWhiteSpace(microphoneDeviceName) ? null : microphoneDeviceName;
        int currentPosition = Microphone.GetPosition(device);
        if (currentPosition < 0 || currentPosition == microphoneReadPosition)
        {
            return;
        }

        int frameCount = currentPosition > microphoneReadPosition
            ? currentPosition - microphoneReadPosition
            : microphoneClip.samples - microphoneReadPosition + currentPosition;
        if (frameCount <= 0)
        {
            return;
        }

        int channels = Mathf.Max(1, microphoneClip.channels);
        float[] samples = ReadClipFrames(microphoneReadPosition, frameCount, channels);
        microphoneReadPosition = currentPosition;

        for (int frame = 0; frame < frameCount; frame++)
        {
            float mono = 0f;
            int sampleIndex = frame * channels;
            for (int channel = 0; channel < channels; channel++)
            {
                mono += samples[sampleIndex + channel];
            }

            mono /= channels;
            short pcm = (short)Mathf.RoundToInt(Mathf.Clamp(mono, -1f, 1f) * short.MaxValue);
            pendingPcm.Add((byte)(pcm & 0xff));
            pendingPcm.Add((byte)((pcm >> 8) & 0xff));
        }

        FlushPcm(false);
    }

    private float[] ReadClipFrames(int startFrame, int frameCount, int channels)
    {
        float[] samples = new float[frameCount * channels];
        int firstFrameCount = Mathf.Min(frameCount, microphoneClip.samples - startFrame);
        float[] first = new float[firstFrameCount * channels];
        microphoneClip.GetData(first, startFrame);
        Array.Copy(first, 0, samples, 0, first.Length);

        int remainingFrames = frameCount - firstFrameCount;
        if (remainingFrames > 0)
        {
            float[] second = new float[remainingFrames * channels];
            microphoneClip.GetData(second, 0);
            Array.Copy(second, 0, samples, first.Length, second.Length);
        }

        return samples;
    }

    private void FlushPcm(bool includeRemainder)
    {
        while (pendingPcm.Count >= AudioChunkBytes)
        {
            QueuePcmChunk(AudioChunkBytes);
        }

        if (includeRemainder && pendingPcm.Count > 0)
        {
            QueuePcmChunk(pendingPcm.Count);
        }
    }

    private void QueuePcmChunk(int byteCount)
    {
        byte[] chunk = pendingPcm.GetRange(0, byteCount).ToArray();
        pendingPcm.RemoveRange(0, byteCount);
        iatClient?.QueueAudio(chunk);
    }

    private void ProcessClientEvents()
    {
        while (clientEvents.TryDequeue(out ClientEvent clientEvent))
        {
            if (clientEvent.SessionId != sessionId)
            {
                continue;
            }

            recording = false;
            processing = false;
            StopMicrophone();

            if (clientEvent.Failed)
            {
                SetStatus(clientEvent.Text, 8f);
                Debug.LogError("[语音控制] " + clientEvent.Text, this);
            }
            else
            {
                HandleTranscript(clientEvent.Text);
            }

            ReleaseCompletedClient();
        }
    }

    private void HandleTranscript(string transcript)
    {
        Debug.Log("[语音控制] 识别结果：" + transcript, this);
        if (!B737VoiceCommandParser.TryParse(transcript, out B737VoiceCommand command, out string message))
        {
            SetStatus("识别：" + transcript + "\n未执行：" + message, 8f);
            return;
        }

        if (TryExecute(command, out string rejectionReason))
        {
            SetStatus("识别：" + transcript + "\n已执行：" + message, 8f);
            Debug.Log("[语音控制] 已执行：" + message, this);
        }
        else
        {
            SetStatus("识别：" + transcript + "\n未执行：" + rejectionReason, 8f);
            Debug.LogWarning("[语音控制] 指令被拒绝：" + rejectionReason, this);
        }
    }

    private bool TryExecute(B737VoiceCommand command, out string rejectionReason)
    {
        rejectionReason = string.Empty;
        if (flightInput == null)
        {
            rejectionReason = "没有找到 FlightInput";
            return false;
        }

        switch (command.Type)
        {
            case B737VoiceCommandType.SetThrottle:
                flightInput.SetThrottle(command.Value);
                return true;
            case B737VoiceCommandType.SetFlapStep:
                flightInput.SetFlapStep(float.IsPositiveInfinity(command.Value) ? flightInput.FlapStepCount : Mathf.RoundToInt(command.Value));
                return true;
            case B737VoiceCommandType.ChangeFlapStep:
                flightInput.SetFlapStep(flightInput.FlapStep + Mathf.RoundToInt(command.Value));
                return true;
            case B737VoiceCommandType.SetSpoilerStep:
                flightInput.SetSpoilerStep(float.IsPositiveInfinity(command.Value) ? flightInput.SpoilerStepCount : Mathf.RoundToInt(command.Value));
                return true;
            case B737VoiceCommandType.ChangeSpoilerStep:
                flightInput.SetSpoilerStep(flightInput.SpoilerStep + Mathf.RoundToInt(command.Value));
                return true;
            case B737VoiceCommandType.SetGearDown:
                return flightInput.TrySetGearDown(command.Value > 0.5f, out rejectionReason);
            case B737VoiceCommandType.SetBrakes:
                if (command.Value > 0.5f && flightInput.Throttle > 0.15f)
                {
                    rejectionReason = "当前油门高于百分之十五，拒绝锁定刹车";
                    return false;
                }
                flightInput.SetBrakes(command.Value > 0.5f);
                return true;
            case B737VoiceCommandType.AdjustPitchTrim:
                flightInput.AdjustPitchTrim(command.Value);
                return true;
            case B737VoiceCommandType.SetPaused:
                flightInput.SetPaused(command.Value > 0.5f);
                return true;
            default:
                rejectionReason = "不支持该语音指令";
                return false;
        }
    }

    private void StopMicrophone()
    {
        if (microphoneClip == null)
        {
            return;
        }

        string device = string.IsNullOrWhiteSpace(microphoneDeviceName) ? null : microphoneDeviceName;
        try { Microphone.End(device); } catch { }
        microphoneClip = null;
    }

    private void ReleaseCompletedClient()
    {
        XfyunIatClient completedClient = iatClient;
        Task completedTask = iatTask;
        iatClient = null;
        iatTask = null;

        if (completedClient == null)
        {
            return;
        }

        if (completedTask == null || completedTask.IsCompleted)
        {
            completedClient.Dispose();
        }
        else
        {
            completedTask.ContinueWith(_ => completedClient.Dispose());
        }
    }

    private void SetStatus(string text, float durationSeconds)
    {
        statusText = text;
        statusVisibleUntil = float.IsPositiveInfinity(durationSeconds)
            ? float.PositiveInfinity
            : Time.realtimeSinceStartup + Mathf.Max(0f, durationSeconds);
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(statusText) || Time.realtimeSinceStartup > statusVisibleUntil)
        {
            return;
        }

        if (statusStyle == null)
        {
            statusStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
        }

        float width = Mathf.Max(120f, Mathf.Min(620f, Screen.width - 24f));
        GUI.Box(new Rect((Screen.width - width) * 0.5f, 12f, width, 84f), statusText, statusStyle);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && recording)
        {
            EndPushToTalk();
        }
    }

    private void OnDestroy()
    {
        recording = false;
        processing = false;
        StopMicrophone();
        iatClient?.Dispose();
        iatClient = null;
        iatTask = null;
    }
}
