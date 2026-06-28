using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class SingleAudioListenerEnforcer : MonoBehaviour
{
    [SerializeField] private AudioListener localListener;

    private void Reset()
    {
        localListener = GetComponent<AudioListener>();
    }

    private void Awake()
    {
        AssignListenerIfNeeded();
    }

    private void OnEnable()
    {
        AssignListenerIfNeeded();
        EnforceSingleListener();
    }

    private void AssignListenerIfNeeded()
    {
        if (localListener == null)
        {
            localListener = GetComponent<AudioListener>();
        }
    }

    [ContextMenu("Enforce Single Audio Listener")]
    public void EnforceSingleListener()
    {
        AssignListenerIfNeeded();

        if (localListener == null)
        {
            return;
        }

        AudioListener[] listeners = FindObjectsOfType<AudioListener>(true);

        foreach (AudioListener listener in listeners)
        {
            if (listener == null)
            {
                continue;
            }

            listener.enabled = listener == localListener;
        }
    }
}
