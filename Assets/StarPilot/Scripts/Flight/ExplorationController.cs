using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarPilot
{
    /// <summary>Survey, recovery and delivery use actual range, alignment, speed and cargo state.</summary>
    [DefaultExecutionOrder(80)]
    public sealed partial class ExplorationController : MonoBehaviour
    {
        public ShipFlightController Ship;
        public ExplorationWorld World;
        public CockpitInteraction Cockpit;
        public bool Active {get;private set;}=true;
        public bool ScanRequested,InteractRequested;
        public bool IsScanning {get;private set;}
        public bool IsInteracting {get;private set;}
        public ExplorationSite Target {get;private set;}
        public ExpeditionSave Save {get;private set;}
        public string SavePath {get;private set;}
        public bool SavingAvailable {get;private set;}=true;
        public int Grade=>Mathf.Min(3,Save.Credits/600);
        public int Capacity=>4+Grade*2;
        public float ScanRange=>160+Grade*40;
        public float Progress {get;private set;}
        public string Instruction {get;private set;}="ПОИСК СИГНАЛОВ";
        public string Notice {get;private set;}="";
        public int UsedCargo=>Save.Cargo+(Mission!=null && Mission.Carrying?ResearchMission.CargoUnits:0);
        public string CargoSummary=>$"ГРУЗ {UsedCargo}/{Capacity}   ДАННЫЕ {Save.Data}";
        public string AccountSummary=>$"{Save.Credits} CR   СКАНЕР MK {Grade+1}";
        public float RadarRange=>radarRanges[radarScale];
        static readonly float[] radarRanges={500,1500,3000};
        int radarScale=1;
        public void CycleRadarRange(){if(Active)radarScale=(radarScale+1)%radarRanges.Length;}
        public event Action<bool> Feedback;
        readonly HashSet<string> scanned=new HashSet<string>(),collected=new HashSet<string>();
        float work,noticeUntil;

        public void Initialize(ShipFlightController ship,ExplorationWorld world,CockpitInteraction cockpit)
        {
            Ship=ship;World=world;Cockpit=cockpit;SavePath=ExpeditionSave.DefaultPath();
            try {Save=ExpeditionSave.Load(SavePath);}
            catch(Exception error){Save=new ExpeditionSave();SavingAvailable=false;Debug.LogWarning("[STAR PILOT] Save preserved: "+error.Message);}
            foreach(string id in Save.Scanned)scanned.Add(id);foreach(string id in Save.Collected)collected.Add(id);
            World.Seed=Save.Seed;World.Expedition=this;World.SetExploration(true);
            if(Save.Mission==null){Save.Mission=ResearchMission.Create(World.LayoutSource,Save.Seed,new SectorAddress(),1);Commit();}
            foreach(var control in Cockpit.Controls)control.Expedition=this;
        }
        public void SetActiveMode(bool active)
        {
            Ship.Approach?.SetArmed(false);
            Active=active;FollowingMission=active;ScanRequested=InteractRequested=false;Target=null;ResetWork();World.SetExploration(active);
        }
        public void RestoreSite(ExplorationSite site)
        {
            site.Scanned=scanned.Contains(site.Id);site.Collected=collected.Contains(site.Id);site.RefreshVisual();
        }
        void Update()
        {
            if(!Active || !Ship.ControlsEnabled){IsScanning=IsInteracting=false;return;}
            if(Time.time>noticeUntil)Notice="";
            if(World.LoadedSectorCount==0){Instruction="ПОИСК БЛИЖНИХ СИГНАЛОВ";return;}
            if(!Target || !Target.gameObject.activeInHierarchy){if(FollowingMission)SelectMissionTarget();else CycleTarget();}
            if(!Target){Instruction="ПОИСК БЛИЖНИХ СИГНАЛОВ";return;}
            bool scan=ScanRequested,use=InteractRequested;
            foreach(var control in Cockpit.Controls)
            {
                if(control.Action==CockpitAction.Scan)scan|=control.Held;
                if(control.Action==CockpitAction.Recover)use|=control.Held;
            }
            IsScanning=IsInteracting=false;
            if(!Ship.EngineOn || Ship.Hull<=0){Instruction="ВКЛЮЧИТЕ ДВИГАТЕЛЬ\nДЛЯ РАБОТЫ ОБОРУДОВАНИЯ";Decay();return;}
            if(Target.IsRemote){Instruction="СИГНАЛ В ДРУГОМ СЕКТОРЕ\nСЛЕДУЙТЕ КУРСУ НА ПРИБОРЕ";Decay();return;}
            float distance=Vector3.Distance(Ship.transform.position,Target.Position);
            float angle=Vector3.Angle(Ship.transform.forward,Target.Position-Ship.transform.position);
            if(Target.MissionObjective){UpdateMission(scan,use,distance,angle);return;}
            if(Target.Kind==SiteKind.Station)
            {
                bool near=distance<26,slow=Ship.Speed<3,aligned=Vector3.Angle(Ship.transform.forward,Target.transform.forward)<22;
                Instruction=!near?"СБЛИЗИТЬСЯ С ПРИЧАЛОМ\nДО 26 МЕТРОВ":!slow?"СНИЗЬТЕ СКОРОСТЬ\nДО 3 М/С":!aligned?"ВЫРОВНЯЙТЕСЬ ПО ОСИ\nСТЫКОВОЧНОГО ПОРТА":"УДЕРЖИВАЙТЕ X / SERVICE\nСДАТЬ ГРУЗ И ОБСЛУЖИТЬСЯ";
                if(use && near && slow && aligned){IsInteracting=true;work+=Time.deltaTime;Progress=Mathf.Clamp01(work/2);if(work>=2)Deliver();}
                else Decay();return;
            }
            if(Target.Exhausted){Instruction="ОБЪЕКТ ИССЛЕДОВАН\nV / NEXT · СЛЕДУЮЩИЙ";Decay();return;}
            if(!Target.Scanned)
            {
                bool near=distance<=ScanRange,slow=Ship.Speed<=12,aligned=angle<=18;
                bool visible=near && aligned && LineOfSight(Target);
                Instruction=!near?$"ПОДЛЕТИТЕ БЛИЖЕ {ScanRange:0} М\nДЛЯ СКАНИРОВАНИЯ":!slow?"СНИЗЬТЕ СКОРОСТЬ\nДО 12 М/С":!aligned?"НАПРАВЬТЕ НОС КОРАБЛЯ\nНА ВЫБРАННЫЙ СИГНАЛ":!visible?"ОБЪЕКТ ЗАКРЫТ ПРЕГРАДОЙ\nСМЕНИТЕ ПОЗИЦИЮ":"УДЕРЖИВАЙТЕ G / SCAN\nСОХРАНЯЙТЕ НАВЕДЕНИЕ";
                if(scan && near && slow && aligned && visible){IsScanning=true;work+=Time.deltaTime;Progress=Mathf.Clamp01(work/4);if(work>=4)CompleteScan();}
                else Decay();return;
            }
            int units=Target.Kind==SiteKind.Mineral?2:1;
            bool capacity=UsedCargo+units<=Capacity,close=distance<32,stable=Ship.Speed<5,aim=angle<25;
            Instruction=!capacity?"ГРУЗОВОЙ ОТСЕК ЗАПОЛНЕН\nB / BASE · НА СТАНЦИЮ":!close?"ПОДЛЕТИТЕ БЛИЖЕ 32 М\nДЛЯ ПОДБОРА НАХОДКИ":!stable?"СНИЗЬТЕ СКОРОСТЬ\nДО 5 М/С":!aim?"НАПРАВЬТЕ НОС КОРАБЛЯ\nНА НАЙДЕННЫЙ ОБЪЕКТ":Target.Kind==SiteKind.Wreck?"X / SERVICE · УДЕРЖИВАТЬ\nПОДОБРАТЬ ЧЁРНЫЙ ЯЩИК":"X / SERVICE · УДЕРЖИВАТЬ\nИЗВЛЕЧЬ ОБРАЗЦЫ";
            bool clear=close && aim && LineOfSight(Target);
            if(capacity && close && stable && aim && !clear)Instruction="ОБЪЕКТ ЗАКРЫТ ПРЕГРАДОЙ\nСМЕНИТЕ ПОЗИЦИЮ";
            if(use && capacity && close && stable && aim && clear)
            {
                IsInteracting=true;work+=Time.deltaTime;Progress=Mathf.Clamp01(work/2.5f);
                if(work>=2.5f)
                {
                    Target.Collected=true;collected.Add(Target.Id);Target.RefreshVisual();Save.Cargo+=units;
                    Save.CargoValue+=Target.Kind==SiteKind.Wreck?180:260;Commit();Announce("НАХОДКА В ГРУЗОВОМ ОТСЕКЕ");SelectHome();
                }
            }
            else Decay();
        }
        bool LineOfSight(ExplorationSite site)
        {
            Vector3 from=Ship.transform.TransformPoint(new Vector3(0,1.5f,3));Vector3 delta=site.Position-from;
            float nearestBlock=float.PositiveInfinity,nearestSite=delta.magnitude;
            foreach(var hit in Physics.RaycastAll(from,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.transform.IsChildOf(Ship.transform))continue;
                if(hit.transform.IsChildOf(site.transform))nearestSite=Mathf.Min(nearestSite,hit.distance);
                else nearestBlock=Mathf.Min(nearestBlock,hit.distance);
            }
            return nearestBlock>=nearestSite-.1f;
        }
        void CompleteScan()
        {
            Target.Scanned=true;scanned.Add(Target.Id);Target.RefreshVisual();ResetWork();
            if(Target.Kind==SiteKind.Anomaly){Save.Data++;Commit();Announce("АНОМАЛИЯ ИССЛЕДОВАНА · ДАННЫЕ +1");SelectHome();}
            else {Commit();Announce(Target.Kind==SiteKind.Wreck?"ОБНАРУЖЕН ЧЁРНЫЙ ЯЩИК":"ОБНАРУЖЕНЫ РЕДКИЕ МИНЕРАЛЫ");}
        }
        void Deliver()
        {
            bool missionDelivery=Mission!=null && Mission.Carrying;
            int reward=Save.CargoValue+Save.Data*120+(missionDelivery?ResearchMission.Reward:0),previousGrade=Grade;
            if(missionDelivery){Mission.Stage=ResearchStage.Complete;Mission.Log("Архив принят. Маршрут зонда восстановлен. Награда: 720 CR.");}
            if(reward>0){Save.Credits+=reward;Save.Delivered++;}
            Save.Cargo=Save.CargoValue=Save.Data=0;Ship.Repair();Commit();ResetWork();
            Announce(missionDelivery?$"ЭКСПЕДИЦИЯ ЗАВЕРШЕНА · +{reward} CR":reward>0?$"ДОСТАВКА +{reward} CR"+(Grade>previousGrade?" · СКАНЕР УЛУЧШЕН":"") :"КОРАБЛЬ ОБСЛУЖЕН · КОРПУС 100%");
            CycleTarget();
        }
        void Announce(string message){Notice=message;noticeUntil=Time.time+7;Feedback?.Invoke(true);}
        void Decay(){work=Mathf.Max(0,work-Time.deltaTime*2);Progress=Mathf.Clamp01(work/(Target && Target.Kind==SiteKind.Station?2:Target && Target.Scanned?2.5f:4));}
        void ResetWork(){work=Progress=0;IsScanning=IsInteracting=false;}
        public void CycleTarget()
        {
            if(!Active)return;
            FollowingMission=false;
            var candidates=new List<ExplorationSite>();
            foreach(var site in World.Sites)if(site && site.gameObject.activeInHierarchy && site.Kind!=SiteKind.Station && !site.Exhausted)candidates.Add(site);
            candidates.Sort((a,b)=>(a.Position-Ship.transform.position).sqrMagnitude.CompareTo((b.Position-Ship.transform.position).sqrMagnitude));
            if(candidates.Count==0){Target=NearestStation();ResetWork();return;}
            int current=candidates.IndexOf(Target);Target=candidates[(current+1)%candidates.Count];ResetWork();
        }
        ExplorationSite NearestStation()
        {
            ExplorationSite best=null;float distance=float.PositiveInfinity;
            foreach(var site in World.Sites)
            {
                if(!site || !site.gameObject.activeInHierarchy || site.Kind!=SiteKind.Station)continue;
                float d=(site.Position-Ship.transform.position).sqrMagnitude;if(d<distance){distance=d;best=site;}
            }
            return best;
        }
        public void SelectHome(){if(!Active)return;FollowingMission=false;Target=NearestStation();ResetWork();}
        public void SelectTarget(ExplorationSite site){if(!Active || !site)return;FollowingMission=site.MissionObjective;Target=site;ResetWork();}
        public void AbortSortie()
        {
            if(Mission!=null && Mission.Carrying){Mission.Stage=ResearchStage.Archive;Mission.Log("Вылет отменён. Повторите извлечение архива у зонда.");}
            Save.Cargo=Save.Data=Save.CargoValue=0;FollowingMission=true;Target=null;Notice="";ResetWork();Commit();
        }
        void Commit()
        {
            if(!SavingAvailable)return;
            Save.Scanned=new List<string>(scanned);Save.Collected=new List<string>(collected);
            try {Save.Write(SavePath);}
            catch(Exception error){SavingAvailable=false;Debug.LogWarning("[STAR PILOT] Expedition saving unavailable: "+error.Message);}
        }
    }
}
