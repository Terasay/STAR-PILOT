using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace StarPilot
{
    public sealed partial class DemoSmokeTest
    {
        IEnumerator ApproachChecks()
        {
            var ship=Demo.Ship;var expedition=Demo.Expedition;var world=Demo.World;var computer=ship.Approach;
            var approachKey=Demo.Cockpit.Controls.Find(c=>c.Action==CockpitAction.Approach);
            var radarKey=Demo.Cockpit.Controls.Find(c=>c.Action==CockpitAction.RadarRange);
            var keys=InputSystem.AddDevice<Keyboard>("StarPilotApproachKeyboard");injectedKeyboard=keys;Demo.Input.enabled=true;
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.Z));yield return null;yield return null;
            CheckResult("Keyboard approach arms physical control",computer.Armed && approachKey.Active,"Z and the physical rocker share one mode and indicator");
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.N));yield return null;yield return null;
            CheckResult("Keyboard changes radar range",expedition.RadarRange==3000 && radarKey.Active,"N selects the 3 km scale and depresses its physical key");
            InputSystem.QueueStateEvent(keys,new KeyboardState());yield return null;yield return null;
            InputSystem.RemoveDevice(keys);Demo.Input.enabled=false;
            radarKey.Press();bool shortRange=expedition.RadarRange==500;radarKey.Press();
            CheckResult("Physical radar scale cycles",shortRange && expedition.RadarRange==1500,"Physical key cycles through 0.5, 1.5 and 3 km");
            approachKey.Press();CheckResult("Physical approach can disengage",!computer.Armed && !approachKey.Active,"Rocker disengages the same flight constraint");

            // Isolate trajectories from generated obstacles; exercise the actual Rigidbody and held thrust.
            Vector3 returnPosition=ship.Body.position;Quaternion returnRotation=ship.Body.rotation;
            world.SetExploration(false);
            var fixture=new GameObject("Approach test target").AddComponent<ExplorationSite>();
            fixture.Id="approach-test";fixture.Kind=SiteKind.Relay;fixture.transform.position=new Vector3(10000,10000,10300);
            expedition.SelectTarget(fixture);expedition.ScanRequested=expedition.InteractRequested=false;
            foreach(var control in Demo.Cockpit.Controls)control.Release();
            if(!ship.EngineOn)ship.ToggleEngine();if(ship.AssistOn)ship.ToggleAssist();
            PlaceShip(fixture.Position-Vector3.forward*300,Quaternion.identity);
            ship.Body.linearVelocity=Vector3.forward*65;ship.Translation=Vector3.forward;ship.Boost=true;computer.SetArmed(true);
            float nearest=float.PositiveInfinity;
            for(int i=0;i<600;i++)
            {
                yield return new WaitForFixedUpdate();nearest=Mathf.Min(nearest,Vector3.Distance(ship.Body.position,fixture.Position));
            }
            computer.Refresh();
            CheckResult("Approach arrests held boosted thrust",computer.Distance>=computer.Standoff && computer.Distance<computer.Standoff+6 && Mathf.Abs(computer.ClosingSpeed)<.7f && nearest>=computer.Standoff,
                $"65 m/s entry with boost held: stopped at {computer.Distance:F1} m, closest {nearest:F1} m, closing {computer.ClosingSpeed:F2} m/s");
            ship.Boost=false;ship.Translation=Vector3.right;ship.Body.linearVelocity=Vector3.right*8;
            yield return Steps(25);
            CheckResult("Approach preserves manual lateral flight",ship.Body.linearVelocity.x>10 && Vector3.Dot(ship.transform.forward,Vector3.forward)>.999f,"Lateral thrusters accelerate without automatic rotation towards the site");

            computer.SetArmed(false);PlaceShip(fixture.Position-Vector3.forward*180,Quaternion.identity);ship.Body.linearVelocity=Vector3.forward*25;
            yield return Steps(25);
            CheckResult("Disengaged approach preserves drift",Mathf.Abs(ship.Speed-25)<.15f,"Assist OFF and APPROACH OFF retain translational inertia");
            computer.SetArmed(true);ship.ToggleEngine();yield return Steps(25);
            CheckResult("Unpowered approach cannot brake",!computer.Active && Mathf.Abs(ship.Speed-25)<.15f,"Armed approach does not apply force with propulsion off");
            ship.ToggleEngine();Demo.Hud.SetMenu(true);Vector3 paused=ship.Body.position;yield return new WaitForSecondsRealtime(.25f);
            CheckResult("Approach respects pause",!computer.Active && Vector3.Distance(paused,ship.Body.position)<.01f,"Menu pause prevents both piloting and approach forces");Demo.Hud.SetMenu(false);

            fixture.Kind=SiteKind.Archive;fixture.Scanned=true;
            PlaceShip(fixture.Position-Vector3.forward*110,Quaternion.identity);ship.Body.linearVelocity=Vector3.forward*30;ship.Translation=Vector3.forward;
            yield return Steps(400);computer.Refresh();
            CheckResult("Approach reaches archive recovery envelope",computer.Distance>=25 && computer.Distance<32 && ship.Speed<5,"Scanned archive approach ends within actual recovery distance and speed limits");
            fixture.Kind=SiteKind.Station;
            PlaceShip(fixture.Position-Vector3.forward*100,Quaternion.identity);ship.Body.linearVelocity=Vector3.forward*25;ship.Translation=Vector3.forward;
            yield return Steps(400);computer.Refresh();
            CheckResult("Approach reaches station service envelope",computer.Distance>=12 && computer.Distance<26 && ship.Speed<3,"Station approach ends within service limits while alignment stays manual");
            fixture.Kind=SiteKind.Anomaly;
            PlaceShip(fixture.Position-Vector3.forward*200,Quaternion.identity);ship.Body.linearVelocity=Vector3.forward*35;ship.Translation=Vector3.forward;
            yield return Steps(400);computer.Refresh();
            CheckResult("Approach keeps anomaly observation clearance",computer.Distance>55 && computer.Distance<expedition.ScanRange && ship.Speed<12,"Anomaly standoff remains outside the parallax exclusion radius and inside scanner range");
            fixture.gameObject.SetActive(false);Destroy(fixture.gameObject);
            PlaceShip(returnPosition,returnRotation);world.SetExploration(true);expedition.FocusMission();yield return new WaitForSeconds(.6f);
            PlaceShip(expedition.Target.Position-Vector3.forward*125,Quaternion.identity);ship.Body.linearVelocity=Vector3.forward*18;ship.Translation=Vector3.forward;
            yield return new WaitForSeconds(.2f);CaptureFrame("20-approach-computer.png");
            computer.SetArmed(false);PlaceShip(expedition.Target.Position-Vector3.forward*160,Quaternion.identity);ship.Body.linearVelocity=Vector3.forward*80;
            yield return new WaitForSeconds(.15f);
            CheckResult("Unsafe approach warns on the cockpit instrument",computer.TooFast && ship.GetComponent<CockpitDisplays>().ApproachWarning.text.Contains("ТОРМОЗИТЕ"),"Actual closing velocity and stopping distance trigger the braking advisory");
            CaptureFrame("21-braking-advisory.png");
            PlaceShip(expedition.Target.Position-Vector3.forward*140,Quaternion.identity);computer.SetArmed(true);
            Demo.PilotCamera.transform.localRotation=Quaternion.Euler(45,0,0);yield return new WaitForSeconds(.2f);CaptureFrame("22-navigation-controls.png");Demo.PilotCamera.transform.localRotation=Quaternion.identity;
            CheckResult("Approach remains a physical cockpit instrument",ship.GetComponent<CockpitDisplays>().ApproachPanel.GetComponentInParent<Canvas>().renderMode==RenderMode.WorldSpace && Demo.Hud.FlightOverlayHidden,"No flight HUD or windshield overlay added");
            Demo.ResetFlight();CheckResult("Reset disengages approach",!computer.Armed,"Reset cannot retain a powered approach to an old target");
            Demo.SetTraining(true);approachKey.Press();
            CheckResult("Training cannot arm site approach",!computer.Armed,"Ring training keeps its original manual flight and attitude display");
            Demo.SetTraining(false);
        }
    }
}
