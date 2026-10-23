using UnityEngine;

namespace StarPilot
{
    public sealed partial class ExplorationController
    {
        public ResearchMission Mission=>Save?.Mission;
        public bool FollowingMission {get;private set;}=true;
        public float ParallaxAngle=>Mission!=null && Mission.EchoSamples==1 && World.MissionSite?
            Vector3.Angle(Mission.FirstBearing,Ship.transform.position-World.MissionSite.Position):0;

        public void FocusMission()
        {
            if(!Active || !Ship.ControlsEnabled)return;
            if(Mission.Stage==ResearchStage.Complete)
            {
                var station=NearestStation();
                if(!station || Vector3.Distance(Ship.transform.position,station.Position)>=26 || Ship.Speed>=3)
                {Announce("НОВАЯ ЭКСПЕДИЦИЯ · У ПРИЧАЛА");return;}
                Save.Mission=ResearchMission.Create(World.LayoutSource,Save.Seed,World.CurrentSector,Mission.Number+1);
                Commit();Announce("ПРИНЯТ НОВЫЙ АВАРИЙНЫЙ СИГНАЛ");
            }
            FollowingMission=true;SelectMissionTarget();
        }
        void SelectMissionTarget()
        {
            World.RefreshMission();
            Target=Mission.Stage>=ResearchStage.Return?NearestStation():World.MissionSite;
            ResetWork();
        }
        void AdvanceMission(ResearchStage stage,string entry,string notification)
        {
            Mission.Stage=stage;Mission.Log(entry);Commit();FollowingMission=true;
            SelectMissionTarget();Announce(notification);
        }
        void UpdateMission(bool scan,bool use,float distance,float angle)
        {
            if(Mission.Stage==ResearchStage.Archive && Mission.ArchiveScanned)
            {
                bool capacity=UsedCargo+ResearchMission.CargoUnits<=Capacity,near=distance<32,slow=Ship.Speed<5,aim=angle<25;
                bool clear=near && aim && LineOfSight(Target);
                Instruction=!capacity?"АРХИВУ НУЖНЫ 2 МЕСТА\nСДАЙТЕ ГРУЗ НА СТАНЦИИ":!near?"ПОДОЙДИТЕ К ЗОНДУ\nБЛИЖЕ 32 МЕТРОВ":!slow?"СНИЗЬТЕ СКОРОСТЬ\nДО 5 М/С":!aim?"НАВЕДИТЕ НОС КОРАБЛЯ\nНА КОНТЕЙНЕР АРХИВА":!clear?"ПОДХОД ПЕРЕКРЫТ\nСМЕНИТЕ ПОЗИЦИЮ":"X / SERVICE · УДЕРЖИВАТЬ\nИЗВЛЕЧЬ АРХИВ ЗОНДА";
                if(use && capacity && near && slow && aim && clear)
                {
                    IsInteracting=true;work+=Time.deltaTime;Progress=Mathf.Clamp01(work/2.5f);
                    if(work>=2.5f)AdvanceMission(ResearchStage.Return,"Архив на борту. Доставьте контейнер на любую станцию.","АРХИВ НА БОРТУ · КУРС НА СТАНЦИЮ");
                }
                else Decay();return;
            }
            bool inRange=distance<=ScanRange,stable=Ship.Speed<=12,aligned=angle<=18;
            bool visible=inRange && aligned && LineOfSight(Target);
            bool echo=Mission.Stage==ResearchStage.Echo;
            bool safeRadius=!echo || distance>=55;
            bool second=echo && Mission.EchoSamples==1,parallax=!second || ParallaxAngle>=50;
            Instruction=!inRange?$"ПОДЛЕТИТЕ БЛИЖЕ {ScanRange:0} М\nДЛЯ СКАНИРОВАНИЯ":!safeRadius?"ОТОЙДИТЕ ОТ ЯДРА\nНЕ МЕНЕЕ 55 МЕТРОВ":!stable?"СНИЗЬТЕ СКОРОСТЬ\nДО 12 М/С":!parallax?$"ОБЛЕТИТЕ АНОМАЛИЮ СБОКУ\nНОВЫЙ РАКУРС {Mathf.Min(100,ParallaxAngle*2):0}%":!aligned?"НАПРАВЬТЕ НОС КОРАБЛЯ\nНА ИСТОЧНИК СИГНАЛА":!visible?"СИГНАЛ ЗАКРЫТ ПРЕГРАДОЙ\nСМЕНИТЕ ПОЗИЦИЮ":echo?$"G / SCAN · ЗАМЕР {Mission.EchoSamples+1} ИЗ 2\nУДЕРЖИВАЙТЕ НАВЕДЕНИЕ":"G / SCAN · УДЕРЖИВАТЬ\nПРОЧИТАТЬ ПАМЯТЬ УСТРОЙСТВА";
            if(scan && inRange && safeRadius && stable && parallax && aligned && visible)
            {
                IsScanning=true;work+=Time.deltaTime;Progress=Mathf.Clamp01(work/4);
                if(work>=4)CompleteMissionScan();
            }
            else Decay();
        }
        void CompleteMissionScan()
        {
            ResetWork();
            if(Mission.Stage==ResearchStage.Relay)
                AdvanceMission(ResearchStage.Echo,"Маяк поймал эхо зонда в аномалии. Нужны два замера с разных сторон.","ПОЛУЧЕНА ЗАЦЕПКА · ЭХО ЗОНДА");
            else if(Mission.Stage==ResearchStage.Echo && Mission.EchoSamples==0)
            {
                Mission.EchoSamples=1;Mission.FirstBearing=(Ship.transform.position-Target.Position).normalized;
                Mission.Log("Первый замер записан. Облетите аномалию сбоку и повторите сканирование.");
                Commit();Announce("ЗАМЕР 1 / 2 · СМЕНИТЕ РАКУРС");
            }
            else if(Mission.Stage==ResearchStage.Echo)
                AdvanceMission(ResearchStage.Archive,"Два замера указали на дрейфующий зонд. Его архив ещё цел.","КООРДИНАТЫ ЗОНДА ВОССТАНОВЛЕНЫ");
            else if(Mission.Stage==ResearchStage.Archive)
            {
                Mission.ArchiveScanned=true;Target.Scanned=true;Target.RefreshVisual();
                Mission.Log("Контейнер разблокирован. Подойдите к зонду и извлеките архив: 2 места.");
                Commit();Announce("АРХИВ НАЙДЕН · ПОДЛЕТИТЕ БЛИЖЕ");
            }
        }
    }
}
