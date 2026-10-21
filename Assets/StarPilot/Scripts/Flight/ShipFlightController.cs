using System;
using UnityEngine;

namespace StarPilot
{
    /// <summary>Equipment-independent six-axis flight. All acceleration goes through PhysX.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ShipFlightController : MonoBehaviour
    {
        [Header("Flight envelope (metres, seconds)")]
        public float forwardAcceleration = 12f;
        public float lateralAcceleration = 7f;
        public float cruiseSpeed = 65f;
        public float boostSpeed = 110f;
        public float brakingAcceleration = 20f;
        public float assistAcceleration = 5f;
        public float angularSpeed = 0.65f;
        public float angularResponse = 3.5f;

        public Rigidbody Body { get; private set; }
        public bool EngineOn { get; private set; }
        public bool AssistOn { get; private set; } = true;
        public float Hull { get; private set; } = 100f;
        public Vector3 Translation { get; set; }
        public Vector3 Rotation { get; set; }
        public bool Brake { get; set; }
        public bool Boost { get; set; }
        public bool ControlsEnabled { get; set; } = true;
        public float Speed => Body ? Body.linearVelocity.magnitude : 0f;
        public float ForwardSpeed => Body ? Vector3.Dot(Body.linearVelocity, transform.forward) : 0f;
        public float Thrust => EngineOn && ControlsEnabled ? Mathf.Clamp01(Translation.magnitude) : 0f;
        public event Action<float, Vector3> Impact;
        public event Action StateChanged;

        Vector3 spawnPosition;
        Quaternion spawnRotation;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.useGravity = false;
            Body.mass = 1800f;
            Body.linearDamping = 0f;
            Body.angularDamping = 0f;
            Body.maxAngularVelocity = 1.2f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        public void ToggleEngine()
        {
            if (Hull <= 0f) return;
            EngineOn = !EngineOn;
            StateChanged?.Invoke();
        }
        public void ToggleAssist() { AssistOn = !AssistOn; StateChanged?.Invoke(); }

        public void ResetShip()
        {
            Body.position = spawnPosition;
            Body.rotation = spawnRotation;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Translation = Rotation = Vector3.zero;
            Brake = Boost = false;
            Hull = 100f;
            EngineOn = false;
            AssistOn = true;
            StateChanged?.Invoke();
        }

        void FixedUpdate()
        {
            if (!EngineOn || !ControlsEnabled || Hull <= 0f) return;

            Vector3 command = Vector3.ClampMagnitude(Translation, 1f);
            float limit = Boost ? boostSpeed : cruiseSpeed;
            Vector3 localVelocity = transform.InverseTransformDirection(Body.linearVelocity);
            Vector3 acceleration = new Vector3(command.x * lateralAcceleration,
                command.y * lateralAcceleration, command.z * forwardAcceleration * (Boost ? 1.8f : 1f));

            // Limit powered acceleration along travel, without erasing collision impulses or drift.
            if (Speed >= limit && Vector3.Dot(acceleration, localVelocity) > 0f)
                acceleration -= Vector3.Project(acceleration, localVelocity);

            Body.AddRelativeForce(acceleration, ForceMode.Acceleration);
            if (Brake)
                Body.AddForce(Vector3.ClampMagnitude(-Body.linearVelocity / Time.fixedDeltaTime,
                    brakingAcceleration), ForceMode.Acceleration);
            else if (AssistOn)
            {
                // Idle axes are gently arrested by manoeuvring thrusters. Assist OFF preserves drift.
                Vector3 correction = new Vector3(Mathf.Abs(command.x) < 0.01f ? -localVelocity.x : 0f,
                    Mathf.Abs(command.y) < 0.01f ? -localVelocity.y : 0f,
                    Mathf.Abs(command.z) < 0.01f ? -localVelocity.z : 0f);
                Body.AddRelativeForce(Vector3.ClampMagnitude(correction * 0.8f, assistAcceleration), ForceMode.Acceleration);
            }

            Vector3 targetAngularVelocity = transform.TransformDirection(Vector3.ClampMagnitude(Rotation, 1f)) * angularSpeed;
            Vector3 angularCorrection = (targetAngularVelocity - Body.angularVelocity) * angularResponse;
            Body.AddTorque(angularCorrection, ForceMode.Acceleration);
        }

        void OnCollisionEnter(Collision collision)
        {
            float severity = Mathf.Max(0f, collision.relativeVelocity.magnitude - 2f);
            if (severity <= 0f) return;
            Hull = Mathf.Max(0f, Hull - severity * 1.5f);
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            Impact?.Invoke(severity, transform.InverseTransformPoint(point));
            if (Hull <= 0f) EngineOn = false;
            StateChanged?.Invoke();
        }
    }
}
