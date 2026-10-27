using UnityEngine;

namespace StarPilot
{
    /// <summary>Small synthesized engine loop; no imported or unlicensed audio.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class ShipAudio : MonoBehaviour
    {
        public ShipFlightController Ship;
        public ExplorationController Expedition;
        AudioSource source, instruments;
        AudioClip loop, confirmation, approachAlert;
        float nextApproachAlert;
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
            instruments=gameObject.AddComponent<AudioSource>();instruments.playOnAwake=false;instruments.volume=.22f;
            float[] cue=new float[sampleRate/2];
            for(int i=0;i<cue.Length;i++)
            {
                float t=i/(float)sampleRate,phase=t<.2f?t:t-.25f;
                if(phase>=0 && phase<.2f)cue[i]=Mathf.Sin(2*Mathf.PI*(t<.2f?660:990)*phase)*Mathf.Sin(Mathf.PI*phase/.2f)*.4f;
            }
            confirmation=AudioClip.Create("Survey confirmation",cue.Length,1,sampleRate,false);confirmation.SetData(cue,0);
            float[] warning=new float[sampleRate/3];
            for(int i=0;i<warning.Length;i++)
            {
                float t=i/(float)sampleRate,phase=t%.16f;
                warning[i]=Mathf.Sin(2*Mathf.PI*440*t)*Mathf.Sin(Mathf.PI*Mathf.Clamp01(phase/.1f))*.35f;
            }
            approachAlert=AudioClip.Create("Approach braking advisory",warning.Length,1,sampleRate,false);approachAlert.SetData(warning,0);
            if(Expedition)Expedition.Feedback+=OnSurvey;
        }

        void OnSurvey(bool success){if(success && instruments)instruments.PlayOneShot(confirmation);}

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
            if(Ship.ControlsEnabled && Ship.Approach && Ship.Approach.TooFast && Time.time>=nextApproachAlert)
            {instruments.PlayOneShot(approachAlert);nextApproachAlert=Time.time+4;}
        }

        void OnDestroy()
        {
            if (Ship) Ship.Impact -= OnImpact;
            if (Expedition) Expedition.Feedback -= OnSurvey;
            if (loop) Destroy(loop);
            if (confirmation) Destroy(confirmation);
            if (approachAlert) Destroy(approachAlert);
        }
    }
}
