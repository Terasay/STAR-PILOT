using UnityEngine;

namespace StarPilot
{
    public sealed class ResearchSiteAnimation : MonoBehaviour
    {
        public Transform Receiver;
        void Update(){if(Receiver)Receiver.localRotation=Quaternion.Euler(-15,Time.time*12,0);}
    }
}
