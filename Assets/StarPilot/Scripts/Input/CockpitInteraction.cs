using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages=UnityEngine.XR.CommonUsages;

namespace StarPilot
{
    [DefaultExecutionOrder(50)]
    public sealed class CockpitInteraction : MonoBehaviour
    {
        public PilotInput Input;
        public ShipFlightController Ship;
        public Transform Stick,Throttle,StickGrip,ThrottleGrip;
        public readonly List<CockpitControl> Controls=new List<CockpitControl>();
        public string Hint {get;private set;}
        public bool StickHeld {get;private set;}
        public bool ThrottleHeld {get;private set;}
        Quaternion grabRotation;
        Vector3 throttleStart;
        float throttleValue;
        CockpitControl mouseControl,leftControl,rightControl;
        bool leftWasGrip,rightWasGrip,leftWasTrigger,rightWasTrigger;
        InputActionMap poses;
        InputAction lp,lr,rp,rr,lt,rt;

        // OpenXR action sets must be registered before loader initialization.
        public void PrepareXrActions()
        {
            if(poses!=null)return;
            poses=new InputActionMap("Cockpit tracked hands");
            lp=poses.AddAction("Left position",InputActionType.Value,"<XRController>{LeftHand}/devicePosition");
            lr=poses.AddAction("Left rotation",InputActionType.Value,"<XRController>{LeftHand}/deviceRotation");
            rp=poses.AddAction("Right position",InputActionType.Value,"<XRController>{RightHand}/devicePosition");
            rr=poses.AddAction("Right rotation",InputActionType.Value,"<XRController>{RightHand}/deviceRotation");
            lt=poses.AddAction("Left tracking",InputActionType.Value,"<XRController>{LeftHand}/isTracked");
            rt=poses.AddAction("Right tracking",InputActionType.Value,"<XRController>{RightHand}/isTracked");
            foreach(string hand in new[]{"LeftHand","RightHand"})
            foreach(string control in new[]{"grip","trigger","primary2DAxis","primaryButton","secondaryButton","menuButton"})
                poses.AddAction(hand+control,InputActionType.PassThrough,"<XRController>{"+hand+"}/"+control);
            poses.Enable();
        }
        public bool TryHandPose(int side,out Vector3 position,out Quaternion rotation,out float grip)
        {
            position=Vector3.zero;rotation=Quaternion.identity;grip=0;
            if(!Input || !Input.IsVr || poses==null)return false;
            var tracked=side<0?lt:rt;
            if(tracked.ReadValue<float>()<.5f)return false;
            Vector3 p=(side<0?lp:rp).ReadValue<Vector3>();Quaternion r=(side<0?lr:rr).ReadValue<Quaternion>();
            Transform origin=Input.View.parent;
            position=origin.TransformPoint(p);rotation=origin.rotation*r;
            InputDevices.GetDeviceAtXRNode(side<0?XRNode.LeftHand:XRNode.RightHand).TryGetFeatureValue(CommonUsages.grip,out grip);
            return true;
        }
        void Update()
        {
            Hint="";
            if(!Input || Input.InMenu || !Input.enabled){ReleaseAll();return;}
            if(Input.IsVr)ReadHands();else ReadMouse();
            foreach(var control in Controls)
            {
                if(control.Action==CockpitAction.Brake)Ship.Brake|=control.Held;
                if(control.Action==CockpitAction.Boost)Ship.Boost|=control.Held;
            }
        }
        void ReadMouse()
        {
            if(!Input.CockpitPointer){if(mouseControl)mouseControl.Release();mouseControl=null;return;}
            if(Mouse.current==null)return;
            var ray=Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            CockpitControl hovered=null;float nearest=float.PositiveInfinity;
            foreach(var hit in Physics.RaycastAll(ray,3f,~0,QueryTriggerInteraction.Collide))
            {
                var c=hit.collider.GetComponent<CockpitControl>();
                if(c && hit.distance<nearest){nearest=hit.distance;hovered=c;}
            }
            if(hovered)Hint=hovered.Action==CockpitAction.Engine?"ЛКМ · ДВИГАТЕЛЬ":hovered.Action==CockpitAction.Assist?"ЛКМ · СТАБИЛИЗАЦИЯ":hovered.Action==CockpitAction.Brake?"УДЕРЖИВАЙ ЛКМ · ТОРМОЗ":"УДЕРЖИВАЙ ЛКМ · УСКОРЕНИЕ";
            else Hint="TAB · ПАНЕЛЬ УПРАВЛЕНИЯ";
            if(Mouse.current.leftButton.wasPressedThisFrame && hovered){mouseControl=hovered;hovered.Press();}
            if(!Mouse.current.leftButton.isPressed && mouseControl){mouseControl.Release();mouseControl=null;}
        }
        void ReadHands()
        {
            bool lValid=TryHandPose(-1,out var lpos,out var lrot,out float lgrip);
            bool rValid=TryHandPose(1,out var rpos,out var rrot,out float rgrip);
            var left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            left.TryGetFeatureValue(CommonUsages.trigger,out float ltrigger);right.TryGetFeatureValue(CommonUsages.trigger,out float rtrigger);
            if(rValid && rgrip>.55f && !rightWasGrip && Vector3.Distance(rpos,StickGrip.position)<.19f)
            {StickHeld=true;grabRotation=Quaternion.Inverse(transform.rotation)*rrot;}
            if(lValid && lgrip>.55f && !leftWasGrip && Vector3.Distance(lpos,ThrottleGrip.position)<.19f)
            {ThrottleHeld=true;throttleStart=transform.InverseTransformPoint(lpos);throttleValue=Ship.Translation.z;}
            if(!rValid || rgrip<.25f)StickHeld=false;
            if(!lValid || lgrip<.25f)ThrottleHeld=false;
            if(StickHeld)
            {
                Quaternion relative=Quaternion.Inverse(grabRotation)*(Quaternion.Inverse(transform.rotation)*rrot);
                Vector3 a=relative.eulerAngles;
                Ship.Rotation=new Vector3(Mathf.Clamp(Mathf.DeltaAngle(0,a.x)/22,-1,1),Mathf.Clamp(Mathf.DeltaAngle(0,a.z)/-22,-1,1),Ship.Rotation.z);
            }
            if(ThrottleHeld)
            {
                float value=throttleValue+(transform.InverseTransformPoint(lpos).z-throttleStart.z)/.22f;
                Ship.Translation=new Vector3(Ship.Translation.x,Ship.Translation.y,Mathf.Clamp(value,-1,1));
            }
            if(lValid && !ThrottleHeld)ReadSwitch(lpos,ltrigger,ref leftWasTrigger,ref leftControl);else Release(ref leftControl);
            if(rValid && !StickHeld)ReadSwitch(rpos,rtrigger,ref rightWasTrigger,ref rightControl);else Release(ref rightControl);
            if(leftControl || rightControl)Ship.Translation=new Vector3(Ship.Translation.x,0,Ship.Translation.z);
            leftWasGrip=lgrip>.55f;rightWasGrip=rgrip>.55f;
        }
        void ReadSwitch(Vector3 hand,float trigger,ref bool wasTrigger,ref CockpitControl held)
        {
            // Direct interaction: bring a controller to the switch, then squeeze the index trigger.
            if(trigger>.6f && !wasTrigger)
            {
                float best=.13f;CockpitControl nearest=null;
                foreach(var c in Controls){float d=Vector3.Distance(hand,c.transform.position);if(d<best){best=d;nearest=c;}}
                if(nearest){held=nearest;nearest.Press();}
            }
            if(trigger<.25f)Release(ref held);
            wasTrigger=trigger>.6f;
        }
        static void Release(ref CockpitControl c){if(c)c.Release();c=null;}
        void ReleaseAll(){Release(ref mouseControl);Release(ref leftControl);Release(ref rightControl);StickHeld=ThrottleHeld=false;leftWasGrip=rightWasGrip=leftWasTrigger=rightWasTrigger=false;}
        void OnDisable(){ReleaseAll();}
        void OnDestroy(){poses?.Dispose();}
    }
}
