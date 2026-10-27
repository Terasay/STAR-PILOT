using UnityEngine;

namespace StarPilot
{
    /// <summary>Battery-fed working lights remain available with propulsion switched off.</summary>
    public sealed class ShipLighting : MonoBehaviour
    {
        public Light[] CabinLights, Headlights;
        public Renderer[] HeadlightLenses;
        public Material LensOn, LensOff;
        public bool HeadlightsOn { get; private set; } = true;
        public bool CabinBright { get; private set; } = true;
        float[] cabinIntensities;
        float[] floodIntensities, floodTargets;
        float rangeProbeDelay;
        readonly RaycastHit[] rangeHits=new RaycastHit[16];
        static readonly Vector3[] BeamSamples={Vector3.forward,new Vector3(.22f,0,1),new Vector3(-.22f,0,1),new Vector3(0,.22f,1),new Vector3(0,-.22f,1)};

        public void Configure(Light[] cabin, Light[] exterior)
        {
            CabinLights=cabin;Headlights=exterior;
            cabinIntensities=new float[cabin.Length];
            for(int i=0;i<cabin.Length;i++)cabinIntensities[i]=cabin[i].intensity;
            floodIntensities=new float[exterior.Length];floodTargets=new float[exterior.Length];
            for(int i=0;i<exterior.Length;i++)floodIntensities[i]=exterior[i].intensity;
            Apply();
        }
        public void SetHeadlights(bool on){HeadlightsOn=on;Apply();}
        public void SetCabinBright(bool bright){CabinBright=bright;Apply();}
        public void ToggleHeadlights(){SetHeadlights(!HeadlightsOn);}
        public void ToggleCabin(){SetCabinBright(!CabinBright);}
        void LateUpdate()
        {
            if(!HeadlightsOn || floodIntensities==null)return;
            rangeProbeDelay-=Time.unscaledDeltaTime;
            if(rangeProbeDelay<=0){MeasureFloodRange();rangeProbeDelay=.1f;}
            for(int i=0;i<Headlights.Length;i++)
            {
                var lamp=Headlights[i];
                // React quickly on approach; restore distant illumination gently after passing an object.
                float response=floodTargets[i]<lamp.intensity?.06f:.45f;
                lamp.intensity=Mathf.Lerp(lamp.intensity,floodTargets[i],1-Mathf.Exp(-Time.unscaledDeltaTime/response));
            }
        }
        void MeasureFloodRange()
        {
            for(int i=0;i<Headlights.Length;i++)
            {
                var lamp=Headlights[i];float distance=Mathf.Min(lamp.range,120);
                foreach(var sample in BeamSamples)
                {
                    int count=Physics.RaycastNonAlloc(lamp.transform.position,lamp.transform.TransformDirection(sample.normalized),rangeHits,distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                    for(int hit=0;hit<count;hit++)
                        if(!rangeHits[hit].collider.transform.IsChildOf(transform))distance=Mathf.Min(distance,rangeHits[hit].distance);
                }
                // Cap incident light near a hull while retaining full power in open space.
                floodTargets[i]=Mathf.Min(floodIntensities[i],2*distance*distance);
            }
        }
        void Apply()
        {
            if(floodIntensities!=null)
            {
                MeasureFloodRange();
                for(int i=0;i<Headlights.Length;i++){Headlights[i].enabled=HeadlightsOn;Headlights[i].intensity=floodTargets[i];}
            }
            if(HeadlightLenses!=null)foreach(var lens in HeadlightLenses)lens.sharedMaterial=HeadlightsOn?LensOn:LensOff;
            if(CabinLights!=null)for(int i=0;i<CabinLights.Length;i++)
                CabinLights[i].intensity=cabinIntensities[i]*(CabinBright?1:.28f);
        }
    }
}
