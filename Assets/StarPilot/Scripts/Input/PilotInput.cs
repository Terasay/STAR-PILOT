using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace StarPilot
{
    /// <summary>Desktop and OpenXR controllers feed the same flight commands.</summary>
    public sealed class PilotInput : MonoBehaviour
    {
        public ShipFlightController Ship;
        public ShipLighting Lighting;
        public ExplorationController Expedition;
        public Transform View;
        public bool InMenu = true;
        public bool IsVr { get; set; }
        public bool CockpitPointer {get;private set;}
        public System.Action MenuRequested;
        public System.Action HelpRequested;
        public System.Action ResetRequested;
        public float mouseSensitivity = 0.075f;
        float lookYaw, lookPitch;
        bool previousPrimary, previousSecondary, previousMenu;

        public void CaptureCursor(bool capture)
        {
            Cursor.lockState = capture && !IsVr && !CockpitPointer ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture || IsVr || CockpitPointer;
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if(Expedition){Expedition.ScanRequested=!InMenu && !IsVr && kb!=null && kb.gKey.isPressed;Expedition.InteractRequested=!InMenu && !IsVr && kb!=null && kb.xKey.isPressed;}
            if (IsVr)
            {
                InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).TryGetFeatureValue(CommonUsages.menuButton, out bool menu);
                if (menu && !previousMenu) MenuRequested?.Invoke();
                previousMenu = menu;
            }
            if (kb != null && kb.escapeKey.wasPressedThisFrame) MenuRequested?.Invoke();
            if (kb != null && kb.hKey.wasPressedThisFrame) HelpRequested?.Invoke();
            if (InMenu)
            {
                Ship.Translation = Ship.Rotation = Vector3.zero;
                Ship.Brake = Ship.Boost = false;
                return;
            }
            if (kb != null)
            {
                if (kb.iKey.wasPressedThisFrame) Ship.ToggleEngine();
                if (kb.tKey.wasPressedThisFrame) Ship.ToggleAssist();
                if (kb.lKey.wasPressedThisFrame) Lighting.ToggleHeadlights();
                if (kb.cKey.wasPressedThisFrame) Lighting.ToggleCabin();
                if (kb.backspaceKey.wasPressedThisFrame) ResetRequested?.Invoke();
                if (kb.vKey.wasPressedThisFrame && Expedition) PressNavigation(CockpitAction.NextSignal);
                if (kb.bKey.wasPressedThisFrame && Expedition) PressNavigation(CockpitAction.Home);
                if (kb.jKey.wasPressedThisFrame && Expedition) PressNavigation(CockpitAction.Mission);
                if (kb.zKey.wasPressedThisFrame && Expedition) PressNavigation(CockpitAction.Approach);
                if (kb.nKey.wasPressedThisFrame && Expedition) PressNavigation(CockpitAction.RadarRange);
            }
            if (IsVr) ReadVr();
            else ReadDesktop(kb);
        }

        void PressNavigation(CockpitAction action)
        {
            foreach(var control in Expedition.Cockpit.Controls)if(control.Action==action){control.Press();return;}
        }

        void ReadDesktop(Keyboard kb)
        {
            if (kb == null) return;
            bool pointer=kb.tabKey.isPressed;
            if(pointer!=CockpitPointer){CockpitPointer=pointer;CaptureCursor(!pointer);}
            Ship.Translation = new Vector3(Axis(kb.dKey.isPressed, kb.aKey.isPressed),
                Axis(kb.rKey.isPressed, kb.fKey.isPressed), Axis(kb.wKey.isPressed, kb.sKey.isPressed));
            Ship.Brake = kb.spaceKey.isPressed;
            Ship.Boost = kb.leftShiftKey.isPressed;
            Vector2 delta = Mouse.current != null && Cursor.lockState == CursorLockMode.Locked
                ? Mouse.current.delta.ReadValue() : Vector2.zero;
            Vector2 steering = delta * mouseSensitivity / Mathf.Max(Time.unscaledDeltaTime * 100f, 0.1f);
            if (kb.leftAltKey.isPressed || CockpitPointer)
            {
                if(!CockpitPointer){lookYaw = Mathf.Clamp(lookYaw + delta.x * mouseSensitivity, -100f, 100f);
                lookPitch = Mathf.Clamp(lookPitch - delta.y * mouseSensitivity, -65f, 65f);}
                Ship.Rotation = new Vector3(Axis(kb.downArrowKey.isPressed, kb.upArrowKey.isPressed),
                    Axis(kb.rightArrowKey.isPressed, kb.leftArrowKey.isPressed), Axis(kb.qKey.isPressed, kb.eKey.isPressed));
            }
            else
            {
                lookYaw = Mathf.Lerp(lookYaw, 0f, Time.unscaledDeltaTime * 6f);
                lookPitch = Mathf.Lerp(lookPitch, 0f, Time.unscaledDeltaTime * 6f);
                Ship.Rotation = new Vector3(Mathf.Clamp(-steering.y, -1f, 1f) + Axis(kb.downArrowKey.isPressed, kb.upArrowKey.isPressed),
                    Mathf.Clamp(steering.x, -1f, 1f) + Axis(kb.rightArrowKey.isPressed, kb.leftArrowKey.isPressed),
                    Axis(kb.qKey.isPressed, kb.eKey.isPressed));
            }
            View.localRotation = Quaternion.Euler(lookPitch, lookYaw, 0f);
        }

        void ReadVr()
        {
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            left.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 l);
            right.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 r);
            left.TryGetFeatureValue(CommonUsages.trigger, out float lt);
            right.TryGetFeatureValue(CommonUsages.trigger, out float rt);
            left.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool brake);
            right.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool boost);
            right.TryGetFeatureValue(CommonUsages.primaryButton, out bool primary);
            right.TryGetFeatureValue(CommonUsages.secondaryButton, out bool secondary);
            left.TryGetFeatureValue(CommonUsages.primaryButton, out bool rollLeft);
            left.TryGetFeatureValue(CommonUsages.secondaryButton, out bool rollRight);
            if (primary && !previousPrimary) Ship.ToggleEngine();
            if (secondary && !previousSecondary) Ship.ToggleAssist();
            previousPrimary = primary;
            previousSecondary = secondary;
            Ship.Translation = new Vector3(DeadZone(l.x), rt - lt, DeadZone(l.y));
            Ship.Rotation = new Vector3(-DeadZone(r.y), DeadZone(r.x), Axis(rollLeft, rollRight));
            Ship.Brake = brake;
            Ship.Boost = boost;
        }

        static float Axis(bool positive, bool negative) => (positive ? 1f : 0f) - (negative ? 1f : 0f);
        static float DeadZone(float value) => Mathf.Abs(value) < 0.15f ? 0f : Mathf.Sign(value) * (Mathf.Abs(value) - 0.15f) / 0.85f;

        void OnApplicationFocus(bool focused)
        {
            if (!focused && !InMenu) MenuRequested?.Invoke();
        }
        void OnDisable() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
