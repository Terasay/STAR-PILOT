using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace StarPilot
{
    public sealed class XrFlightMode : MonoBehaviour
    {
        public DemoBootstrap Demo;
        public bool Running { get; private set; }
        public bool Starting { get; private set; }
        Transform origin;
        TrackedPoseDriver pose;
        readonly List<XRInputSubsystem> inputs=new List<XRInputSubsystem>();

        public void StartVr() { if(!Running && !Starting)StartCoroutine(Initialize()); }
        IEnumerator Initialize()
        {
            Starting=true;
            Demo.Cockpit.PrepareXrActions();
            Demo.Hud.ShowVrMessage("Подключение к OpenXR…");
            // Register pose actions before OpenXR creates its immutable action set.
            var headPosition=new UnityEngine.InputSystem.InputAction("Head position",UnityEngine.InputSystem.InputActionType.Value,"<XRHMD>/centerEyePosition",expectedControlType:"Vector3");
            var headRotation=new UnityEngine.InputSystem.InputAction("Head rotation",UnityEngine.InputSystem.InputActionType.Value,"<XRHMD>/centerEyeRotation",expectedControlType:"Quaternion");
            var headTracking=new UnityEngine.InputSystem.InputAction("Head tracking",UnityEngine.InputSystem.InputActionType.Value,"<XRHMD>/trackingState",expectedControlType:"Integer");
            headPosition.Enable();headRotation.Enable();headTracking.Enable();
            var settings=XRGeneralSettings.Instance;
            if(settings==null || settings.Manager==null)
            {
                headPosition.Dispose();headRotation.Dispose();headTracking.Dispose();
                Fail("OpenXR не настроен: Unity → STAR PILOT → Configure PC OpenXR.");yield break;
            }
            yield return settings.Manager.InitializeLoader();
            if(settings.Manager.activeLoader==null)
            {
                headPosition.Dispose();headRotation.Dispose();headTracking.Dispose();
                Fail("OpenXR runtime / шлем не обнаружен. Доступен обычный режим.");yield break;
            }
            settings.Manager.StartSubsystems();
            var displays=new List<XRDisplaySubsystem>();SubsystemManager.GetSubsystems(displays);
            bool active=false;foreach(var display in displays)active|=display.running;
            if(!active)
            {
                settings.Manager.StopSubsystems();settings.Manager.DeinitializeLoader();
                headPosition.Dispose();headRotation.Dispose();headTracking.Dispose();
                Fail("Шлем не запущен. Проверь PC OpenXR runtime и подключение.");yield break;
            }
            SubsystemManager.GetSubsystems(inputs);
            foreach(var input in inputs)input.TrySetTrackingOriginMode(TrackingOriginModeFlags.Device);
            var camera=Demo.PilotCamera;
            origin=new GameObject("Seated XR origin").transform;origin.SetParent(Demo.Ship.transform,false);
            origin.localPosition=new Vector3(0,1.62f,-.25f);
            camera.transform.SetParent(origin,false);camera.transform.localPosition=Vector3.zero;camera.transform.localRotation=Quaternion.identity;
            // OpenXR tracking uses Input System pose actions, updated immediately before rendering.
            pose=camera.gameObject.AddComponent<TrackedPoseDriver>();
            pose.positionInput=new UnityEngine.InputSystem.InputActionProperty(headPosition);
            pose.rotationInput=new UnityEngine.InputSystem.InputActionProperty(headRotation);
            pose.trackingStateInput=new UnityEngine.InputSystem.InputActionProperty(headTracking);
            pose.positionInput.action.Enable();pose.rotationInput.action.Enable();pose.trackingStateInput.action.Enable();
            foreach(var input in inputs)input.TryRecenter();
            Demo.PilotCamera.GetUniversalAdditionalCameraDataSafe(false);
            Demo.Input.IsVr=true;Running=true;Starting=false;
            Demo.Hud.OnVrStarted();
        }

        void Fail(string message) { Starting=false;Demo.Hud.ShowVrMessage(message);Debug.LogWarning("[STAR PILOT] "+message); }

        void OnDestroy()
        {
            if(pose)
            {
                pose.positionInput.action?.Dispose();pose.rotationInput.action?.Dispose();pose.trackingStateInput.action?.Dispose();
            }
            if(Running && XRGeneralSettings.Instance && XRGeneralSettings.Instance.Manager)
            {
                XRGeneralSettings.Instance.Manager.StopSubsystems();XRGeneralSettings.Instance.Manager.DeinitializeLoader();
            }
        }
    }

    static class CameraVrSettings
    {
        public static void GetUniversalAdditionalCameraDataSafe(this Camera camera,bool postProcessing)
        {
            var data=camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if(data){data.renderPostProcessing=postProcessing;data.antialiasing=UnityEngine.Rendering.Universal.AntialiasingMode.None;}
        }
    }
}
