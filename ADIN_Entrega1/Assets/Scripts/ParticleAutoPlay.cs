using UnityEngine;

public class ParticleAutoPlay : MonoBehaviour
{
    private void Awake()
    {
        ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true);

        float maxDuration = 0f;

        foreach (ParticleSystem ps in systems)
        {
            ps.Play(true);

            ParticleSystem.MainModule main = ps.main;
            float duration = main.duration + main.startLifetime.constantMax;

            if (duration > maxDuration)
                maxDuration = duration;
        }

        if (systems.Length == 0)
            maxDuration = 3f;

        Destroy(gameObject, maxDuration);
    }
}