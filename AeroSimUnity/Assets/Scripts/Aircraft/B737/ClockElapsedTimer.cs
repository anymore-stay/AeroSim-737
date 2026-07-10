using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ClockElapsedTimer : MonoBehaviour
{
    private const int AtlasColumns = 16;
    private const int AtlasRows = 6;
    private const int MaximumDisplayMinutes = 9 * 60 + 59;

    [SerializeField] private RawImage minutesImage;
    [SerializeField] private RawImage separatorImage;
    [SerializeField] private RawImage tensSecondsImage;
    [SerializeField] private RawImage onesSecondsImage;
    [SerializeField] private RawImage stateImage;

    private double startedAt;
    private int lastDisplayedSecond = -1;

    public int ElapsedSeconds { get; private set; }

    private void Awake()
    {
        AutoBind();
    }

    private void OnEnable()
    {
        if (!AutoBind())
        {
            enabled = false;
            return;
        }

        startedAt = Time.realtimeSinceStartupAsDouble;
        lastDisplayedSecond = -1;
        stateImage.uvRect = GetStateUvRect(true);
        RefreshDisplay(GetElapsedSeconds(Time.realtimeSinceStartupAsDouble, startedAt));
    }

    private void Update()
    {
        int elapsedSeconds = GetElapsedSeconds(Time.realtimeSinceStartupAsDouble, startedAt);

        if (elapsedSeconds != lastDisplayedSecond)
        {
            RefreshDisplay(elapsedSeconds);
        }
    }

    private void OnDisable()
    {
        if (stateImage != null)
        {
            stateImage.uvRect = GetStateUvRect(false);
        }
    }

    public static int[] GetDisplayDigits(int elapsedSeconds)
    {
        int totalMinutes = Mathf.Clamp(
            Mathf.Max(0, elapsedSeconds) / 60,
            0,
            MaximumDisplayMinutes);
        int hours = totalMinutes / 60;
        int minutes = totalMinutes % 60;

        return new[]
        {
            hours,
            minutes / 10,
            minutes % 10
        };
    }

    public static int GetElapsedSeconds(double realtimeNow, double playStartedAt)
    {
        double elapsedSeconds = realtimeNow - playStartedAt;

        if (double.IsNaN(elapsedSeconds) || elapsedSeconds <= 0d)
        {
            return 0;
        }

        return elapsedSeconds >= int.MaxValue
            ? int.MaxValue
            : (int)elapsedSeconds;
    }

    public static Rect GetDigitUvRect(int digit, int row)
    {
        int clampedDigit = Mathf.Clamp(digit, 0, 9);
        int clampedRow = Mathf.Clamp(row, 0, AtlasRows - 1);

        return new Rect(
            clampedDigit / (float)AtlasColumns,
            (AtlasRows - 1 - clampedRow) / (float)AtlasRows,
            1f / AtlasColumns,
            1f / AtlasRows);
    }

    public static Rect GetStateUvRect(bool running)
    {
        return running
            ? new Rect(28f / 52f, 2f / 12f, 22f / 52f, 10f / 12f)
            : new Rect(4f / 52f, 2f / 12f, 19f / 52f, 9f / 12f);
    }

    private void RefreshDisplay(int elapsedSeconds)
    {
        int[] digits = GetDisplayDigits(elapsedSeconds);

        minutesImage.uvRect = GetDigitUvRect(digits[0], 5);
        tensSecondsImage.uvRect = GetDigitUvRect(digits[1], 2);
        onesSecondsImage.uvRect = GetDigitUvRect(digits[2], 3);

        ElapsedSeconds = elapsedSeconds;
        lastDisplayedSecond = elapsedSeconds;
    }

    private bool AutoBind()
    {
        if (minutesImage == null) minutesImage = FindImage("Clock_ET_Hours");
        if (separatorImage == null) separatorImage = FindImage("Clock_ET_Separator");
        if (tensSecondsImage == null) tensSecondsImage = FindImage("Clock_ET_MinutesTens");
        if (onesSecondsImage == null) onesSecondsImage = FindImage("Clock_ET_MinutesOnes");
        if (stateImage == null) stateImage = FindImage("Clock_ET_Hold");

        bool bound = minutesImage != null
            && separatorImage != null
            && tensSecondsImage != null
            && onesSecondsImage != null
            && stateImage != null;

        if (!bound)
        {
            Debug.LogError("ClockElapsedTimer could not bind all Clock ET UI images.", this);
        }

        return bound;
    }

    private RawImage FindImage(string objectName)
    {
        Transform child = transform.Find(objectName);
        return child != null ? child.GetComponent<RawImage>() : null;
    }
}
