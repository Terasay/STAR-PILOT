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
        GameObject menu, help;
        Text menuTitle, message;
        bool started;
        public bool FlightOverlayHidden => !menu.activeSelf && !help.activeSelf;
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
            menu=Panel(canvas.transform,"Flight menu",new Vector2(500,-15),new Vector2(660,815),new Color(.009f,.021f,.031f,.96f));
            CockpitDisplays.Rect(menu.transform,"Accent",new Vector2(-327,0),new Vector2(3,815),Cyan);
            Label(menu.transform,"Eyebrow","S T A R   P I L O T    /    0 5",15,Cyan,new Vector2(0,355),new Vector2(555,35));
            menuTitle=Label(menu.transform,"Title","Неизвестные сигналы",38,Color.white,new Vector2(0,295),new Vector2(555,70));
            Label(menu.transform,"Intro","Исследуйте обломки, аномалии и минералы.\nСканируйте сигналы, забирайте находки\nи доставляйте их на станцию за улучшения.",21,Ink,new Vector2(0,200),new Vector2(555,110));
            Label(menu.transform,"Controls summary","W / S  тяга   ·   Мышь  поворот   ·   Пробел  тормоз\nG  сканировать   ·   X  подобрать / сдать груз\nV  следующий сигнал   ·   B  станция\nAlt + мышь  обзор   ·   H  справка",19,Ink,new Vector2(0,80),new Vector2(555,110));
            Button(menu.transform,"ИССЛЕДОВАТЬ / ПРОДОЛЖИТЬ",new Vector2(0,-25),()=>StartMode(false),true);
            Button(menu.transform,"ЗАПУСТИТЬ В VR",new Vector2(0,-97),()=>Demo.RequestVr(),false);
            Button(menu.transform,"ТРЕНИРОВКА ПО МАЯКАМ",new Vector2(0,-169),()=>StartMode(true),false);
            Button(menu.transform,"СБРОС ВЫЛЕТА · ПОТЕРЯ ГРУЗА",new Vector2(0,-241),()=>{Demo.ResetFlight();SetMenu(false);},false);
            message=Label(menu.transform,"VR status","VR: нужен PC OpenXR runtime и подключённый шлем.\nСмена режима сбрасывает вылет и несданный груз.",14,Ink,new Vector2(0,-313),new Vector2(555,50));
            var quit=CockpitDisplays.Rect(menu.transform,"Exit",new Vector2(0,-375),new Vector2(260,32),new Color(.015f,.028f,.04f,.8f));
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

            help=Panel(canvas.transform,"Controls",new Vector2(-450,0),new Vector2(760,865),new Color(.009f,.021f,.031f,.96f));
            Label(help.transform,"Title","УПРАВЛЕНИЕ",26,Cyan,new Vector2(0,375),new Vector2(650,50));
            Label(help.transform,"Bindings","W / S                 Тяга вперёд / назад\nA / D                 Боковые двигатели\nR / F                  Вверх / вниз\nМышь / стрелки    Тангаж и рыскание\nQ / E                 Крен\nЛевый Alt + мышь  Осмотреть кабину\nTab + ЛКМ           Кнопки на панели\nПробел               Активное торможение\nЛевый Shift          Ускорение\nT                       Стабилизация вкл. / выкл.\nI                        Двигатель вкл. / выкл.\nL / C                  Фары / яркость кабины\nBackspace            Вернуться к старту\nH / Escape           Помощь / меню",19,Ink,new Vector2(0,35),new Vector2(650,500));
            Label(help.transform,"Survey bindings","G / SCAN           Удерживать: сканировать сигнал\nX / SERVICE        Удерживать: подбор / сдача груза\nV / NEXT             Следующий сигнал\nB / BASE             Курс на ближайшую станцию",19,Cyan,new Vector2(0,-270),new Vector2(650,120));
            Label(help.transform,"Physics note","Сканирование: до 160 м, скорость до 12 м/с.\nПодбор: до 32 м, скорость до 5 м/с.\nСброс / смена режима: несданный груз теряется.",17,Ink,new Vector2(0,-375),new Vector2(650,85));
            help.SetActive(false);
            Input.MenuRequested=()=>SetMenu(!Input.InMenu);
            Input.HelpRequested=()=> { if(!Input.IsVr)help.SetActive(!help.activeSelf); };
            Input.ResetRequested=Demo.ResetFlight;
            SetMenu(true);
        }

        public void SetMenu(bool visible)
        {
            Input.InMenu=visible;Ship.ControlsEnabled=!visible;
            Time.timeScale=visible?0f:1f;
            Input.CaptureCursor(!visible);
            menu.SetActive(visible);
            help.SetActive(false);
            if(!visible && !started)
            {
                started=true;if(!Ship.EngineOn)Ship.ToggleEngine();
            }
            if(visible)menuTitle.text=started?"Полёт приостановлен":"Неизвестные сигналы";
        }

        void StartMode(bool training)
        {
            bool changed=Demo.Training!=training;Demo.SetTraining(training);SetMenu(false);
            if(changed && !Ship.EngineOn)Ship.ToggleEngine();
        }

        public void ShowVrMessage(string text) { message.text=text; }
        public void OnVrStarted() { SetMenu(false); canvas.enabled=false; }
        public void OnVrStopped() { canvas.enabled=true;SetMenu(true); }

        void Update()
        {
            var kb=Keyboard.current;
            if(Input.InMenu && kb!=null && kb.enterKey.wasPressedThisFrame)SetMenu(false);
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
