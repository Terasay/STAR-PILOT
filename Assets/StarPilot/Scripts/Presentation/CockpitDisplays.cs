using UnityEngine;
using UnityEngine.UI;

namespace StarPilot
{
    public sealed class CockpitDisplays : MonoBehaviour
    {
        public static Material ScreenMaterial;
        public ShipFlightController Ship;
        public FlightRoute Route;
        public Text SpeedText, SystemsText, NavigationText, FlightMode, AttitudeText, OverheadText;
        public Text RouteText, GuidanceText, StatusText, InteractionText;
        public CockpitInteraction Interaction;
        public ExplorationController Expedition;
        public GameObject OperationsPanel;
        public Text MissionTitle,MissionInstruction,MissionCargo,MissionAccount;
        public RectTransform MissionProgress;
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
            SystemsText.text=$"КОРПУС  {Ship.Hull:000}%";
            FlightMode.text=!Ship.EngineOn?"ДВИГАТЕЛЬ ВЫКЛ.":Ship.Brake?"ТОРМОЖЕНИЕ":Ship.Boost?"УСКОРЕНИЕ":Ship.AssistOn?"СТАБИЛИЗАЦИЯ ВКЛ.":"СВОБОДНАЯ ИНЕРЦИЯ";
            NavigationText.text=Route.Completed?"СВОБОДНЫЙ ПОЛЁТ":$"{Route.Distance:0} м";
            RouteText.text=Route.Completed?"МАРШРУТ ПРОЙДЕН":$"МАЯК {Route.Current+1} / {Route.Gates.Length}";
            Vector3 target=Ship.transform.InverseTransformPoint(Route.Target);
            float heading=Mathf.Atan2(target.x,target.z)*Mathf.Rad2Deg;
            float elevation=Mathf.Atan2(target.y,new Vector2(target.x,target.z).magnitude)*Mathf.Rad2Deg;
            GuidanceText.text=Route.Completed?"ЧЕТЫРЕ МАЯКА · ГОТОВО":
                $"{(Mathf.Abs(heading)<4?"ПО КУРСУ":heading>0?"ВПРАВО":"ВЛЕВО")}  {Mathf.Abs(heading):0}°\n"+
                $"{(Mathf.Abs(elevation)<3?"ПО ВЫСОТЕ":elevation>0?"ВЫШЕ":"НИЖЕ")}  {Mathf.Abs(elevation):0}°";
            bool exploring=Expedition && Expedition.Active;
            OperationsPanel.SetActive(exploring);SystemsText.gameObject.SetActive(!exploring);
            if(exploring)
            {
                var site=Expedition.Target;
                float distance=site?Vector3.Distance(site.Position,Ship.transform.position):0;
                RouteText.text=site?site.Title:"ПОИСК СИГНАЛА";
                NavigationText.text=!site?"—":distance<1000?$"{distance:0} м":$"{distance/1000:0.0} км";
                Vector3 relative=site?Ship.transform.InverseTransformPoint(site.Position):Vector3.forward;
                heading=Mathf.Atan2(relative.x,relative.z)*Mathf.Rad2Deg;
                elevation=Mathf.Atan2(relative.y,new Vector2(relative.x,relative.z).magnitude)*Mathf.Rad2Deg;
                GuidanceText.text=$"{(Mathf.Abs(heading)<4?"ПО КУРСУ":heading>0?"ВПРАВО":"ВЛЕВО")} {Mathf.Abs(heading):0}°\n"+
                    $"{(Mathf.Abs(elevation)<3?"ПО ВЫСОТЕ":elevation>0?"ВЫШЕ":"НИЖЕ")} {Mathf.Abs(elevation):0}°";
                if(site && site.Kind==SiteKind.Station && distance<26)
                {
                    Vector3 axis=Ship.transform.InverseTransformDirection(site.transform.forward);
                    float yaw=Mathf.Atan2(axis.x,axis.z)*Mathf.Rad2Deg;
                    float pitch=Mathf.Atan2(axis.y,new Vector2(axis.x,axis.z).magnitude)*Mathf.Rad2Deg;
                    GuidanceText.text=$"ОСЬ: {(Mathf.Abs(yaw)<4?"ПРЯМО":yaw>0?"ВПРАВО":"ВЛЕВО")} {Mathf.Abs(yaw):0}°\n"+
                        $"{(Mathf.Abs(pitch)<3?"ПО ВЫСОТЕ":pitch>0?"ВЫШЕ":"НИЖЕ")} {Mathf.Abs(pitch):0}°";
                }
                MissionTitle.text=Expedition.IsScanning?$"СКАНИРОВАНИЕ {Expedition.Progress*100:0}%":Expedition.IsInteracting?$"ОПЕРАЦИЯ {Expedition.Progress*100:0}%":site?site.Kind==SiteKind.Station?"ДОСТАВКА И СЕРВИС":"ИССЛЕДОВАТЬ СИГНАЛ":"ИССЛЕДОВАТЕЛЬСКИЙ ВЫЛЕТ";
                MissionInstruction.text=Expedition.Instruction;
                MissionCargo.text=Expedition.CargoSummary;
                MissionAccount.text=Expedition.AccountSummary+$"   КОРПУС {Ship.Hull:0}%";
                MissionProgress.sizeDelta=new Vector2(480*Expedition.Progress,7);
                MissionProgress.anchoredPosition=new Vector2(-240+240*Expedition.Progress,-23);
            }
            StatusText.text=!Ship.ControlsEnabled?"ПОЛЁТ ПРИОСТАНОВЛЕН":Ship.Hull<=0?"КОРПУС РАЗРУШЕН":Ship.Hull<35?"ПОВРЕЖДЕНИЕ КОРПУСА":!Ship.EngineOn?"ДВИГАТЕЛЬ ВЫКЛ.":Ship.Brake?"ТОРМОЖЕНИЕ":Ship.Boost?"УСКОРЕНИЕ":"СИСТЕМЫ В НОРМЕ";
            StatusText.color=Ship.Hull<35?new Color(1,.42f,.21f):new Color(.62f,.89f,.88f);
            StatusText.fontSize=exploring && !string.IsNullOrEmpty(Expedition.Notice)?22:30;
            if(exploring && Ship.ControlsEnabled && Ship.Hull>=35 && Ship.EngineOn && !string.IsNullOrEmpty(Expedition.Notice))StatusText.text=Expedition.Notice;
            if(exploring && !Expedition.SavingAvailable)StatusText.text="СОХРАНЕНИЕ НЕДОСТУПНО";
            bool desktop=Interaction.Input && !Interaction.Input.IsVr;
            InteractionText.text=!Ship.ControlsEnabled?(desktop?"ESC · ПРОДОЛЖИТЬ":"КНОПКА МЕНЮ · ПРОДОЛЖИТЬ"):
                Ship.Hull<=0?(desktop?"BACKSPACE · НОВЫЙ ВЫЛЕТ":"ТРЕБУЕТСЯ ПЕРЕЗАПУСК"):
                desktop && Interaction.Input.CockpitPointer?Interaction.Hint:
                !Ship.EngineOn?(desktop?"I / ENGINE · ВКЛЮЧИТЬ":"ПЕРЕКЛЮЧАТЕЛЬ ENGINE"):
                desktop?(exploring?"V · СЛЕДУЮЩИЙ    B · БАЗА":"TAB · ПРИБОРЫ    H · СПРАВКА"):"ПРЯМОЕ УПРАВЛЕНИЕ";
            Vector3 angle=Ship.transform.eulerAngles;
            AttitudeText.text=$"P {Mathf.DeltaAngle(0,angle.x):+000;-000;000}   Y {angle.y:000}\nR {Mathf.DeltaAngle(0,angle.z):+000;-000;000}";
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
