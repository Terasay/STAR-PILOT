using System.Collections.Generic;
using UnityEngine;

namespace StarPilot
{
    [DefaultExecutionOrder(-100)]
    public sealed class DemoBootstrap : MonoBehaviour
    {
        public ShipFlightController Ship { get; private set; }
        public Camera PilotCamera { get; private set; }
        public FlightRoute Route { get; private set; }
        public PilotInput Input { get; private set; }
        public DemoHud Hud { get; private set; }
        public ShipAudio Audio { get; private set; }
        public XrFlightMode Xr { get; private set; }
        public CockpitInteraction Cockpit { get; private set; }
        readonly List<Object> owned=new List<Object>();

        void Awake()
        {
            QualitySettings.vSyncCount=1;Application.targetFrameRate=90;
            var builder=new DemoWorldBuilder(owned);
            builder.Build(transform,out var ship,out var camera,out var route,out var displays);
            Ship=ship;PilotCamera=camera;Route=route;
            Input=Ship.gameObject.AddComponent<PilotInput>();Input.Ship=Ship;Input.View=PilotCamera.transform;
            Cockpit=Ship.GetComponent<CockpitInteraction>();Cockpit.Input=Input;
            Audio=Ship.gameObject.AddComponent<ShipAudio>();Audio.Ship=Ship;
            Xr=gameObject.AddComponent<XrFlightMode>();Xr.Demo=this;
            Hud=gameObject.AddComponent<DemoHud>();Hud.Demo=this;Hud.Input=Input;Hud.Ship=Ship;Hud.Route=Route;Hud.PilotCamera=PilotCamera;
            Hud.Build();
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-starPilotSmokeTest")>=0)
                gameObject.AddComponent<DemoSmokeTest>().Demo=this;
        }

        public void ResetFlight() { Ship.ResetShip();Route.Restart(); }
        public void RequestVr() { Xr.StartVr(); }
        void OnDestroy()
        {
            Time.timeScale=1f;
            foreach(var item in owned)if(item)Destroy(item);
        }
    }
}
