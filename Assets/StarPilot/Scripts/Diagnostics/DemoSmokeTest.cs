using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StarPilot
{
    /// <summary>Opt-in integration checks in an actual Windows player, including rendered screenshots.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class DemoSmokeTest : MonoBehaviour
    {
        public DemoBootstrap Demo;
        [Serializable] sealed class Check { public string name; public bool passed; public string detail; }
        [Serializable] sealed class Report
        {
            public string unityVersion, graphicsDevice, resolution;
            public List<Check> checks=new List<Check>();
            public List<string> errors=new List<string>();
        }
        readonly Report report=new Report();
        Keyboard injectedKeyboard;
        Mouse injectedMouse;
        InputSettings.BackgroundBehavior previousBackgroundBehavior;
        string output;
        void Awake()
        {
            Application.logMessageReceived+=OnLog;
            previousBackgroundBehavior=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        }
        void Update()
        {
            // Keep this player's injected devices current while the user works in another window.
            // The ordinary game never changes background input handling or device selection.
            if(injectedKeyboard!=null && injectedKeyboard.added)injectedKeyboard.MakeCurrent();
            if(injectedMouse!=null && injectedMouse.added)injectedMouse.MakeCurrent();
        }
        void OnLog(string text,string stack,LogType type)
        {
            if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)report.errors.Add(text+"\n"+stack);
        }
        void CheckResult(string name,bool passed,string detail)
        {
            report.checks.Add(new Check{name=name,passed=passed,detail=detail});
            Debug.Log($"[STAR PILOT TEST] {(passed?"PASS":"FAIL")} / {name} / {detail}");
        }
        IEnumerator Start()
        {
            string[] args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-testOutput");
            output=index>=0 && index+1<args.Length?args[index+1]:Path.Combine(Application.persistentDataPath,"SmokeTest");
            Directory.CreateDirectory(output);
            report.unityVersion=Application.unityVersion;report.graphicsDevice=SystemInfo.graphicsDeviceName;
            report.resolution=$"{Screen.width} x {Screen.height}";
            yield return new WaitForSecondsRealtime(1.5f);
            CaptureFrame("01-menu.png");
            yield return new WaitForSecondsRealtime(.5f);
            Demo.SetTraining(true);Demo.Hud.SetMenu(false);Demo.Input.enabled=false;
            var ship=Demo.Ship;
            CheckResult("Scene initialized",Demo.Route.Gates.Length==4 && Camera.main==Demo.PilotCamera,"4 gates, pilot camera, live cockpit");
            CheckResult("Unobstructed first-person view",Demo.Hud.FlightOverlayHidden && Demo.Hud.DesktopCanvas.GetComponentsInChildren<UnityEngine.UI.Graphic>().Length==0,
                "No active screen-space text, reticle, objective marker or impact overlay during flight");
            var displays=ship.GetComponent<CockpitDisplays>();
            yield return new WaitForSecondsRealtime(.15f);
            CheckResult("Navigation on physical instrument",displays.RouteText.text.Contains("1 / 4") && displays.NavigationText.text.Contains("м") &&
                displays.GuidanceText.GetComponentInParent<Canvas>().renderMode==RenderMode.WorldSpace,"Gate, range and course guidance belong to the cockpit screen");
            ship.Body.rotation=Quaternion.Euler(0,180,0);yield return Steps(2);yield return new WaitForSecondsRealtime(.15f);
            CheckResult("Instrument guides to a target behind the ship",displays.GuidanceText.text.Contains("180°"),displays.GuidanceText.text);
            ship.Body.rotation=Quaternion.identity;yield return Steps(2);
            Demo.Hud.SetMenu(true);yield return new WaitForSecondsRealtime(.15f);
            CheckResult("Pause is visible on the cockpit display",displays.StatusText.text.Contains("ПРИОСТАНОВЛЕН"),"Physical status display announces pause, including when the desktop menu is hidden in VR");
            Demo.Hud.SetMenu(false);
            ship.Translation=Vector3.forward;
            yield return Steps(100);
            CheckResult("Forward thrust",ship.ForwardSpeed>20f && ship.ForwardSpeed<26f,$"velocity {ship.ForwardSpeed:F2} m/s");
            ship.Translation=Vector3.zero;ship.ToggleAssist();float speed=ship.Speed;
            yield return Steps(50);
            CheckResult("Inertial coast",Mathf.Abs(ship.Speed-speed)<.1f,$"before {speed:F2}; after {ship.Speed:F2}");
            ship.Brake=true;yield return Steps(100);ship.Brake=false;
            CheckResult("Active braking",ship.Speed<.1f,$"velocity {ship.Speed:F3} m/s");
            ship.Translation=Vector3.right;yield return Steps(50);ship.Translation=Vector3.zero;
            CheckResult("Lateral thrusters",ship.Body.linearVelocity.x>6f,$"X velocity {ship.Body.linearVelocity.x:F2} m/s");
            Quaternion before=ship.transform.rotation;ship.Rotation=new Vector3(0,.6f,0);yield return Steps(50);ship.Rotation=Vector3.zero;
            CheckResult("Angular flight",Quaternion.Angle(before,ship.transform.rotation)>10f,$"turn {Quaternion.Angle(before,ship.transform.rotation):F2} degrees");
            Demo.ResetFlight();
            CheckResult("Reset",ship.Speed<.001f && ship.Hull==100f && !ship.EngineOn && Demo.Route.Current==0,"velocity, hull, engine and route reset");
            ship.ToggleEngine();ship.ToggleAssist();
            ship.Body.position=Demo.Route.Gates[0].position-Vector3.forward*4;
            ship.Body.linearVelocity=Vector3.forward*30;
            yield return Steps(20);
            CheckResult("Checkpoint crossing",Demo.Route.Current==1,$"current gate index {Demo.Route.Current}");

            Demo.ResetFlight();ship.ToggleEngine();ship.ToggleAssist();
            var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.name="Integration test obstacle";
            obstacle.transform.position=new Vector3(0,1.2f,16);obstacle.transform.localScale=new Vector3(12,12,2);
            ship.Body.linearVelocity=Vector3.forward*32;yield return Steps(40);
            CheckResult("Collision damage",ship.Hull<99f,$"hull {ship.Hull:F2}%");Destroy(obstacle);
            Demo.ResetFlight();ship.ToggleEngine();
            var testKeyboard=InputSystem.AddDevice<Keyboard>("StarPilotSmokeKeyboard");
            injectedKeyboard=testKeyboard;
            Demo.Input.enabled=true;
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.W));
            yield return null;yield return Steps(10);
            CheckResult("Keyboard flight input",ship.Translation.z>.9f && ship.ForwardSpeed>1f,$"W command {ship.Translation.z}; speed {ship.ForwardSpeed:F2}");
            CheckResult("Physical throttle follows keyboard",Mathf.Abs(Mathf.DeltaAngle(0,Demo.Cockpit.Throttle.localEulerAngles.x))>12,"W moves the visible throttle and resting glove");
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.Space));
            yield return null;yield return null;
            CheckResult("Keyboard brake input",ship.Brake,"Space command reaches flight controller");
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.T));
            yield return null;yield return null;
            CheckResult("Keyboard assist toggle",!ship.AssistOn,"T toggles stabilization");
            var testMouse=InputSystem.AddDevice<Mouse>("StarPilotCockpitMouse");
            injectedMouse=testMouse;
            var engine=Demo.Cockpit.Controls.Find(c=>c.Action==CockpitAction.Engine);
            Vector3 screen=Demo.PilotCamera.WorldToScreenPoint(engine.transform.position);
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.Tab));
            InputSystem.QueueStateEvent(testMouse,new MouseState{position=new Vector2(screen.x,screen.y)});
            yield return null;yield return null;
            var click=new MouseState{position=new Vector2(screen.x,screen.y)};click=click.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(testMouse,click);
            yield return null;yield return null;
            CheckResult("Mouse cockpit switch",!ship.EngineOn && Demo.Input.CockpitPointer,"Tab + mouse ray reaches ENGINE switch and shuts down propulsion");
            InputSystem.QueueStateEvent(testMouse,new MouseState{position=new Vector2(screen.x,screen.y)});
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());
            yield return null;yield return null;
            InputSystem.RemoveDevice(testMouse);
            if(!ship.EngineOn)ship.ToggleEngine();
            var brakeControl=Demo.Cockpit.Controls.Find(c=>c.Action==CockpitAction.Brake);
            brakeControl.Press();yield return null;yield return null;
            CheckResult("Physical momentary brake",ship.Brake,"Cockpit BRAKE holds the shared flight command");
            brakeControl.Release();yield return null;yield return null;
            CheckResult("Physical brake release",!ship.Brake,"Releasing BRAKE clears the command without latching");
            Demo.Cockpit.PrepareXrActions();
            CheckResult("Desktop without XR tracking",!Demo.Cockpit.TryHandPose(-1,out _,out _,out _),"No headset required; tracked hands are not fabricated");
            var hands=ship.GetComponent<PilotHands>();
            CheckResult("Pilot hands at controls",Vector3.Distance(hands.Right.position,hands.RightRest.position)<.01f && Vector3.Distance(hands.Left.position,hands.LeftRest.position)<.01f,"Both visible glove rigs follow the control grip poses");
            var glove=hands.Left.GetComponent<SkinnedMeshRenderer>();
            var beforeSkin=new Mesh();var afterSkin=new Mesh();glove.BakeMesh(beforeSkin);
            var fingerJoint=hands.LeftFingers[1].GetChild(0);Quaternion previousJoint=fingerJoint.localRotation;
            fingerJoint.localRotation*=Quaternion.Euler(25,0,0);glove.BakeMesh(afterSkin);fingerJoint.localRotation=previousJoint;
            var original=beforeSkin.vertices;var bent=afterSkin.vertices;int moved=0;
            for(int vertex=0;vertex<original.Length;vertex++)if(Vector3.Distance(original[vertex],bent[vertex])>.0005f)moved++;
            CheckResult("Glove fabric follows finger joints",moved>50,$"{moved} cloth vertices deform at the middle finger joint");
            Destroy(beforeSkin);Destroy(afterSkin);
            var lighting=ship.GetComponent<ShipLighting>();
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.L));yield return null;yield return null;
            CheckResult("Keyboard headlights",!lighting.HeadlightsOn && !lighting.Headlights[0].enabled,"L switches both exterior floodlights off");
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.C));yield return null;yield return null;
            float dimIntensity=lighting.CabinLights[0].intensity;
            CheckResult("Keyboard cabin lighting",!lighting.CabinBright,"C selects dim cabin mode");
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());yield return null;yield return null;
            var cabinSwitch=Demo.Cockpit.Controls.Find(c=>c.Action==CockpitAction.CabinLight);
            cabinSwitch.Press();
            CheckResult("Overhead lighting switch",lighting.CabinBright && lighting.CabinLights[0].intensity>dimIntensity*3,"Physical overhead rocker restores bright work lights");
            var floodSwitch=Demo.Cockpit.Controls.Find(c=>c.Action==CockpitAction.Headlights);floodSwitch.Press();
            CheckResult("Overhead floodlight switch",lighting.HeadlightsOn,"Physical overhead rocker restores exterior floodlights");
            lighting.SetHeadlights(true);lighting.SetCabinBright(true);
            InputSystem.RemoveDevice(testKeyboard);Demo.Input.enabled=false;
            Demo.ResetFlight();ship.ToggleEngine();
            yield return Steps(5);
            ship.Translation=Vector3.forward;yield return Steps(30);ship.Translation=Vector3.zero;
            yield return new WaitForSecondsRealtime(.35f);
            CaptureFrame("02-cockpit.png");
            Demo.PilotCamera.transform.localRotation=Quaternion.Euler(27,-22,0);
            CaptureFrame("03-controls.png");
            Demo.PilotCamera.transform.localRotation=Quaternion.Euler(-8,22,0);
            CaptureFrame("04-space.png");
            Demo.PilotCamera.transform.localRotation=Quaternion.Euler(-23,0,0);
            CaptureFrame("05-overhead.png");
            Vector3 cameraPosition=Demo.PilotCamera.transform.localPosition;
            Demo.PilotCamera.transform.position=hands.Left.position+ship.transform.TransformDirection(new Vector3(.1f,.28f,-.22f));
            Demo.PilotCamera.transform.LookAt(hands.Left.position,ship.transform.up);
            CaptureFrame("08-glove-detail.png");
            Demo.PilotCamera.transform.localPosition=cameraPosition;
            // Inspect the open bind pose independently of grip occlusion; no XR tracking is fabricated.
            Demo.PilotCamera.transform.localRotation=Quaternion.identity;
            hands.enabled=false;
            Vector3 handPosition=hands.Left.position;Quaternion handRotation=hands.Left.rotation;
            var rotations=new Quaternion[glove.bones.Length];var bind=glove.sharedMesh.bindposes;
            for(int b=1;b<glove.bones.Length;b++)
            {
                rotations[b]=glove.bones[b].localRotation;
                int parent=Array.IndexOf(glove.bones,glove.bones[b].parent);
                glove.bones[b].localRotation=(bind[parent]*bind[b].inverse).rotation;
            }
            hands.Left.position=Demo.PilotCamera.transform.TransformPoint(new Vector3(-.08f,-.015f,.42f));
            hands.Left.rotation=Demo.PilotCamera.transform.rotation*Quaternion.Euler(-90,0,-12);
            hands.LeftArm.gameObject.SetActive(false);
            yield return null;
            CaptureFrame("09-glove-open.png");
            hands.LeftArm.gameObject.SetActive(true);hands.Left.SetPositionAndRotation(handPosition,handRotation);
            for(int b=1;b<glove.bones.Length;b++)glove.bones[b].localRotation=rotations[b];
            hands.enabled=true;
            ship.Translation=Vector3.zero;ship.Body.linearVelocity=Vector3.zero;
            var asteroid=GameObject.Find("Asteroid 0").transform;
            Demo.PilotCamera.transform.rotation=Quaternion.LookRotation(asteroid.position-Demo.PilotCamera.transform.position,ship.transform.up);
            lighting.SetHeadlights(false);yield return new WaitForSecondsRealtime(.15f);
            float unlit=CaptureFrame("06-headlights-off.png");
            lighting.SetHeadlights(true);yield return new WaitForSecondsRealtime(.15f);
            float lit=CaptureFrame("07-headlights-on.png");
            CheckResult("Headlights illuminate geometry",lit>unlit+.025f,$"asteroid centre brightness: off {unlit:F3}; on {lit:F3}");
            Demo.PilotCamera.transform.localRotation=Quaternion.identity;
            yield return ExplorationChecks();
            yield return new WaitForSecondsRealtime(.5f);
            CheckResult("No runtime errors",report.errors.Count==0,$"errors {report.errors.Count}");
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
            bool passed=report.errors.Count==0;foreach(var check in report.checks)passed&=check.passed;
            Application.Quit(passed?0:1);
        }
        IEnumerator ExplorationChecks()
        {
            var ship=Demo.Ship;var expedition=Demo.Expedition;var world=Demo.World;
            Demo.SetTraining(false);Demo.Hud.SetMenu(false);Demo.Input.enabled=false;
            if(!ship.EngineOn)ship.ToggleEngine();
            yield return new WaitForSeconds(.8f);
            CheckResult("Exploration replaces training gates",!Demo.Route.gameObject.activeInHierarchy && world.LoadedSectorCount>0 && world.LoadedSectorCount<=27,
                $"{world.LoadedSectorCount} sectors resident; rings hidden");
            var key=new SectorAddress(17,-8,31);var layout=world.LayoutSource.Generate(world.Seed,key);
            CheckResult("Sector seed reproducibility",JsonUtility.ToJson(layout)==JsonUtility.ToJson(world.LayoutSource.Generate(world.Seed,key)) &&
                JsonUtility.ToJson(layout)!=JsonUtility.ToJson(world.LayoutSource.Generate(world.Seed+1,key)),"Same address and seed reproduce a layout; another seed changes it");
            bool safe=true,stations=true;
            for(int x=-3;x<=3;x++)for(int z=-3;z<=3;z++)
            {
                var sample=world.LayoutSource.Generate(world.Seed,new SectorAddress(x,2,z));
                for(int i=0;i<sample.Rocks.Length;i++)foreach(var site in sample.Sites)
                    safe&=Vector3.Distance(sample.Rocks[i],site.Position)>=sample.RockSizes[i]+99;
                bool reachable=false;
                for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)for(int dz=-1;dz<=1;dz++)
                    foreach(var site in world.LayoutSource.Generate(world.Seed,new SectorAddress(x+dx,2+dy,z+dz)).Sites)reachable|=site.Kind==SiteKind.Station;
                stations&=reachable;
            }
            CheckResult("Generated routes have clearance and service access",safe && stations,"49 neighborhoods: rock exclusion around sites and a station in each resident neighborhood");
            ExplorationSite wreck=FindSite($"{world.Seed}/0:0:0/0");
            CheckResult("Introductory wreck available",wreck && wreck.Kind==SiteKind.Wreck,"A deterministic first destination exists near launch");
            if(!wreck)yield break;
            expedition.SelectTarget(wreck);PlaceShip(wreck.Position-Vector3.forward*220,Quaternion.identity);
            expedition.ScanRequested=true;yield return new WaitForSeconds(.35f);
            CheckResult("Scanner respects range",!wreck.Scanned && expedition.Progress==0,expedition.Instruction);
            PlaceShip(wreck.Position-Vector3.forward*100,Quaternion.Euler(0,90,0));yield return new WaitForSeconds(.35f);
            CheckResult("Scanner respects pointing",!wreck.Scanned && expedition.Progress==0,expedition.Instruction);
            PlaceShip(wreck.Position-Vector3.forward*100,Quaternion.identity);
            var obstruction=GameObject.CreatePrimitive(PrimitiveType.Cube);obstruction.name="Scanner test occluder";
            obstruction.transform.position=wreck.Position-Vector3.forward*50;obstruction.transform.localScale=new Vector3(20,20,3);
            yield return new WaitForSeconds(.35f);
            CheckResult("Scanner respects occlusion",expedition.Progress==0 && expedition.Instruction.Contains("ПРЕГРАДОЙ"),expedition.Instruction);
            Destroy(obstruction);expedition.ScanRequested=false;yield return null;
            var keys=InputSystem.AddDevice<Keyboard>("StarPilotSurveyKeyboard");injectedKeyboard=keys;Demo.Input.enabled=true;
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.G));yield return new WaitForSeconds(1.2f);
            CheckResult("Keyboard drives physical scanner",expedition.IsScanning && Demo.Cockpit.Controls.Find(c=>c.Action==CockpitAction.Scan).Active,"G drives scanner progress and the cockpit button lamp");
            CaptureFrame("10-survey.png");
            yield return new WaitForSeconds(3.2f);
            CheckResult("Wreck survey reveals cargo",wreck.Scanned && !wreck.Collected && expedition.Save.Cargo==0 && wreck.CargoVisual.gameObject.activeSelf,"Four-second scan reveals recorder without awarding uncollected cargo");
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.X));yield return new WaitForSeconds(.3f);
            CheckResult("Remote salvage is rejected",!wreck.Collected && expedition.Save.Cargo==0,"Must approach within 32 m");
            InputSystem.QueueStateEvent(keys,new KeyboardState());yield return null;yield return null;Demo.Input.enabled=false;
            PlaceShip(wreck.Position-Vector3.forward*24,Quaternion.identity);
            var service=Demo.Cockpit.Controls.Find(c=>c.Action==CockpitAction.Recover);service.Press();
            yield return new WaitForSeconds(1);CaptureFrame("11-recovery.png");yield return new WaitForSeconds(1.8f);service.Release();
            CheckResult("Physical service recovers a black box",wreck.Collected && expedition.Save.Cargo==1 && expedition.Save.CargoValue==180 && expedition.Target.Kind==SiteKind.Station,"Held SERVICE button recovers cargo and selects a return station");
            var saved=ExpeditionSave.Load(expedition.SavePath);
            CheckResult("Exploration progress survives save reload",saved.Cargo==1 && saved.Collected.Contains(wreck.Id) && saved.Scanned.Contains(wreck.Id),"Atomic save contains scanned, collected and cargo states");
            var station=expedition.Target;int credits=expedition.Save.Credits;
            PlaceShip(station.Position-station.transform.forward*10,Quaternion.LookRotation(-station.transform.forward));expedition.InteractRequested=true;
            yield return new WaitForSeconds(.35f);
            CheckResult("Service requires docking alignment",expedition.Save.Cargo==1 && expedition.Progress==0,expedition.Instruction);
            PlaceShip(station.Position-station.transform.forward*10,station.transform.rotation);
            yield return new WaitForSeconds(1);CaptureFrame("12-station.png");yield return new WaitForSeconds(1.3f);expedition.InteractRequested=false;
            CheckResult("Delivery rewards and empties hold",expedition.Save.Cargo==0 && expedition.Save.Credits==credits+180 && ship.Hull==100,"Recorder delivered for 180 CR; cargo cleared and hull serviced");
            expedition.SelectTarget(station);expedition.InteractRequested=true;yield return new WaitForSeconds(2.2f);expedition.InteractRequested=false;
            CheckResult("Delivery cannot pay twice",expedition.Save.Credits==credits+180,"Empty hold servicing does not duplicate credits");
            // The anomaly is a distinct loop: data is scanned directly, without a cargo pickup.
            var anomaly=FindSite($"{world.Seed}/0:0:0/1");expedition.SelectTarget(anomaly);
            PlaceShip(anomaly.Position-Vector3.forward*85,Quaternion.identity);expedition.ScanRequested=true;
            yield return new WaitForSeconds(4.3f);expedition.ScanRequested=false;
            CheckResult("Anomaly yields research data",anomaly.Exhausted && expedition.Save.Data==1 && expedition.Save.Cargo==0,"Data occupies no physical cargo slots");
            CaptureFrame("13-anomaly.png");
            var mineral=FindSite($"{world.Seed}/0:0:0/2");expedition.SelectTarget(mineral);
            PlaceShip(mineral.Position-Vector3.forward*85,Quaternion.identity);
            Demo.Input.enabled=true;
            var scanButton=Demo.Cockpit.Controls.Find(c=>c.Action==CockpitAction.Scan);
            var mouse=InputSystem.AddDevice<Mouse>("StarPilotSurveyMouse");injectedMouse=mouse;
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.LeftAlt));
            InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(0,-466.6667f)});yield return null;yield return null;
            Vector3 screen=Demo.PilotCamera.WorldToScreenPoint(scanButton.transform.position);
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.Tab));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(screen.x,screen.y)});yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(screen.x,screen.y)}.WithButton(MouseButton.Left));
            yield return new WaitForSeconds(1);CaptureFrame("15-survey-controls.png");yield return new WaitForSeconds(3.4f);
            CheckResult("Mouse scans through equipment console",mineral.Scanned && !mineral.Collected,"Tab + held left mouse button on SCAN completes the mineral survey");
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(screen.x,screen.y)});InputSystem.QueueStateEvent(keys,new KeyboardState());
            yield return null;yield return null;InputSystem.RemoveDevice(mouse);Demo.Input.enabled=false;Demo.PilotCamera.transform.localRotation=Quaternion.identity;
            PlaceShip(mineral.Position-Vector3.forward*26,Quaternion.identity);
            expedition.Save.Cargo=3;expedition.InteractRequested=true;yield return new WaitForSeconds(.35f);
            CheckResult("Mineral samples respect cargo capacity",!mineral.Collected && expedition.Progress==0 && expedition.Instruction.Contains("ЗАПОЛНЕН"),"Two-unit sample cannot fit in one remaining cargo slot");
            expedition.Save.Cargo=0;yield return new WaitForSeconds(2.8f);expedition.InteractRequested=false;
            CheckResult("Mineral recovery produces samples",mineral.Collected && expedition.Save.Cargo==2 && expedition.Save.CargoValue==260,"Recovered mineral occupies two slots and is worth 260 CR");
            // Set an account just below the upgrade threshold; deliver real recovered cargo and data.
            expedition.Save.Credits=599;station=expedition.Target;PlaceShip(station.Position-station.transform.forward*10,station.transform.rotation);
            expedition.InteractRequested=true;yield return new WaitForSeconds(2.3f);expedition.InteractRequested=false;
            CheckResult("Mixed delivery upgrades survey equipment",expedition.Save.Credits==979 && expedition.Save.Data==0 && expedition.Save.Cargo==0 && expedition.ScanRange==200 && expedition.Capacity==6,
                "260 CR samples + 120 CR research cross 600 CR threshold: 200 m scanner and 6 cargo slots");
            int finalCredits=expedition.Save.Credits;
            // Switching destinations and returning are available through keyboard and physical controls.
            Demo.Input.enabled=true;InputSystem.QueueStateEvent(keys,new KeyboardState(Key.V));yield return null;yield return null;
            CheckResult("Keyboard selects next signal",expedition.Target && expedition.Target.Kind!=SiteKind.Station,"V selects a remaining survey target");
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.B));yield return null;yield return null;
            CheckResult("Keyboard selects a return station",expedition.Target && expedition.Target.Kind==SiteKind.Station,"B selects nearest service station");
            InputSystem.QueueStateEvent(keys,new KeyboardState());yield return null;yield return null;Demo.Input.enabled=false;InputSystem.RemoveDevice(keys);
            Vector3 far=new Vector3(3500,1200,-2800),velocity=new Vector3(8,3,-4);
            PlaceShip(far,Quaternion.identity);ship.Body.linearVelocity=velocity;world.RebaseIfNeeded();
            Vector3 absolute=new Vector3(world.Origin.X*1000,world.Origin.Y*1000,world.Origin.Z*1000)+ship.Body.position;
            CheckResult("Floating origin preserves position and velocity",Vector3.Distance(absolute,far)<.01f && Vector3.Distance(ship.Body.linearVelocity,velocity)<.01f && !world.Origin.Equals(default(SectorAddress)),
                $"Origin {world.Origin}; local {ship.Body.position}; velocity {ship.Body.linearVelocity}");
            ship.Body.linearVelocity=Vector3.zero;yield return new WaitForSeconds(1);
            CheckResult("Distant sectors replace old sectors",world.LoadedSectorCount<=27 && world.LoadedSectorCount>0 && !FindSite(wreck.Id),$"{world.LoadedSectorCount} resident after travelling several kilometers");
            Demo.ResetFlight();ship.ToggleEngine();yield return new WaitForSeconds(.9f);
            var returned=FindSite($"{world.Seed}/0:0:0/0");
            CheckResult("Collected discoveries stay collected on return",returned && returned.Exhausted && !returned.CargoVisual.gameObject.activeSelf && expedition.Save.Credits==finalCredits,"Unloading and reconstructing the starting sector cannot respawn the reward");
            CaptureFrame("14-exploration-cockpit.png");
        }
        ExplorationSite FindSite(string id)
        {
            foreach(var site in Demo.World.Sites)if(site && site.Id==id)return site;return null;
        }
        void PlaceShip(Vector3 position,Quaternion rotation)
        {
            var ship=Demo.Ship;ship.Translation=ship.Rotation=Vector3.zero;ship.Brake=ship.Boost=false;
            ship.Body.position=position;ship.Body.rotation=rotation;ship.Body.linearVelocity=ship.Body.angularVelocity=Vector3.zero;
            ship.transform.SetPositionAndRotation(position,rotation);Physics.SyncTransforms();
        }
        static IEnumerator Steps(int count) { for(int i=0;i<count;i++)yield return new WaitForFixedUpdate(); }
        float CaptureFrame(string name)
        {
            // Minimized players may skip the swapchain. An explicit render request still renders the scene.
            var target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);
            target.antiAliasing=((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).msaaSampleCount;
            target.Create();
            Canvas canvas=Demo.Hud.DesktopCanvas;
            RenderMode oldMode=canvas.renderMode;Camera oldCamera=canvas.worldCamera;float oldDistance=canvas.planeDistance;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=Demo.PilotCamera;canvas.planeDistance=.2f;
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(Demo.PilotCamera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            RenderTexture previous=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
            Color32[] pixels=image.GetPixels32();int visible=0,samples=0;
            float centreLuma=0;
            for(int y=target.height/2-20;y<target.height/2+20;y++)for(int x=target.width/2-20;x<target.width/2+20;x++)
            {var pixel=pixels[y*target.width+x];centreLuma+=(pixel.r*.2126f+pixel.g*.7152f+pixel.b*.0722f)/(255*1600);}
            if(name=="02-cockpit.png")
            {
                FlightInstrument navigation=null;
                foreach(var instrument in Demo.Ship.GetComponent<CockpitDisplays>().Instruments)
                    if(instrument.Kind==FlightInstrument.Instrument.Navigation)navigation=instrument;
                Vector3 viewport=Demo.PilotCamera.WorldToViewportPoint(navigation.transform.position);
                int centerX=Mathf.RoundToInt(viewport.x*target.width),centerY=Mathf.RoundToInt(viewport.y*target.height),cyanPixels=0;
                for(int y=-65;y<=65;y++)for(int x=-65;x<=65;x++)
                {
                    if(x*x+y*y>65*65)continue;
                    int px=centerX+x,py=centerY+y;if(px<0 || px>=target.width || py<0 || py>=target.height)continue;
                    var pixel=pixels[py*target.width+px];
                    if(pixel.g>40 && pixel.g>pixel.r*1.4f && pixel.b>pixel.r*1.2f)cyanPixels++;
                }
                CheckResult("Navigation screen unobstructed",cyanPixels>90,$"{cyanPixels} illuminated instrument pixels inside central display");
            }
            for(int i=0;i<pixels.Length;i+=37)
            {
                samples++;if(pixels[i].r>15 || pixels[i].g>15 || pixels[i].b>15)visible++;
            }
            CheckResult(name+" rendered",visible>samples*.03f,$"visible sample fraction {visible/(float)samples:F3}");
            File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());
            RenderTexture.active=previous;
            canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldDistance;
            Canvas.ForceUpdateCanvases();target.Release();Destroy(target);Destroy(image);
            return centreLuma;
        }
        void OnDestroy() { Application.logMessageReceived-=OnLog;InputSystem.settings.backgroundBehavior=previousBackgroundBehavior; }
    }
}
