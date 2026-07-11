using UnityEngine;

[DisallowMultipleComponent]
public class B737FmsButton : MonoBehaviour
{
    public enum ButtonType
    {
        LeftLine,
        RightLine,
        Index,
        Clear
    }

    [SerializeField] private B737FmsDisplay display;
    [SerializeField] private ButtonType buttonType;
    [SerializeField, Range(1, 6)] private int lineIndex = 1;

    public void Configure(B737FmsDisplay targetDisplay, ButtonType type, int line)
    {
        display = targetDisplay;
        buttonType = type;
        lineIndex = Mathf.Clamp(line, 1, 6);
    }

    public void Press()
    {
        if (display == null)
        {
            display = FindObjectOfType<B737FmsDisplay>();
        }

        if (display == null)
        {
            Debug.LogWarning($"[B737 FMS] Button {name} has no FMS display target.", this);
            return;
        }

        if (buttonType == ButtonType.LeftLine)
        {
            display.PressLeftLine(lineIndex);
        }
        else if (buttonType == ButtonType.RightLine)
        {
            display.PressRightLine(lineIndex);
        }
        else if (buttonType == ButtonType.Index)
        {
            display.ShowIndexPage();
        }
        else if (buttonType == ButtonType.Clear)
        {
            display.ClearOrBack();
        }
    }
}
