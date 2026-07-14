using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

public static class B737VoiceCommandParser
{
    private const string SpokenNumberPattern = @"[0-9]+(?:\.[0-9]+)?|[零〇一二两三四五六七八九十百点]+";

    public static bool TryParse(string transcript, out B737VoiceCommand command, out string message)
    {
        command = default;
        string text = Normalize(transcript);
        if (text.Length == 0)
        {
            message = "没有识别到语音";
            return false;
        }

        if (text.Contains("反推"))
        {
            message = "为避免误触发，语音反推暂未开放";
            return false;
        }

        if (text.Contains("油门"))
        {
            if (ContainsAny(text, "怠速", "慢车", "收光"))
            {
                return Success(B737VoiceCommandType.SetThrottle, 0f, "油门设为怠速", out command, out message);
            }

            if (ContainsAny(text, "最大", "全油门", "推满"))
            {
                return Success(B737VoiceCommandType.SetThrottle, 1f, "油门设为百分之一百", out command, out message);
            }

            if (TryReadThrottle(text, out float throttle))
            {
                return Success(
                    B737VoiceCommandType.SetThrottle,
                    throttle,
                    string.Format(CultureInfo.InvariantCulture, "油门设为 {0:P0}", throttle),
                    out command,
                    out message);
            }

            message = "没有听清油门百分比";
            return false;
        }

        if (text.Contains("起落架"))
        {
            if (ContainsAny(text, "放下", "放出", "打开"))
                return Success(B737VoiceCommandType.SetGearDown, 1f, "放下起落架", out command, out message);
            if (ContainsAny(text, "收起", "收上", "收回"))
                return Success(B737VoiceCommandType.SetGearDown, 0f, "收起起落架", out command, out message);
        }

        if (text.Contains("襟翼"))
        {
            if (ContainsAny(text, "全收", "全部收起", "完全收起"))
                return Success(B737VoiceCommandType.SetFlapStep, 0f, "襟翼全部收起", out command, out message);
            if (ContainsAny(text, "全放", "全部放下", "完全放下"))
                return Success(B737VoiceCommandType.SetFlapStep, float.PositiveInfinity, "襟翼全部放下", out command, out message);
            if (ContainsAny(text, "增加", "放下一级", "加一级", "下一档"))
                return Success(B737VoiceCommandType.ChangeFlapStep, 1f, "襟翼增加一级", out command, out message);
            if (ContainsAny(text, "减少", "收回一级", "减一级", "上一档"))
                return Success(B737VoiceCommandType.ChangeFlapStep, -1f, "襟翼收回一级", out command, out message);
        }

        if (text.Contains("扰流板") || text.Contains("减速板"))
        {
            if (ContainsAny(text, "全收", "收起", "关闭"))
                return Success(B737VoiceCommandType.SetSpoilerStep, 0f, "扰流板收起", out command, out message);
            if (ContainsAny(text, "全开", "全部打开", "完全打开"))
                return Success(B737VoiceCommandType.SetSpoilerStep, float.PositiveInfinity, "扰流板全部打开", out command, out message);
            if (ContainsAny(text, "增加", "打开一级", "加一级"))
                return Success(B737VoiceCommandType.ChangeSpoilerStep, 1f, "扰流板增加一级", out command, out message);
            if (ContainsAny(text, "减少", "收回一级", "减一级"))
                return Success(B737VoiceCommandType.ChangeSpoilerStep, -1f, "扰流板收回一级", out command, out message);
        }

        if (text.Contains("刹车") || text.Contains("制动"))
        {
            if (ContainsAny(text, "解除", "松开", "释放", "关闭"))
                return Success(B737VoiceCommandType.SetBrakes, 0f, "解除刹车", out command, out message);
            if (ContainsAny(text, "开启", "打开", "锁定", "刹住"))
                return Success(B737VoiceCommandType.SetBrakes, 1f, "开启刹车", out command, out message);
        }

        if (text.Contains("配平"))
        {
            if (ContainsAny(text, "抬头", "机头向上", "向上"))
                return Success(B737VoiceCommandType.AdjustPitchTrim, 0.05f, "抬头配平增加一级", out command, out message);
            if (ContainsAny(text, "低头", "机头向下", "向下"))
                return Success(B737VoiceCommandType.AdjustPitchTrim, -0.05f, "低头配平增加一级", out command, out message);
        }

        if (ContainsAny(text, "继续飞行", "继续仿真", "解除暂停"))
            return Success(B737VoiceCommandType.SetPaused, 0f, "继续飞行", out command, out message);
        if (ContainsAny(text, "暂停飞行", "暂停仿真"))
            return Success(B737VoiceCommandType.SetPaused, 1f, "暂停飞行", out command, out message);

        message = "未匹配到可执行的飞行指令";
        return false;
    }

