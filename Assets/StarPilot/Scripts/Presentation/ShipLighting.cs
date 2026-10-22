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

        public void Configure(Light[] cabin, Light[] exterior)
        {
            CabinLights=cabin;Headlights=exterior;
            cabinIntensities=new float[cabin.Length];
            for(int i=0;i<cabin.Length;i++)cabinIntensities[i]=cabin[i].intensity;
            Apply();
        }
        public void SetHeadlights(bool on){HeadlightsOn=on;Apply();}
        public void SetCabinBright(bool bright){CabinBright=bright;Apply();}
        public void ToggleHeadlights(){SetHeadlights(!HeadlightsOn);}
        public void ToggleCabin(){SetCabinBright(!CabinBright);}
        void Apply()
        {
            if(Headlights!=null)foreach(var lamp in Headlights)lamp.enabled=HeadlightsOn;
            if(HeadlightLenses!=null)foreach(var lens in HeadlightLenses)lens.sharedMaterial=HeadlightsOn?LensOn:LensOff;
            if(CabinLights!=null)for(int i=0;i<CabinLights.Length;i++)
                CabinLights[i].intensity=cabinIntensities[i]*(CabinBright?1:.28f);
        }
    }
}
