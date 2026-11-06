using UnityEngine;

public class ParticleAutoPlay : MonoBehaviour
{
    private ParticleSystem[] systems;

    private void Awake()
    {
        systems = GetComponentsInChildren<ParticleSystem>(true);
    }

    private void OnEnable()
    {
        if (systems == null || systems.Length == 0)
            systems = GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem ps in systems)
        {
            ps.Clear(true);
            ps.Play(true);
        }
    }
}