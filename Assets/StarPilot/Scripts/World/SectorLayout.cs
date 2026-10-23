using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarPilot
{
    public enum SiteKind { Wreck, Anomaly, Mineral, Station, Relay, Archive }

    [Serializable]
    public struct SectorAddress : IEquatable<SectorAddress>
    {
        public long X,Y,Z;
        public SectorAddress(long x,long y,long z){X=x;Y=y;Z=z;}
        public bool Equals(SectorAddress other)=>X==other.X && Y==other.Y && Z==other.Z;
        public override bool Equals(object other)=>other is SectorAddress address && Equals(address);
        public override int GetHashCode(){unchecked{return ((X.GetHashCode()*397)^Y.GetHashCode())*397^Z.GetHashCode();}}
        public override string ToString()=>$"{X}:{Y}:{Z}";
    }
    [Serializable]
    public sealed class SiteLayout
    {
        public SiteKind Kind;
        public Vector3 Position;
        public float Yaw;
    }
    [Serializable]
    public sealed class SectorLayout
    {
        public const int Version=1;
        public SectorAddress Address;
        public SiteLayout[] Sites;
        public Vector3[] Rocks;
        public float[] RockSizes;
        public int Theme;
    }
    // A learned decoder can supply the same small, validated layout. The current source is procedural.
    public interface ISectorLayoutSource { SectorLayout Generate(int seed,SectorAddress address); }

    public sealed class SeededSectorLayoutSource : ISectorLayoutSource
    {
        struct Rng
        {
            ulong state;
            public Rng(int seed,SectorAddress address)
            {
                unchecked
                {
                    state=Mix((ulong)address.X)^Mix((ulong)address.Y+0x9E3779B97F4A7C15UL)^Mix((ulong)address.Z+0xD1B54A32D192ED03UL)^Mix((uint)seed);
                    if(state==0)state=1;
                }
            }
            static ulong Mix(ulong x){unchecked{x=(x^(x>>30))*0xBF58476D1CE4E5B9UL;x=(x^(x>>27))*0x94D049BB133111EBUL;return x^(x>>31);}}
            public float Next(){unchecked{state^=state>>12;state^=state<<25;state^=state>>27;return ((state*2685821657736338717UL)>>40)/16777216f;}}
            public Vector3 Position()=>new Vector3(140+Next()*720,140+Next()*720,140+Next()*720);
        }
        public SectorLayout Generate(int seed,SectorAddress address)
        {
            var random=new Rng(seed,address);
            var layout=new SectorLayout {Address=address,Theme=(int)(random.Next()*3)};
            if(address.Equals(new SectorAddress(0,0,0)))
            {
                layout.Sites=new[]{
                    new SiteLayout {Kind=SiteKind.Wreck,Position=new Vector3(30,12,270)},
                    new SiteLayout {Kind=SiteKind.Anomaly,Position=new Vector3(-34,0,460)},
                    new SiteLayout {Kind=SiteKind.Mineral,Position=new Vector3(28,-10,650)}};
                layout.Rocks=new[]{new Vector3(71,32,205),new Vector3(-83,62,310),new Vector3(135,-72,410),new Vector3(-170,-85,640),new Vector3(220,70,850)};
                layout.RockSizes=new[]{37f,44,52,65,49};return layout;
            }
            var sites=new List<SiteLayout>();
            // Each neighbourhood has service stations; most sectors contain two survey destinations.
            bool station=FloorMod(address.X,3)==1 && FloorMod(address.Y,3)==0 && FloorMod(address.Z,3)==1;
            sites.Add(new SiteLayout {Kind=station?SiteKind.Station:(SiteKind)(int)(random.Next()*3),Position=random.Position(),Yaw=random.Next()*360});
            Vector3 second=random.Position();
            if(Vector3.Distance(second,sites[0].Position)>240)
                sites.Add(new SiteLayout {Kind=(SiteKind)(int)(random.Next()*3),Position=second,Yaw=random.Next()*360});
            layout.Sites=sites.ToArray();
            var rocks=new List<Vector3>();var sizes=new List<float>();
            for(int attempt=0;attempt<40 && rocks.Count<5;attempt++)
            {
                Vector3 p=random.Position();float size=14+random.Next()*48;bool clear=true;
                foreach(var site in sites)if(Vector3.Distance(p,site.Position)<size+100)clear=false;
                if(clear){rocks.Add(p);sizes.Add(size);}
            }
            layout.Rocks=rocks.ToArray();layout.RockSizes=sizes.ToArray();return layout;
        }
        static long FloorMod(long value,long divisor)=>(value%divisor+divisor)%divisor;
    }
}
