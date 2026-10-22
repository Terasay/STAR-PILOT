using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace StarPilot
{
    public sealed class DemoHud : MonoBehaviour
    {
        public DemoBootstrap Demo;
        public PilotInput Input;
        public ShipFlightController Ship;
        public FlightRoute Route;
        public Camera PilotCamera;
        Canvas canvas;
        public Canvas DesktopCanvas => canvas;
        GameObject menu, help, flightInfo;
        Text status, telemetry, objective, modeLabel, menuTitle, message;
        RectTransform targetMarker;
        Image flash;
        bool started;
        float nextRefresh;
        static readonly Color Ink = new Color(.7f,.81f,.86f);
        static readonly Color Cyan = new Color(.32f,.88f,.94f);

        public void Build()
        {
            canvas = new GameObject("Desktop interface",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform,false);canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            if (EventSystem.current == null)
            {
                var events=new GameObject("Input events",typeof(EventSystem),typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform,false);
            }
            modeLabel=Label(canvas.transform,"Mode", "",13,Ink,new Vector2(695,495),new Vector2(400,24),TextAnchor.MiddleRight);
            flightInfo=new GameObject("Flight information",typeof(RectTransform));flightInfo.transform.SetParent(canvas.transform,false);
            Label(flightInfo.transform,"Flight ID","SP–01    /    KEPLER APPROACH",14,Ink,new Vector2(-660,495),new Vector2(460,25));
            CockpitDisplays.Rect(flightInfo.transform,"ID rule",new Vector2(-754,473),new Vector2(270,1),new Color(.2f,.6f,.66f,.45f));
            status=Label(flightInfo.transform,"Status","",15,Cyan,new Vector2(-655,-492),new Vector2(480,30),TextAnchor.MiddleLeft);
            telemetry=Label(flightInfo.transform,"Interaction hint","",16,Cyan,new Vector2(0,-444),new Vector2(740,40),TextAnchor.MiddleCenter);
            Label(flightInfo.transform,"Hint","L  фары  ·  C  свет  ·  TAB  панель  ·  ALT  обзор  ·  H  помощь",14,Ink,new Vector2(530,-492),new Vector2(720,30),TextAnchor.MiddleRight);
            objective=Label(flightInfo.transform,"Objective","",16,Ink,new Vector2(-675,403),new Vector2(430,65));
            var marker=Panel(flightInfo.transform,"Target",Vector2.zero,new Vector2(34,34),new Color(0,0,0,0));targetMarker=marker.GetComponent<RectTransform>();
            Label(marker.transform,"Diamond","◇",39,Cyan,Vector2.zero,new Vector2(46,46),TextAnchor.MiddleCenter);

            flash=CockpitDisplays.Rect(canvas.transform,"Impact flash",Vector2.zero,new Vector2(4000,2500),new Color(1,.12f,.04f,0));

            menu=Panel(canvas.transform,"Flight menu",new Vector2(500,-15),new Vector2(660,705),new Color(.009f,.021f,.031f,.96f));
            CockpitDisplays.Rect(menu.transform,"Accent",new Vector2(-327,0),new Vector2(3,705),Cyan);
            Label(menu.transform,"Eyebrow","S T A R   P I L O T    /    0 3",15,Cyan,new Vector2(0,300),new Vector2(555,35));
            menuTitle=Label(menu.transform,"Title","Орбита Кеплера",43,Color.white,new Vector2(0,240),new Vector2(555,70));
            Label(menu.transform,"Intro","SP–01 готов к вылету.\nПройди четыре навигационных маяка\nи почувствуй корабль в свободном полёте.",21,Ink,new Vector2(0,140),new Vector2(555,110));
            Label(menu.transform,"Controls summary","W / S   тяга     ·     Мышь   поворот\nA / D   вбок      ·     R / F   вверх / вниз\nПробел   тормоз   ·   Shift   ускорение",19,Ink,new Vector2(0,20),new Vector2(555,100));
            Button(menu.transform,"НАЧАТЬ / ПРОДОЛЖИТЬ",new Vector2(0,-95),()=>SetMenu(false),true);
            Button(menu.transform,"ЗАПУСТИТЬ В VR",new Vector2(0,-167),()=>Demo.RequestVr(),false);
            Button(menu.transform,"ВЕРНУТЬСЯ К СТАРТУ",new Vector2(0,-239),()=>{Demo.ResetFlight();SetMenu(false);},false);
            message=Label(menu.transform,"VR status","VR: нужен PC OpenXR runtime и подключённый шлем.",14,Ink,new Vector2(0,-305),new Vector2(555,38));
            var quit=CockpitDisplays.Rect(menu.transform,"Exit",new Vector2(0,-382),new Vector2(260,32),new Color(.015f,.028f,.04f,.8f));
            quit.raycastTarget=true;var quitButton=quit.gameObject.AddComponent<Button>();quitButton.targetGraphic=quit;
            Label(quit.transform,"Exit label","ВЫЙТИ ИЗ ДЕМО",13,Ink,Vector2.zero,new Vector2(250,30),TextAnchor.MiddleCenter);
            quitButton.onClick.AddListener(()=>
            {
                #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying=false;
                #else
                Application.Quit();
                #endif
            });

            help=Panel(canvas.transform,"Controls",new Vector2(-450,0),new Vector2(760,715),new Color(.009f,.021f,.031f,.96f));
            Label(help.transform,"Title","УПРАВЛЕНИЕ",26,Cyan,new Vector2(0,300),new Vector2(650,50));
            Label(help.transform,"Bindings","W / S                 Тяга вперёд / назад\nA / D                 Боковые двигатели\nR / F                  Вверх / вниз\nМышь / стрелки    Тангаж и рыскание\nQ / E                 Крен\nЛевый Alt + мышь  Осмотреть кабину\nTab + ЛКМ           Кнопки на панели\nПробел               Активное торможение\nЛевый Shift          Ускорение\nT                       Стабилизация вкл. / выкл.\nI                        Двигатель вкл. / выкл.\nL / C                  Фары / яркость кабины\nBackspace            Вернуться к старту\nH / Escape           Помощь / меню",19,Ink,new Vector2(0,35),new Vector2(650,500));
            Label(help.transform,"Physics note","Стабилизация плавно гасит движение.\nОтключи её клавишей T, чтобы лететь по инерции.\nТормоз и двигатели работают при включённом питании.",18,Cyan,new Vector2(0,-265),new Vector2(650,100));
            help.SetActive(false);
            Input.MenuRequested=()=>SetMenu(!Input.InMenu);
            Input.HelpRequested=()=>help.SetActive(!help.activeSelf);
            Input.ResetRequested=Demo.ResetFlight;
            SetMenu(true);
        }

        public void SetMenu(bool visible)
        {
            Input.InMenu=visible;Ship.ControlsEnabled=!visible;
            Time.timeScale=visible?0f:1f;
            Input.CaptureCursor(!visible);
            menu.SetActive(visible);flightInfo.SetActive(!visible);
            help.SetActive(false);
            if(!visible && !started)
            {
                started=true;if(!Ship.EngineOn)Ship.ToggleEngine();
            }
            if(visible)menuTitle.text=started?"Полёт приостановлен":"Орбита Кеплера";
        }

        public void ShowVrMessage(string text) { message.text=text; }
        public void OnVrStarted() { SetMenu(false); canvas.enabled=false; }
        public void OnVrStopped() { canvas.enabled=true;SetMenu(true); }

        void Update()
        {
            var kb=Keyboard.current;
            if(Input.InMenu && kb!=null && kb.enterKey.wasPressedThisFrame)SetMenu(false);
            if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.05f;
            status.text=Ship.Hull<=0?"КОРПУС РАЗРУШЕН  ·  BACKSPACE":Ship.EngineOn?(Ship.Brake?"ТОРМОЖЕНИЕ":"ДВИГАТЕЛЬ РАБОТАЕТ"):"ДВИГАТЕЛЬ ВЫКЛЮЧЕН  ·  I";
            telemetry.text=Demo.Cockpit.Hint;
            objective.text=Route.Completed?"МАРШРУТ ПРОЙДЕН\nСвободный полёт · Backspace — повторить":$"УЧЕБНЫЙ МАРШРУТ\nМаяк {Route.Current+1:00} / 04    ·    {Route.Distance:0} м";
            modeLabel.text=Input.IsVr?"OPENXR  /  SEATED FLIGHT":"DESKTOP  /  "+(Ship.AssistOn?"ASSISTED FLIGHT":"INERTIAL FLIGHT");
            Vector3 viewport=PilotCamera.WorldToViewportPoint(Route.Target);
            targetMarker.gameObject.SetActive(!Route.Completed && viewport.z>0 && viewport.x>.05f && viewport.x<.95f && viewport.y>.12f && viewport.y<.88f);
            var rect=canvas.GetComponent<RectTransform>();
            targetMarker.anchoredPosition=new Vector2((viewport.x-.5f)*rect.rect.width,(viewport.y-.5f)*rect.rect.height);
            flash.color=new Color(1,.12f,.04f,Demo.Audio?Demo.Audio.ImpactFlash*.3f:0);
        }

        static GameObject Panel(Transform parent,string name,Vector2 position,Vector2 size,Color color)=>CockpitDisplays.Rect(parent,name,position,size,color).gameObject;
        static Text Label(Transform p,string n,string t,int s,Color c,Vector2 pos,Vector2 size,TextAnchor a=TextAnchor.UpperLeft)=>CockpitDisplays.Text(p,n,t,s,c,pos,size,a);
        static void Button(Transform parent,string title,Vector2 pos,UnityEngine.Events.UnityAction action,bool primary)
        {
            var image=CockpitDisplays.Rect(parent,title,pos,new Vector2(555,57),primary?new Color(.24f,.75f,.81f):new Color(.055f,.1f,.13f));
            image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var colors=button.colors;colors.highlightedColor=new Color(.8f,.94f,1);colors.pressedColor=new Color(.5f,.7f,.8f);button.colors=colors;
            Label(image.transform,"Label",title,18,primary?new Color(.015f,.05f,.07f):Ink,Vector2.zero,new Vector2(530,50),TextAnchor.MiddleCenter);
            button.onClick.AddListener(action);
        }
    }
}
