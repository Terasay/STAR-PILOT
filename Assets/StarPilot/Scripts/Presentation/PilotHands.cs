using UnityEngine;

namespace StarPilot
{
    /// <summary>Original articulated suit gloves. The same model follows the controls or tracked controllers.</summary>
    public sealed class PilotHands : MonoBehaviour
    {
        public CockpitInteraction Interaction;
        public Transform Left, Right, LeftArm, RightArm, LeftRest, RightRest;
        public Transform[] LeftFingers, RightFingers;
        public void Build(Geometry g,Transform cockpit,Material suit,Material rubber,Material metal,Material light)
        {
            Left=Glove(g,cockpit,-1,suit,rubber,metal,light,out LeftFingers);
            Right=Glove(g,cockpit,1,suit,rubber,metal,light,out RightFingers);
            LeftArm=Arm(g,cockpit,suit,rubber,metal,light);RightArm=Arm(g,cockpit,suit,rubber,metal,light);
        }
        static Transform Glove(Geometry g,Transform parent,int side,Material suit,Material rubber,Material metal,Material light,out Transform[] fingers)
        {
            var hand=new GameObject(side<0?"Left pilot glove":"Right pilot glove").transform;hand.SetParent(parent,false);
            g.Sphere("Pressure glove / palm",hand,Vector3.zero,new Vector3(.115f,.07f,.155f),suit);
            g.Sphere("Padded glove back",hand,new Vector3(0,.025f,-.006f),new Vector3(.112f,.044f,.123f),suit);
            g.Panel("Glove reinforcement",hand,new Vector3(0,.045f,-.016f),new Vector3(.071f,.012f,.07f),suit,new Vector3(8,0,0),.018f);
            g.Sphere("Wrist seal",hand,new Vector3(0,0,-.105f),new Vector3(.135f,.095f,.085f),rubber);
            g.Panel("Wrist instrument",hand,new Vector3(0,.04f,-.105f),new Vector3(.095f,.025f,.06f),suit,default,.018f);
            g.Box("Wrist life support",hand,new Vector3(0,.05f,-.105f),new Vector3(.07f,.008f,.03f),rubber);
            for(int j=0;j<3;j++)g.Box("Wrist indicator",hand,new Vector3(-.022f+j*.022f,.055f,-.105f),new Vector3(.012f,.004f,.019f),light);
            fingers=new Transform[5];
            for(int j=0;j<4;j++)
            {
                var joint=new GameObject("Finger "+j+" / flexion").transform;joint.SetParent(hand,false);
                joint.localPosition=new Vector3((j-1.5f)*.027f,0,.052f);fingers[j]=joint;
                float length=j==0?.044f:j==3?.039f:.05f;
                g.Sphere("Knuckle",joint,Vector3.zero,new Vector3(.029f,.034f,.034f),suit);
                g.Sphere("Proximal finger",joint,new Vector3(0,0,length*.5f),new Vector3(.026f,.032f,length+.016f),suit);
                var tip=new GameObject("Curled finger tip").transform;tip.SetParent(joint,false);tip.localPosition=new Vector3(0,0,length);tip.localRotation=Quaternion.Euler(65,0,0);
                g.Sphere("Glove fingertip",tip,new Vector3(0,0,.02f),new Vector3(.026f,.031f,.052f),rubber);
                g.Box("Knuckle seam",joint,new Vector3(0,.018f,.012f),new Vector3(.02f,.003f,.008f),metal);
            }
            var thumb=new GameObject("Thumb / flexion").transform;thumb.SetParent(hand,false);thumb.localPosition=new Vector3(-side*.061f,-.008f,0);
            thumb.localRotation=Quaternion.Euler(20,-side*45,0);fingers[4]=thumb;
            g.Sphere("Thumb",thumb,new Vector3(0,0,.025f),new Vector3(.035f,.036f,.075f),suit);
            return hand;
        }
        static Transform Arm(Geometry g,Transform parent,Material suit,Material rubber,Material metal,Material light)
        {
            var arm=new GameObject("Pressure suit / forearm").transform;arm.SetParent(parent,false);
            g.Sphere("Sleeve",arm,Vector3.zero,new Vector3(.15f,.14f,1),rubber);
            g.Sphere("Padded pressure sleeve",arm,new Vector3(0,.017f,0),new Vector3(.145f,.125f,.88f),suit);
            g.Panel("Forearm armour",arm,new Vector3(0,.07f,.10f),new Vector3(.095f,.014f,.42f),suit,default,.024f);
            for(int i=0;i<5;i++)g.Box("Flexion seam",arm,new Vector3(0,.066f,-.28f+i*.09f),new Vector3(.11f,.007f,.014f),rubber);
            return arm;
        }
        void LateUpdate()
        {
            if(!Interaction || !Interaction.Input)return;
            PoseHand(Left,LeftArm,LeftRest,LeftFingers,-1);
            PoseHand(Right,RightArm,RightRest,RightFingers,1);
        }
        void PoseHand(Transform hand,Transform arm,Transform rest,Transform[] fingers,int side)
        {
            bool vr=Interaction.Input.IsVr;
            bool tracked=Interaction.TryHandPose(side,out Vector3 p,out Quaternion q,out float grip);
            hand.gameObject.SetActive(!vr || tracked);arm.gameObject.SetActive(!vr || tracked);
            if(vr && tracked){hand.SetPositionAndRotation(p,q*Quaternion.Euler(25,0,0));}
            else {hand.SetPositionAndRotation(rest.position,rest.rotation);grip=.8f;}
            for(int i=0;i<4;i++)fingers[i].localRotation=Quaternion.Euler(Mathf.Lerp(-8,68,grip),0,0);
            Vector3 elbow=transform.TransformPoint(new Vector3(side*.36f,.78f,-.05f));
            Vector3 wrist=hand.TransformPoint(new Vector3(0,0,-.13f));
            arm.position=(elbow+wrist)*.5f;arm.rotation=Quaternion.LookRotation(wrist-elbow,transform.up);
            arm.localScale=new Vector3(1,1,Vector3.Distance(elbow,wrist));
        }
    }
}
