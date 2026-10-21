using UnityEngine;

namespace StarPilot
{
    public sealed class FlightRoute : MonoBehaviour
    {
        public ShipFlightController Ship;
        public Transform[] Gates;
        [System.NonSerialized] public Renderer[][] GateLights;
        public Material ActiveMaterial, InactiveMaterial, CompleteMaterial;
        public int Current { get; private set; }
        public bool Completed => Gates != null && Current >= Gates.Length;
        public Vector3 Target => Completed ? Gates[Gates.Length - 1].position : Gates[Current].position;
        public float Distance => Vector3.Distance(Ship.transform.position, Target);
        public string Message { get; private set; } = "TRAINING CORRIDOR / READY";
        Vector3 previousPosition;

        public void Restart()
        {
            Current = 0;
            previousPosition = Ship.transform.position;
            Message = "TRAINING CORRIDOR / READY";
            RefreshLights();
        }

        void Start() { Restart(); }
        void Update()
        {
            Vector3 position = Ship.transform.position;
            if (!Completed && Ship.EngineOn)
            {
                Transform gate = Gates[Current];
                Vector3 a = gate.InverseTransformPoint(previousPosition);
                Vector3 b = gate.InverseTransformPoint(position);
                // Sweep the gate plane, so a fast ship cannot skip a checkpoint between frames.
                if (a.z < 0f && b.z >= 0f)
                {
                    Vector3 crossing = Vector3.Lerp(a, b, -a.z / (b.z - a.z));
                    if (new Vector2(crossing.x, crossing.y).magnitude < 15f)
                    {
                        Current++;
                        Message = Completed ? "CORRIDOR COMPLETE / FREE FLIGHT" : "CHECKPOINT CONFIRMED";
                        RefreshLights();
                    }
                }
            }
            previousPosition = position;
        }

        void RefreshLights()
        {
            if (GateLights == null) return;
            for (int i = 0; i < GateLights.Length; i++)
                foreach (Renderer r in GateLights[i])
                    r.sharedMaterial = i < Current ? CompleteMaterial : i == Current ? ActiveMaterial : InactiveMaterial;
        }
    }
}