    private static bool Success(
        B737VoiceCommandType type,
        float value,
        string successMessage,
        out B737VoiceCommand command,
        out string message)
    {
        command = new B737VoiceCommand(type, value);
        message = successMessage;
        return true;
    }

    private static bool TryReadThrottle(string text, out float throttle)
    {
        Match percentage = Regex.Match(text, @"百分之(" + SpokenNumberPattern + ")");
        if (!percentage.Success)
        {
            percentage = Regex.Match(text, @"油门(?:调到|设置到|设为|到)?(" + SpokenNumberPattern + @")(?:%|％)");
        }

        if (percentage.Success && TryParseSpokenNumber(percentage.Groups[1].Value, out float percent))
        {
            throttle = Clamp01(percent / 100f);
            return percent >= 0f && percent <= 100f;
        }

        Match tenths = Regex.Match(text, @"油门(?:调到|设置到|设为|到)?(" + SpokenNumberPattern + ")成");
        if (tenths.Success && TryParseSpokenNumber(tenths.Groups[1].Value, out float tenth))
        {
            throttle = Clamp01(tenth / 10f);
            return tenth >= 0f && tenth <= 10f;
        }

        throttle = 0f;
        return false;
    }

    private static bool TryParseSpokenNumber(string text, out float value)
    {
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        string[] parts = text.Split('点');
        if (!TryParseChineseInteger(parts[0], out int integer))
        {
            value = 0f;
            return false;
        }

        value = integer;
        if (parts.Length == 1)
        {
            return true;
        }

        if (parts.Length != 2 || parts[1].Length == 0)
        {
            return false;
        }

        float place = 0.1f;
        foreach (char character in parts[1])
        {
            int digit = ChineseDigit(character);
            if (digit < 0)
            {
                return false;
            }

            value += digit * place;
            place *= 0.1f;
        }

        return true;
    }

    private static bool TryParseChineseInteger(string text, out int value)
    {
        value = 0;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (text.IndexOf('十') < 0 && text.IndexOf('百') < 0)
        {
            foreach (char character in text)
            {
                int digit = ChineseDigit(character);
                if (digit < 0)
                {
                    return false;
                }

                value = value * 10 + digit;
            }

            return true;
        }

        int pendingDigit = 0;
        foreach (char character in text)
        {
            int digit = ChineseDigit(character);
            if (digit >= 0)
            {
                pendingDigit = digit;
                continue;
            }

            int unit = character == '十' ? 10 : character == '百' ? 100 : 0;
            if (unit == 0)
            {
                return false;
            }

            value += (pendingDigit == 0 ? 1 : pendingDigit) * unit;
            pendingDigit = 0;
        }

        value += pendingDigit;
        return true;
    }

    private static int ChineseDigit(char character)
    {
        switch (character)
        {
            case '零':
            case '〇': return 0;
            case '一': return 1;
            case '二':
            case '两': return 2;
            case '三': return 3;
            case '四': return 4;
            case '五': return 5;
            case '六': return 6;
            case '七': return 7;
            case '八': return 8;
            case '九': return 9;
            default: return -1;
        }
    }

    private static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(text.Length);
        foreach (char character in text)
        {
            if (!char.IsWhiteSpace(character) &&
                character != '，' && character != ',' &&
                character != '。' && character != '！' && character != '!' &&
                character != '？' && character != '?')
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static bool ContainsAny(string text, params string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            if (text.Contains(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static float Clamp01(float value)
    {
        return Math.Max(0f, Math.Min(1f, value));
    }
}
