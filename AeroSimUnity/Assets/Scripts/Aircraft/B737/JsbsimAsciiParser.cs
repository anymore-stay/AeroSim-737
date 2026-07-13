using System;

public static class JsbsimAsciiParser
{
    public static int ParseValues(
        byte[] buffer,
        int start,
        int length,
        float[] values,
        bool[] valid)
    {
        if (buffer == null || values == null || valid == null)
        {
            return 0;
        }

        int end = Math.Min(buffer.Length, start + Math.Max(0, length));
        int fieldIndex = 0;
        int tokenStart = Math.Max(0, start);

        for (int i = tokenStart; i <= end; i++)
        {
            if (i < end && buffer[i] != (byte)',')
            {
                continue;
            }

            if (fieldIndex < values.Length && fieldIndex < valid.Length)
            {
                valid[fieldIndex] = TryParseFloat(
                    buffer,
                    tokenStart,
                    i - tokenStart,
                    out values[fieldIndex]);
            }

            fieldIndex++;
            tokenStart = i + 1;
        }

        return fieldIndex;
    }

    public static bool TryParseFloat(
        byte[] buffer,
        int start,
        int length,
        out float result)
    {
        result = 0f;
        if (buffer == null || length <= 0 || start < 0 || start >= buffer.Length)
        {
            return false;
        }

        int index = start;
        int end = Math.Min(buffer.Length, start + length);
        SkipWhitespace(buffer, ref index, end);
        if (index >= end)
        {
            return false;
        }

        bool negative = false;
        if (buffer[index] == (byte)'-' || buffer[index] == (byte)'+')
        {
            negative = buffer[index] == (byte)'-';
            index++;
        }

        if (MatchesAsciiIgnoreCase(buffer, index, end, "nan"))
        {
            result = float.NaN;
            return true;
        }
        if (MatchesAsciiIgnoreCase(buffer, index, end, "inf")
            || MatchesAsciiIgnoreCase(buffer, index, end, "infinity"))
        {
            result = negative ? float.NegativeInfinity : float.PositiveInfinity;
            return true;
        }

        double value = 0d;
        bool hasDigits = false;
        while (index < end && IsDigit(buffer[index]))
        {
            hasDigits = true;
            value = value * 10d + buffer[index] - (byte)'0';
            index++;
        }

        if (index < end && buffer[index] == (byte)'.')
        {
            index++;
            double place = 0.1d;
            while (index < end && IsDigit(buffer[index]))
            {
                hasDigits = true;
                value += (buffer[index] - (byte)'0') * place;
                place *= 0.1d;
                index++;
            }
        }

        if (!hasDigits)
        {
            return false;
        }

        int exponent = 0;
        bool exponentNegative = false;
        if (index < end && (buffer[index] == (byte)'e' || buffer[index] == (byte)'E'))
        {
            index++;
            if (index < end && (buffer[index] == (byte)'-' || buffer[index] == (byte)'+'))
            {
                exponentNegative = buffer[index] == (byte)'-';
                index++;
            }

            bool hasExponentDigits = false;
            while (index < end && IsDigit(buffer[index]))
            {
                hasExponentDigits = true;
                exponent = Math.Min(400, exponent * 10 + buffer[index] - (byte)'0');
                index++;
            }

            if (!hasExponentDigits)
            {
                return false;
            }
        }

        SkipWhitespace(buffer, ref index, end);
        if (index != end)
        {
            return false;
        }

        if (exponent != 0)
        {
            value *= Math.Pow(10d, exponentNegative ? -exponent : exponent);
        }
        if (negative)
        {
            value = -value;
        }

        result = (float)value;
        return true;
    }

    private static void SkipWhitespace(byte[] buffer, ref int index, int end)
    {
        while (index < end
               && (buffer[index] == (byte)' '
                   || buffer[index] == (byte)'\t'
                   || buffer[index] == (byte)'\r'))
        {
            index++;
        }
    }

    private static bool IsDigit(byte value)
    {
        return value >= (byte)'0' && value <= (byte)'9';
    }

    private static bool MatchesAsciiIgnoreCase(
        byte[] buffer,
        int start,
        int end,
        string expected)
    {
        int index = start;
        int expectedIndex = 0;
        while (index < end && expectedIndex < expected.Length)
        {
            byte current = buffer[index];
            if (current >= (byte)'A' && current <= (byte)'Z')
            {
                current = (byte)(current + ((byte)'a' - (byte)'A'));
            }
            if (current != (byte)expected[expectedIndex])
            {
                return false;
            }
            index++;
            expectedIndex++;
        }

        SkipWhitespace(buffer, ref index, end);
        return expectedIndex == expected.Length && index == end;
    }
}
