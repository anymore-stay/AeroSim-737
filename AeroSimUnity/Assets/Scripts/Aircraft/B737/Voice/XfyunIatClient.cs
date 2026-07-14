using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed class XfyunIatCredentials
{
    private const string AppIdVariable = "XFYUN_APP_ID";
    private const string ApiKeyVariable = "XFYUN_API_KEY";
    private const string ApiSecretVariable = "XFYUN_API_SECRET";

    private XfyunIatCredentials(string appId, string apiKey, string apiSecret)
    {
        AppId = appId;
        ApiKey = apiKey;
        ApiSecret = apiSecret;
    }

    public string AppId { get; }
    public string ApiKey { get; }
    public string ApiSecret { get; }

    public static bool TryLoadFromEnvironment(out XfyunIatCredentials credentials, out string error)
    {
        string appId = Environment.GetEnvironmentVariable(AppIdVariable);
        string apiKey = Environment.GetEnvironmentVariable(ApiKeyVariable);
        string apiSecret = Environment.GetEnvironmentVariable(ApiSecretVariable);

        if (string.IsNullOrWhiteSpace(appId) ||
            string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(apiSecret))
        {
            credentials = null;
            error = "缺少讯飞环境变量：XFYUN_APP_ID、XFYUN_API_KEY、XFYUN_API_SECRET";
            return false;
        }

        credentials = new XfyunIatCredentials(appId.Trim(), apiKey.Trim(), apiSecret.Trim());
        error = string.Empty;
        return true;
    }
}

public sealed class XfyunIatClient : IDisposable
{
    private const string Host = "iat-api.xfyun.cn";
    private const string Path = "/v2/iat";
    private const int AudioFrameIntervalMilliseconds = 40;

    private readonly XfyunIatCredentials credentials;
    private readonly ClientWebSocket socket = new ClientWebSocket();
    private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
    private readonly ConcurrentQueue<byte[]> audioFrames = new ConcurrentQueue<byte[]>();
    private readonly SemaphoreSlim audioAvailable = new SemaphoreSlim(0);
    private readonly StringBuilder transcript = new StringBuilder();

    private int started;
    private int completionRequested;
    private int notificationSent;
    private bool firstFrameSent;

    public XfyunIatClient(XfyunIatCredentials credentials)
    {
        this.credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    }

    public event Action<string> RecognitionCompleted;
    public event Action<string> RecognitionFailed;

    public async Task StartAsync()
    {
        if (Interlocked.Exchange(ref started, 1) != 0)
        {
            return;
        }

        try
        {
            Uri uri = BuildAuthenticatedUri(credentials.ApiKey, credentials.ApiSecret, DateTime.UtcNow);
            await socket.ConnectAsync(uri, cancellation.Token);
            Task sendTask = SendLoopAsync(cancellation.Token);
            Task receiveTask = ReceiveLoopAsync(cancellation.Token);
            await Task.WhenAll(sendTask, receiveTask);
        }
        catch (OperationCanceledException)
        {
            if (Volatile.Read(ref notificationSent) == 0)
            {
                NotifyFailure("讯飞语音识别已取消");
            }
        }
        catch (Exception exception)
        {
            NotifyFailure("讯飞语音识别连接失败：" + exception.Message);
        }
    }

    public void QueueAudio(byte[] pcm16Data)
    {
        if (pcm16Data == null || pcm16Data.Length == 0 ||
            Volatile.Read(ref completionRequested) != 0)
        {
            return;
        }

        audioFrames.Enqueue(pcm16Data);
        audioAvailable.Release();
    }

    public void CompleteInput()
    {
        if (Interlocked.Exchange(ref completionRequested, 1) == 0)
        {
            audioAvailable.Release();
        }
    }

    public static Uri BuildAuthenticatedUri(string apiKey, string apiSecret, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("APIKey 不能为空", nameof(apiKey));
        if (string.IsNullOrWhiteSpace(apiSecret)) throw new ArgumentException("APISecret 不能为空", nameof(apiSecret));

        string date = utcNow.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture);
        string signatureOrigin = "host: " + Host + "\n" +
                                 "date: " + date + "\n" +
                                 "GET " + Path + " HTTP/1.1";

