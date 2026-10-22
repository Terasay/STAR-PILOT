using UnityEngine;
using UnityEngine.UI;

namespace StarPilot
{
    public sealed class CockpitDisplays : MonoBehaviour
    {
        public static Material ScreenMaterial;
        public ShipFlightController Ship;
        public FlightRoute Route;
        public Text SpeedText, SystemsText, NavigationText, FlightMode, AttitudeText, CombinerText, OverheadText;
        public ShipLighting Lighting;
        public RectTransform[] AxisIndicators=new RectTransform[3];
        public FlightInstrument[] Instruments;
        public Transform Stick, Throttle;
        float nextRefresh;

        void Update()
        {
            if (Stick) Stick.localRotation = Quaternion.Slerp(Stick.localRotation,Quaternion.Euler(Ship.Rotation.x * 18f, 0f, -Ship.Rotation.y * 18f),Time.unscaledDeltaTime*12);
            if (Throttle) Throttle.localRotation = Quaternion.Slerp(Throttle.localRotation,Quaternion.Euler(Ship.Translation.z * 28f, 0f, 0f),Time.unscaledDeltaTime*10);
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f;
            OverheadText.text=$"PROPULSION    {(Ship.EngineOn?"ONLINE":"STANDBY")}\nFLIGHT ASSIST   {(Ship.AssistOn?"ENGAGED":"FREE DRIFT")}\nFLOOD {(Lighting.HeadlightsOn?"ON":"OFF")}     CABIN {(Lighting.CabinBright?"BRIGHT":"DIM")}";
            SpeedText.text=$"{Ship.Speed:000.0}";
            SystemsText.text=$"HULL / {Ship.Hull:000}%";
            FlightMode.text=!Ship.EngineOn?"PROPULSION / STANDBY":Ship.Brake?"DECELERATION / ACTIVE":Ship.Boost?"OVERDRIVE / ACTIVE":Ship.AssistOn?"FLIGHT ASSIST / ON":"FLIGHT ASSIST / OFF";
            NavigationText.text=!Ship.ControlsEnabled?"FLIGHT PAUSED / ESC":Route.Completed?"ROUTE COMPLETE":$"GATE {Route.Current+1:00}  ·  {Route.Distance:0000} M";
            Vector3 angle=Ship.transform.eulerAngles;
            AttitudeText.text=$"P {Mathf.DeltaAngle(0,angle.x):+000;-000;000}   Y {angle.y:000}\nR {Mathf.DeltaAngle(0,angle.z):+000;-000;000}";
            CombinerText.text=$"VEL {Ship.Speed:000.0}      {(Ship.EngineOn?"ENG ON":"ENG OFF")}      {(Ship.AssistOn?"SAS":"DRIFT")}\n"+(Route.Completed?"KEPLER / FREE FLIGHT":$"NAV {Route.Current+1:00}     {Route.Distance:0000} M");
            float[] axes={Ship.Translation.x,Ship.Translation.y,Ship.Rotation.z};
            for(int i=0;i<3;i++)AxisIndicators[i].anchoredPosition=new Vector2(axes[i]*107,90-i*78);
        }

        public static Canvas Screen(Transform parent, string name, Vector3 position, Vector2 size, Vector3 euler)
        {
            var canvas = new GameObject(name, typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.transform.SetParent(parent, false);
            canvas.transform.localPosition = position;
            canvas.transform.localRotation = Quaternion.Euler(euler);
            canvas.transform.localScale = Vector3.one * 0.001f;
            canvas.GetComponent<RectTransform>().sizeDelta = size;
            return canvas;
        }

        public static Text Text(Transform parent, string name, string text, int fontSize, Color color,
            Vector2 position, Vector2 size, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(parent, false);
            label.rectTransform.anchoredPosition = position;
            label.rectTransform.sizeDelta = size;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text; label.fontSize = fontSize; label.color = color;
            label.alignment = alignment; label.supportRichText = true;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            var canvas=parent.GetComponentInParent<Canvas>();
            if(ScreenMaterial && canvas && canvas.renderMode==RenderMode.WorldSpace)label.material=ScreenMaterial;
            return label;
        }

        public static Image Rect(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.rectTransform.anchoredPosition = position; image.rectTransform.sizeDelta = size;
            image.color = color; image.raycastTarget = false;
            var canvas=parent.GetComponentInParent<Canvas>();
            if(ScreenMaterial && canvas && canvas.renderMode==RenderMode.WorldSpace)image.material=ScreenMaterial;
            return image;
        }
    }
}
