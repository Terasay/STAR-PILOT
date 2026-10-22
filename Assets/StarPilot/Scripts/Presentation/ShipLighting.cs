using UnityEngine;

namespace StarPilot
{
    /// <summary>Battery-fed working lights remain available with propulsion switched off.</summary>
    public sealed class ShipLighting : MonoBehaviour
    {
        public Light[] CabinLights, Headlights;
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
        public void ToggleHeadlights(){HeadlightsOn=!HeadlightsOn;Apply();}
        public void ToggleCabin(){CabinBright=!CabinBright;Apply();}
        void Apply()
        {
            if(Headlights!=null)foreach(var lamp in Headlights)lamp.enabled=HeadlightsOn;
            if(CabinLights!=null)for(int i=0;i<CabinLights.Length;i++)
                CabinLights[i].intensity=cabinIntensities[i]*(CabinBright?1:.28f);
        }
    }
}
