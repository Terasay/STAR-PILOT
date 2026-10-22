using UnityEngine;

namespace StarPilot
{
    public enum CockpitAction { Engine, Assist, Brake, Boost, Headlights, CabinLight }

    /// <summary>A physical switch and its lamp always display simulation state, regardless of input device.</summary>
    public sealed class CockpitControl : MonoBehaviour
    {
        public CockpitAction Action;
        public ShipFlightController Ship;
        public ShipLighting Lighting;
        public Transform Handle;
        public Renderer Lamp;
        public Material Lit, Unlit;
        public bool Held { get; private set; }
        public bool IsToggle => Action!=CockpitAction.Brake && Action!=CockpitAction.Boost;
        public bool Active => Action switch {
            CockpitAction.Engine=>Ship.EngineOn, CockpitAction.Assist=>Ship.AssistOn,
            CockpitAction.Brake=>Ship.Brake, CockpitAction.Boost=>Ship.Boost,
            CockpitAction.Headlights=>Lighting.HeadlightsOn, _=>Lighting.CabinBright };
        public void Press()
        {
            if(!Ship.ControlsEnabled)return;
            if(Action==CockpitAction.Engine)Ship.ToggleEngine();
            else if(Action==CockpitAction.Assist)Ship.ToggleAssist();
            else if(Action==CockpitAction.Headlights)Lighting.ToggleHeadlights();
            else if(Action==CockpitAction.CabinLight)Lighting.ToggleCabin();
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
