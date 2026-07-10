using UnityEngine;

public class PFDAttitudeSimulator : MonoBehaviour
{
    public enum SimulationMode
    {
        Manual,
        Automatic
    }

    [SerializeField] private PFDHorizonController controller;
    [SerializeField] private SimulationMode mode = SimulationMode.Automatic;
    [SerializeField, Range(-30f, 30f)] private float manualPitchDeg;
    [SerializeField, Range(-60f, 60f)] private float manualRollDeg;
    [SerializeField] private float pitchAmplitude = 20f;
    [SerializeField, Min(0.01f)] private float pitchPeriod = 6f;
    [SerializeField] private float rollAmplitude = 30f;
    [SerializeField, Min(0.01f)] private float rollPeriod = 8f;

    public static Vector2 EvaluateAutomaticAttitude(
        float time,
        float pitchAmplitude,
        float pitchPeriod,
        float rollAmplitude,
        float rollPeriod)
    {
        return new Vector2(
            EvaluateSine(time, pitchAmplitude, pitchPeriod),
            EvaluateSine(time, rollAmplitude, rollPeriod));
    }

    private void Update()
    {
        if (controller == null)
        {
            controller = GetComponent<PFDHorizonController>();
        }

        if (controller == null)
        {
            return;
        }

        Vector2 attitude = mode == SimulationMode.Manual
            ? new Vector2(manualPitchDeg, manualRollDeg)
            : EvaluateAutomaticAttitude(
                Time.time,
                pitchAmplitude,
                pitchPeriod,
                rollAmplitude,
                rollPeriod);

        controller.SetAttitude(attitude.x, attitude.y);
    }

    private static float EvaluateSine(float time, float amplitude, float period)
    {
        if (period <= 0f)
        {
            return 0f;
        }

        return amplitude * Mathf.Sin(time * Mathf.PI * 2f / period);
    }
}