        byte[] signatureBytes;
        using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret)))
        {
            signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(signatureOrigin));
        }

        string signature = Convert.ToBase64String(signatureBytes);
        string authorizationOrigin = string.Format(
            CultureInfo.InvariantCulture,
            "api_key=\"{0}\", algorithm=\"hmac-sha256\", headers=\"host date request-line\", signature=\"{1}\"",
            apiKey,
            signature);
        string authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes(authorizationOrigin));

        string url = "wss://" + Host + Path +
                     "?authorization=" + Uri.EscapeDataString(authorization) +
                     "&date=" + Uri.EscapeDataString(date) +
                     "&host=" + Uri.EscapeDataString(Host);
        return new Uri(url);
    }

    private async Task SendLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await audioAvailable.WaitAsync(token);

            while (audioFrames.TryDequeue(out byte[] audio))
            {
                await SendAudioFrameAsync(audio, firstFrameSent ? 1 : 0, token);
                firstFrameSent = true;
                await Task.Delay(AudioFrameIntervalMilliseconds, token);
            }

            if (Volatile.Read(ref completionRequested) != 0 && audioFrames.IsEmpty)
            {
                if (!firstFrameSent)
                {
                    await SendAudioFrameAsync(Array.Empty<byte>(), 0, token);
                    firstFrameSent = true;
                }

                await SendAudioFrameAsync(Array.Empty<byte>(), 2, token);
                return;
            }
        }
    }

    private async Task SendAudioFrameAsync(byte[] audio, int status, CancellationToken token)
    {
        string json;
        if (status == 0)
        {
            XfyunFirstFrame frame = new XfyunFirstFrame
            {
                common = new XfyunCommon { app_id = credentials.AppId },
                business = new XfyunBusiness(),
                data = CreateData(audio, status)
            };
            json = JsonUtility.ToJson(frame);
        }
        else
        {
            json = JsonUtility.ToJson(new XfyunAudioFrame { data = CreateData(audio, status) });
        }

        byte[] payload = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(
            new ArraySegment<byte>(payload),
            WebSocketMessageType.Text,
            true,
            token);
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        byte[] buffer = new byte[8192];
        while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            using (MemoryStream message = new MemoryStream())
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        if (transcript.Length > 0)
                            NotifySuccess();
                        else
                            NotifyFailure("讯飞连接已关闭，未返回识别文字");
                        return;
                    }

                    message.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                string json = Encoding.UTF8.GetString(message.ToArray());
                XfyunResponse response = JsonUtility.FromJson<XfyunResponse>(json);
                if (response == null)
                {
                    NotifyFailure("无法解析讯飞语音识别响应");
                    return;
                }

                if (response.code != 0)
                {
                    NotifyFailure(string.Format(
                        CultureInfo.InvariantCulture,
                        "讯飞语音识别失败（{0}）：{1}",
                        response.code,
                        response.message));
                    return;
                }

                AppendTranscript(response.data?.result);
                if (response.data != null &&
                    (response.data.status == 2 || (response.data.result != null && response.data.result.ls)))
                {
                    NotifySuccess();
                    return;
                }
            }
        }
    }

    private void AppendTranscript(XfyunResult result)
    {
        if (result?.ws == null)
        {
            return;
        }

        foreach (XfyunWordSegment segment in result.ws)
        {
            if (segment?.cw == null)
            {
                continue;
            }

            foreach (XfyunCandidate candidate in segment.cw)
            {
                if (candidate != null && !string.IsNullOrEmpty(candidate.w))
                {
                    transcript.Append(candidate.w);
                    break;
                }
            }
        }
    }

    private void NotifySuccess()
    {
        if (Interlocked.Exchange(ref notificationSent, 1) != 0)
        {
            return;
        }

        RecognitionCompleted?.Invoke(transcript.ToString());
        cancellation.Cancel();
    }

    private void NotifyFailure(string message)
    {
        if (Interlocked.Exchange(ref notificationSent, 1) != 0)
        {
            return;
        }

        RecognitionFailed?.Invoke(message);
        cancellation.Cancel();
    }

    private static XfyunAudioData CreateData(byte[] audio, int status)
    {
        return new XfyunAudioData
        {
            status = status,
            format = "audio/L16;rate=16000",
            encoding = "raw",
            audio = Convert.ToBase64String(audio ?? Array.Empty<byte>())
        };
    }

    public void Dispose()
    {
        cancellation.Cancel();
        try { socket.Abort(); } catch { }
        socket.Dispose();
        cancellation.Dispose();
        audioAvailable.Dispose();
    }

    [Serializable]
    private sealed class XfyunFirstFrame
    {
        public XfyunCommon common;
        public XfyunBusiness business;
        public XfyunAudioData data;
    }

    [Serializable]
    private sealed class XfyunAudioFrame
    {
        public XfyunAudioData data;
    }

    [Serializable]
    private sealed class XfyunCommon
    {
        public string app_id;
    }

    [Serializable]
    private sealed class XfyunBusiness
    {
        public string language = "zh_cn";
        public string domain = "iat";
        public string accent = "mandarin";
        public int vad_eos = 1500;
        public int ptt = 1;
    }

    [Serializable]
    private sealed class XfyunAudioData
    {
        public int status;
        public string format;
        public string encoding;
        public string audio;
    }

    [Serializable]
    private sealed class XfyunResponse
    {
        public int code;
        public string message;
        public XfyunResponseData data;
    }

    [Serializable]
    private sealed class XfyunResponseData
    {
        public int status;
        public XfyunResult result;
    }

    [Serializable]
    private sealed class XfyunResult
    {
        public bool ls;
        public XfyunWordSegment[] ws;
    }

    [Serializable]
    private sealed class XfyunWordSegment
    {
        public XfyunCandidate[] cw;
    }

    [Serializable]
    private sealed class XfyunCandidate
    {
        public string w;
    }
}
