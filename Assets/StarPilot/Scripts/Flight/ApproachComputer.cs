using UnityEngine;

namespace StarPilot
{
    /// <summary>Limits velocity towards the selected site; steering and lateral flight remain manual.</summary>
    public sealed class ApproachComputer : MonoBehaviour
    {
        public ShipFlightController Ship;
        public ExplorationController Expedition;
        public bool Armed {get;private set;}
        public bool Available=>Expedition && Expedition.Active && Expedition.Target && Expedition.Target.gameObject.activeInHierarchy;
        public bool Active=>Armed && Available && Ship.EngineOn && Ship.ControlsEnabled && Ship.Hull>0;
        public float Distance {get;private set;}
        public float ClosingSpeed {get;private set;}
        public float Standoff {get;private set;}
        public float StoppingDistance {get;private set;}
        public float AllowedClosingSpeed {get;private set;}
        public bool TooFast {get;private set;}
        public bool Limiting {get;private set;}
        public float Margin=>Distance-Standoff;
        Vector3 direction;

        public void SetArmed(bool armed){Armed=armed && Expedition && Expedition.Active;Limiting=false;}
        public void Toggle(){SetArmed(!Armed);}
        void Update(){Refresh();if(!Active)Limiting=false;}

        public void Refresh()
        {
            if(!Available){Distance=ClosingSpeed=StoppingDistance=AllowedClosingSpeed=Standoff=0;TooFast=false;return;}
            var site=Expedition.Target;
            Vector3 offset=site.Position-Ship.Body.position;Distance=offset.magnitude;
            direction=Distance>.01f?offset/Distance:Vector3.zero;
            ClosingSpeed=Vector3.Dot(Ship.Body.linearVelocity,direction);
            Standoff=site.Kind==SiteKind.Station?12:site.Kind==SiteKind.Anomaly?85:
                site.Scanned && (site.Kind==SiteKind.Wreck || site.Kind==SiteKind.Mineral || site.Kind==SiteKind.Archive)?25:Mathf.Min(110,Expedition.ScanRange*.65f);
            float deceleration=Mathf.Max(.1f,Ship.brakingAcceleration);
            float closing=Mathf.Max(0,ClosingSpeed);
            StoppingDistance=closing*closing/(2*deceleration);
            // Reserve distance and deceleration for discrete physics steps and a smooth final approach.
            AllowedClosingSpeed=Mathf.Sqrt(2*deceleration*.65f*Mathf.Max(0,Margin-3));
            TooFast=closing>5 && StoppingDistance+closing*.4f>Mathf.Max(0,Margin);
        }

        public Vector3 ConstrainAcceleration(Vector3 worldAcceleration,float step)
        {
            Refresh();Limiting=false;
            if(!Active || step<=0)return worldAcceleration;
            float commanded=Vector3.Dot(worldAcceleration,direction);
            float permitted=Mathf.Max(-Ship.brakingAcceleration,(AllowedClosingSpeed-ClosingSpeed)/step);
            if(commanded<=permitted)return worldAcceleration;
            Limiting=commanded-permitted>.05f;
            return worldAcceleration+direction*(permitted-commanded);
        }
    }
}
