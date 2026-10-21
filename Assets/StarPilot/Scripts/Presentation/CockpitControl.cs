using UnityEngine;

namespace StarPilot
{
    public enum CockpitAction { Engine, Assist, Brake, Boost }

    /// <summary>A physical switch and its lamp always display simulation state, regardless of input device.</summary>
    public sealed class CockpitControl : MonoBehaviour
    {
        public CockpitAction Action;
        public ShipFlightController Ship;
        public Transform Handle;
        public Renderer Lamp;
        public Material Lit, Unlit;
        public bool Held { get; private set; }
        public bool IsToggle => Action==CockpitAction.Engine || Action==CockpitAction.Assist;
        public bool Active => Action==CockpitAction.Engine?Ship.EngineOn:Action==CockpitAction.Assist?Ship.AssistOn:Action==CockpitAction.Brake?Ship.Brake:Ship.Boost;
        public void Press()
        {
            if(!Ship.ControlsEnabled)return;
            if(Action==CockpitAction.Engine)Ship.ToggleEngine();
            else if(Action==CockpitAction.Assist)Ship.ToggleAssist();
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
