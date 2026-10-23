using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarPilot
{
    public enum ResearchStage { Relay, Echo, Archive, Return, Complete }

    [Serializable]
    public sealed class MissionLocation
    {
        public SectorAddress Sector;
        public Vector3 Position;
    }

    /// <summary>Persistent mission state. Locations are saved, never rerolled on reload or origin shifts.</summary>
    [Serializable]
    public sealed class ResearchMission
    {
        public const int Reward=720, CargoUnits=2;
        public int Version=1,Number=1;
        public ResearchStage Stage;
        public MissionLocation Relay,Echo,Archive;
        public int EchoSamples;
        public Vector3 FirstBearing;
        public bool ArchiveScanned;
        public List<string> Journal=new List<string>();
        public bool Carrying=>Stage==ResearchStage.Return;
        public MissionLocation CurrentLocation=>Stage==ResearchStage.Relay?Relay:Stage==ResearchStage.Echo?Echo:Archive;
        public SiteKind CurrentKind=>Stage==ResearchStage.Relay?SiteKind.Relay:Stage==ResearchStage.Echo?SiteKind.Anomaly:SiteKind.Archive;
        public string Name=>$"ЭХО КЕПЛЕРА · {Number:00}";
        public string Step=>Stage switch {
            ResearchStage.Relay=>"1 / 4 · АВАРИЙНЫЙ МАЯК",
            ResearchStage.Echo=>$"2 / 4 · ЗАМЕР {EchoSamples+1} ИЗ 2",
            ResearchStage.Archive=>"3 / 4 · АРХИВ ЗОНДА",
            ResearchStage.Return=>"4 / 4 · ДОСТАВКА АРХИВА",
            _=>"ЭКСПЕДИЦИЯ ЗАВЕРШЕНА" };
        public string LatestEntry=>Journal.Count==0?"":Journal[Journal.Count-1];
        public void Log(string text){Journal.Add(text);while(Journal.Count>6)Journal.RemoveAt(0);}

        public static ResearchMission Create(ISectorLayoutSource source,int seed,SectorAddress anchor,int number)
        {
            int side=number%2==1?1:-1;
            var mission=new ResearchMission { Number=number,
                Relay=Place(source,seed,anchor,new Vector3(250,160,340)),
                Echo=Place(source,seed,new SectorAddress(anchor.X,anchor.Y,anchor.Z+side),new Vector3(350,350,360)),
                Archive=Place(source,seed,new SectorAddress(anchor.X+side,anchor.Y,anchor.Z+side),new Vector3(380,240,650)) };
            mission.Log("Маяк повторяет код пропавшего зонда. Проверьте сигнал.");return mission;
        }
        static MissionLocation Place(ISectorLayoutSource source,int seed,SectorAddress sector,Vector3 preferred)
        {
            var layout=source.Generate(seed,sector);float best=float.PositiveInfinity;Vector3 chosen=Vector3.zero;
            for(int x=0;x<5;x++)for(int y=0;y<5;y++)for(int z=0;z<5;z++)
            {
                Vector3 p=new Vector3(120+x*190,120+y*190,120+z*190);bool clear=true;
                foreach(var site in layout.Sites)if(Vector3.Distance(p,site.Position)<170)clear=false;
                for(int i=0;i<layout.Rocks.Length;i++)if(Vector3.Distance(p,layout.Rocks[i])<145+layout.RockSizes[i]*.6f)clear=false;
                float cost=(p-preferred).sqrMagnitude;if(clear && cost<best){best=cost;chosen=p;}
            }
            if(float.IsPositiveInfinity(best))throw new InvalidOperationException("No safe research mission placement in sector "+sector);
            return new MissionLocation {Sector=sector,Position=chosen};
        }
        public bool IsValid()
        {
            if(Version!=1 || Number<1 || Stage<ResearchStage.Relay || Stage>ResearchStage.Complete || EchoSamples<0 || EchoSamples>1 ||
                Journal==null || Journal.Count>6 || !Valid(Relay) || !Valid(Echo) || !Valid(Archive))return false;
            if(!Finite(FirstBearing.x) || !Finite(FirstBearing.y) || !Finite(FirstBearing.z))return false;
            if(EchoSamples==1 && Mathf.Abs(FirstBearing.magnitude-1)>.02f)return false;
            if(Stage>=ResearchStage.Return && !ArchiveScanned)return false;
            foreach(string entry in Journal)if(entry==null || entry.Length>200)return false;
            return true;
        }
        static bool Valid(MissionLocation place)=>place!=null && Finite(place.Position.x) && Finite(place.Position.y) && Finite(place.Position.z) &&
            place.Position.x>=0 && place.Position.x<=1000 && place.Position.y>=0 && place.Position.y<=1000 && place.Position.z>=0 && place.Position.z<=1000;
        static bool Finite(float n)=>!float.IsNaN(n) && !float.IsInfinity(n);
    }
}
