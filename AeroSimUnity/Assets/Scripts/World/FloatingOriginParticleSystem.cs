using UnityEngine;

/// <summary>
/// Fixes live particles that are simulated in world space when the floating
/// origin shifts.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class FloatingOriginParticleSystem : MonoBehaviour
{
    [SerializeField] private bool clearInsteadOfMove;

    private ParticleSystem particles;
    private ParticleSystem.Particle[] buffer;

    private void Awake()
    {
        particles = GetComponent<ParticleSystem>();
    }

    private void OnEnable()
    {
        FloatingOriginManager.OriginShifted += HandleOriginShift;
    }

    private void OnDisable()
    {
        FloatingOriginManager.OriginShifted -= HandleOriginShift;
    }

    private void HandleOriginShift(Vector3 offset)
    {
        if (particles == null) particles = GetComponent<ParticleSystem>();

        if (clearInsteadOfMove)
        {
            particles.Clear(true);
            return;
        }

        ParticleSystem.MainModule main = particles.main;
        if (main.simulationSpace != ParticleSystemSimulationSpace.World) return;

        int maxParticles = main.maxParticles;
        if (buffer == null || buffer.Length < maxParticles)
            buffer = new ParticleSystem.Particle[maxParticles];

        int count = particles.GetParticles(buffer);
        for (int i = 0; i < count; i++)
            buffer[i].position += offset;

        particles.SetParticles(buffer, count);
    }
}
