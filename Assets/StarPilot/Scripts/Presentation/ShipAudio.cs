using UnityEngine;

namespace StarPilot
{
    /// <summary>Small synthesized engine loop; no imported or unlicensed audio.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class ShipAudio : MonoBehaviour
    {
        public ShipFlightController Ship;
        AudioSource source;
        AudioClip loop;
        public float ImpactFlash { get; private set; }

        void Start()
        {
            source = GetComponent<AudioSource>();
            const int sampleRate = 22050;
            float[] samples = new float[sampleRate * 2];
            var random = new System.Random(43);
            float noise = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)sampleRate;
                noise = Mathf.Lerp(noise, (float)random.NextDouble() * 2f - 1f, 0.035f);
                samples[i] = Mathf.Sin(t * 2f * Mathf.PI * 44f) * 0.18f +
                    Mathf.Sin(t * 2f * Mathf.PI * 88f) * 0.07f + noise * 0.24f;
            }
            loop = AudioClip.Create("StarPilot propulsion loop", samples.Length, 1, sampleRate, false);
            loop.SetData(samples, 0);
            source.clip = loop;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.Play();
            Ship.Impact += OnImpact;
        }

        void OnImpact(float severity, Vector3 localPoint)
        {
            ImpactFlash = Mathf.Clamp01(severity / 20f);
            // The same directional impact event is available for a future vest adapter.
            Debug.Log($"[STAR PILOT] Hull impact {severity:F1} m/s; local direction {localPoint.normalized}");
        }

        void Update()
        {
            float target = Ship.EngineOn ? 0.14f + Ship.Thrust * 0.22f : 0f;
            source.volume = Mathf.MoveTowards(source.volume, target, Time.unscaledDeltaTime * 0.5f);
            source.pitch = Mathf.Lerp(source.pitch, 0.8f + Ship.Thrust * (Ship.Boost ? 0.8f : 0.4f), Time.unscaledDeltaTime * 3f);
            ImpactFlash = Mathf.MoveTowards(ImpactFlash, 0f, Time.unscaledDeltaTime * 2f);
        }

        void OnDestroy()
        {
            if (Ship) Ship.Impact -= OnImpact;
            if (loop) Destroy(loop);
        }
    }
}
