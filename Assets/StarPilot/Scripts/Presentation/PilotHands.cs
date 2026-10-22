using System.Collections.Generic;
using UnityEngine;

namespace StarPilot
{
    /// <summary>Continuous cloth gloves, three joints per digit, shared by desktop and tracked controllers.</summary>
    public sealed class PilotHands : MonoBehaviour
    {
        public CockpitInteraction Interaction;
        public Transform Left,Right,LeftArm,RightArm,LeftRest,RightRest;
        public Transform[] LeftFingers,RightFingers;
        PressureGlove leftGlove,rightGlove;
        readonly List<Mesh> meshes=new List<Mesh>();
        public void Build(Geometry g,Transform cockpit,Material suit,Material rubber,Material metal,Material light)
        {
            var fabric=g.Material("Gloves / matte woven protective fabric",new Color(.26f,.245f,.205f),0,.08f);
            g.DetailMaterial(fabric,"cotton_jersey",9,1.15f);
            fabric.SetFloat("_VertexTint",1);
            fabric.SetFloat("_Fabric",1);
            leftGlove=new PressureGlove(cockpit,-1,fabric,meshes);
            rightGlove=new PressureGlove(cockpit,1,fabric,meshes);
            Left=leftGlove.Root;Right=rightGlove.Root;
            LeftFingers=leftGlove.Fingers;RightFingers=rightGlove.Fingers;
            LeftArm=Arm(g,cockpit,suit,rubber);RightArm=Arm(g,cockpit,suit,rubber);
        }
        static Transform Arm(Geometry g,Transform parent,Material suit,Material rubber)
        {
            var arm=new GameObject("Pressure suit / tailored forearm").transform;arm.SetParent(parent,false);
            g.PressureSleeve(arm,suit);
            // The sleeve mesh carries its own sewn seams and elastic cuff.
            return arm;
        }
        void LateUpdate()
        {
            if(!Interaction || !Interaction.Input)return;
            PoseHand(leftGlove,LeftArm,LeftRest,-1);
            PoseHand(rightGlove,RightArm,RightRest,1);
        }
        void PoseHand(PressureGlove glove,Transform arm,Transform rest,int side)
        {
            bool vr=Interaction.Input.IsVr;
            bool tracked=Interaction.TryHandPose(side,out Vector3 p,out Quaternion q,out float grip);
            var hand=glove.Root;
            hand.gameObject.SetActive(!vr || tracked);arm.gameObject.SetActive(!vr || tracked);
            if(vr && tracked)hand.SetPositionAndRotation(p,q*Quaternion.Euler(25,0,0));
            else {hand.SetPositionAndRotation(rest.position,rest.rotation);grip=side<0?.70f:.83f;}
            glove.Pose(grip);
            Vector3 elbow=transform.TransformPoint(new Vector3(side*.36f,.78f,-.05f));
            Vector3 wrist=hand.TransformPoint(new Vector3(0,0,-.091f));
            arm.position=(elbow+wrist)*.5f;arm.rotation=Quaternion.LookRotation(wrist-elbow,transform.up);
            arm.localScale=new Vector3(.95f,.95f,Vector3.Distance(elbow,wrist));
        }
        void OnDestroy(){foreach(var mesh in meshes)if(mesh)Destroy(mesh);}
    }
}
