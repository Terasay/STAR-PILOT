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
        string output;
        void Awake() { Application.logMessageReceived+=OnLog; }
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
            Demo.Hud.SetMenu(false);Demo.Input.enabled=false;
            var ship=Demo.Ship;
            CheckResult("Scene initialized",Demo.Route.Gates.Length==4 && Camera.main==Demo.PilotCamera,"4 gates, pilot camera, live cockpit");
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
            InputSystem.RemoveDevice(testKeyboard);Demo.Input.enabled=false;
            Demo.ResetFlight();ship.ToggleEngine();
            yield return Steps(5);
            ship.Translation=Vector3.forward;yield return Steps(30);ship.Translation=Vector3.zero;
            CaptureFrame("02-cockpit.png");
            Demo.PilotCamera.transform.localRotation=Quaternion.Euler(27,-22,0);
            CaptureFrame("03-controls.png");
            Demo.PilotCamera.transform.localRotation=Quaternion.Euler(-8,22,0);
            CaptureFrame("04-space.png");
            Demo.PilotCamera.transform.localRotation=Quaternion.identity;
            yield return new WaitForSecondsRealtime(.5f);
            CheckResult("No runtime errors",report.errors.Count==0,$"errors {report.errors.Count}");
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
            bool passed=report.errors.Count==0;foreach(var check in report.checks)passed&=check.passed;
            Application.Quit(passed?0:1);
        }
        static IEnumerator Steps(int count) { for(int i=0;i<count;i++)yield return new WaitForFixedUpdate(); }
        void CaptureFrame(string name)
        {
            // Minimized players may skip the swapchain. An explicit render request still renders the scene.
            var target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);
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
        }
        void OnDestroy() { Application.logMessageReceived-=OnLog; }
    }
}
