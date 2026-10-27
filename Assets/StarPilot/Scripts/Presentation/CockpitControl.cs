using UnityEngine;

namespace StarPilot
{
    public enum CockpitAction { Engine, Assist, Brake, Boost, Headlights, CabinLight, Scan, Recover, NextSignal, Home, Mission, Approach, RadarRange }

    /// <summary>A physical switch and its lamp always display simulation state, regardless of input device.</summary>
    public sealed class CockpitControl : MonoBehaviour
    {
        public CockpitAction Action;
        public ShipFlightController Ship;
        public ShipLighting Lighting;
        public ExplorationController Expedition;
        public Transform Handle;
        public Renderer Lamp;
        public Material Lit, Unlit;
        public bool Held { get; private set; }
        float pressedUntil;
        public bool IsToggle => Action==CockpitAction.Engine || Action==CockpitAction.Assist || Action==CockpitAction.Headlights || Action==CockpitAction.CabinLight || Action==CockpitAction.Approach;
        public bool Active => Action switch {
            CockpitAction.Engine=>Ship.EngineOn, CockpitAction.Assist=>Ship.AssistOn,
            CockpitAction.Brake=>Ship.Brake, CockpitAction.Boost=>Ship.Boost,
            CockpitAction.Headlights=>Lighting.HeadlightsOn, CockpitAction.CabinLight=>Lighting.CabinBright,
            CockpitAction.Approach=>Ship.Approach && Ship.Approach.Armed,
            CockpitAction.Scan=>Expedition && Expedition.IsScanning,CockpitAction.Recover=>Expedition && Expedition.IsInteracting,_=>Time.time<pressedUntil };
        public void Press()
        {
            if(!Ship.ControlsEnabled)return;
            if(Action==CockpitAction.Engine)Ship.ToggleEngine();
            else if(Action==CockpitAction.Assist)Ship.ToggleAssist();
            else if(Action==CockpitAction.Headlights)Lighting.ToggleHeadlights();
            else if(Action==CockpitAction.CabinLight)Lighting.ToggleCabin();
            else if(Action==CockpitAction.NextSignal){Expedition?.CycleTarget();pressedUntil=Time.time+.25f;}
            else if(Action==CockpitAction.Home){Expedition?.SelectHome();pressedUntil=Time.time+.25f;}
            else if(Action==CockpitAction.Mission){Expedition?.FocusMission();pressedUntil=Time.time+.25f;}
            else if(Action==CockpitAction.Approach)Ship.Approach?.Toggle();
            else if(Action==CockpitAction.RadarRange){Expedition?.CycleRadarRange();pressedUntil=Time.time+.25f;}
            else Held=true;
        }
        public void Release(){Held=false;}
        void LateUpdate()
        {
            if(IsToggle)Handle.localRotation=Quaternion.Slerp(Handle.localRotation,Quaternion.Euler(Active?-28:28,0,0),Time.unscaledDeltaTime*16);
            else Handle.localPosition=Vector3.Lerp(Handle.localPosition,new Vector3(0,0,Active?.007f:-.008f),Time.unscaledDeltaTime*20);
            Lamp.sharedMaterial=Active?Lit:Unlit;
        }
        void OnDisable(){Held=false;}
    }
}
