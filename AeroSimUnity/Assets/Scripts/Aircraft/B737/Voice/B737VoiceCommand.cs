public enum B737VoiceCommandType
{
    SetThrottle,
    SetFlapStep,
    ChangeFlapStep,
    SetSpoilerStep,
    ChangeSpoilerStep,
    SetGearDown,
    SetBrakes,
    AdjustPitchTrim,
    SetPaused
}

public struct B737VoiceCommand
{
    public B737VoiceCommand(B737VoiceCommandType type, float value)
    {
        Type = type;
        Value = value;
    }

    public B737VoiceCommandType Type { get; }
    public float Value { get; }
}
