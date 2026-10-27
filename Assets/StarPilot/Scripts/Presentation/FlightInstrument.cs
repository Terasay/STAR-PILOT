using UnityEngine;
using UnityEngine.UI;

namespace StarPilot
{
    /// <summary>Vector instruments in physical cockpit screens. Drawn in one UI mesh per display.</summary>
    public sealed class FlightInstrument : MaskableGraphic
    {
        public enum Instrument { Navigation, Velocity, Systems, Attitude }
        public Instrument Kind;
        public ShipFlightController Ship;
        public FlightRoute Route;
        public ExplorationController Expedition;
        static readonly Color Cyan=new Color(.23f,.83f,.86f), Dim=new Color(.06f,.22f,.26f),
            Pale=new Color(.68f,.91f,.9f), Amber=new Color(1f,.53f,.22f);
        float refresh;
        void Update(){if(Time.unscaledTime>refresh){refresh=Time.unscaledTime+.08f;SetVerticesDirty();}}
        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();if(!Ship || !Route)return;
            if(Kind==Instrument.Navigation)Navigation(v);
            else if(Kind==Instrument.Velocity)Velocity(v);
            else if(Kind==Instrument.Systems)Systems(v);
            else Attitude(v);
        }
        void Navigation(VertexHelper v)
        {
            for(int i=0;i<96;i++)
            {
                int n=v.currentVertCount;var background=new Color(.003f,.013f,.019f);
                v.AddVert(Vector2.zero,background,Vector2.zero);v.AddVert(Polar(216,i*3.75f),background,Vector2.zero);
                v.AddVert(Polar(216,(i+1)*3.75f),background,Vector2.zero);v.AddTriangle(n,n+1,n+2);
            }
            Vector2 center=new Vector2(0,52);
            for(int r=28;r<=112;r+=28)Arc(v,center,r,0,360,2,Dim);
            Line(v,center+new Vector2(-113,0),center+new Vector2(113,0),1.8f,Dim);Line(v,center+new Vector2(0,-113),center+new Vector2(0,113),1.8f,Dim);
            Arc(v,center,114,0,360,2,Cyan);
            for(int i=0;i<48;i++){float a=i*7.5f;Line(v,center+Polar(117,a),center+Polar(i%4==0?124:120,a),i%4==0?2:1,i%4==0?Pale:Dim);}
            if(Expedition && Expedition.Active)
            {
                foreach(var site in Expedition.World.Sites)
                {
                    if(!site || !site.gameObject.activeInHierarchy || site.Exhausted)continue;
                    Vector3 p=Ship.transform.InverseTransformPoint(site.Position);
                    bool selected=site==Expedition.Target;
                    bool outside=p.magnitude>Expedition.RadarRange || site.IsRemote;
                    if(outside && !selected)continue;
                    Vector2 planar=new Vector2(p.x,p.z);
                    Vector2 offset=outside?(planar.sqrMagnitude>.01f?planar.normalized:Vector2.up)*102:planar*(100/Expedition.RadarRange);
                    Vector2 mark=center+offset;
                    Color ink=selected?Amber:site.Kind==SiteKind.Station?Cyan:site.Kind==SiteKind.Anomaly?new Color(.68f,.48f,.95f):Pale;
                    if(selected)
                    {
                        Line(v,center,mark,1.5f,new Color(.4f,.25f,.1f));
                        Arc(v,mark,10,0,360,2,Amber);
                        if(outside)
                        {
                            Vector2 outward=offset.normalized,normal=new Vector2(-outward.y,outward.x);
                            Line(v,mark-outward*5-normal*5,mark+outward*5,2,Amber);Line(v,mark+outward*5,mark-outward*5+normal*5,2,Amber);
                            continue;
                        }
                    }
                    Contact(v,mark,site.Kind,ink);
                    if(Mathf.Abs(p.y)>30)
                    {
                        float sign=Mathf.Sign(p.y);
                        Line(v,mark+new Vector2(-3,sign*8),mark+new Vector2(0,sign*11),1.5f,ink);
                        Line(v,mark+new Vector2(0,sign*11),mark+new Vector2(3,sign*8),1.5f,ink);
                    }
                }
            }
            else for(int i=Route.Current;i<Route.Gates.Length;i++)
            {
                Vector3 p=Ship.transform.InverseTransformPoint(Route.Gates[i].position);
                Vector2 mark=center+Vector2.ClampMagnitude(new Vector2(p.x,p.z)*.18f,100);
                if(i==Route.Current)Line(v,center,mark,2,new Color(.4f,.25f,.1f));
                Arc(v,mark,i==Route.Current?9:4,0,360,3,i==Route.Current?Amber:Dim);
            }
            Line(v,center+new Vector2(-10,-8),center+new Vector2(0,13),2,Pale);Line(v,center+new Vector2(0,13),center+new Vector2(10,-8),2,Pale);
            Line(v,center+new Vector2(-10,-8),center+new Vector2(0,-3),2,Pale);Line(v,center+new Vector2(10,-8),center+new Vector2(0,-3),2,Pale);
            // A rotating scan is deliberately slow; stars and the outside world have no twinkle animation.
            float sweep=Time.time*18f;Line(v,center,center+Polar(108,sweep),1,new Color(.12f,.65f,.65f,.35f));
        }
        static void Contact(VertexHelper v,Vector2 p,SiteKind kind,Color ink)
        {
            if(kind==SiteKind.Anomaly){Arc(v,p,4,0,360,2,ink);return;}
            if(kind==SiteKind.Mineral)
            {Line(v,p+new Vector2(-4,-4),p+new Vector2(4,4),2,ink);Line(v,p+new Vector2(-4,4),p+new Vector2(4,-4),2,ink);return;}
            if(kind==SiteKind.Relay)
            {Line(v,p+new Vector2(0,-5),p+new Vector2(0,5),2,ink);Line(v,p+new Vector2(-4,2),p+new Vector2(4,2),2,ink);return;}
            bool diamond=kind==SiteKind.Wreck;
            Vector2 a=diamond?new Vector2(0,5):new Vector2(-4,4),b=diamond?new Vector2(5,0):new Vector2(4,4),
                c=diamond?new Vector2(0,-5):new Vector2(4,-4),d=diamond?new Vector2(-5,0):new Vector2(-4,-4);
            Line(v,p+a,p+b,2,ink);Line(v,p+b,p+c,2,ink);Line(v,p+c,p+d,2,ink);Line(v,p+d,p+a,2,ink);
            if(kind==SiteKind.Archive)Line(v,p+new Vector2(-2,0),p+new Vector2(2,0),2,ink);
        }
        void Velocity(VertexHelper v)
        {
            Vector2 c=new Vector2(-105,5);
            Arc(v,c,110,35,325,1,Dim);Arc(v,c,103,35,325,7,Dim);
            Arc(v,c,103,35,35+Mathf.Clamp01(Ship.Speed/110)*290,7,Cyan);
            for(int i=0;i<=22;i++)Line(v,c+Polar(113,35+i*290f/22),c+Polar(i%2==0?123:118,35+i*290f/22),1.5f,i%2==0?Pale:Dim);
            for(int i=0;i<16;i++)
            {
                float y=-100+i*13;
                Line(v,new Vector2(95,y),new Vector2(130,y),7,i/16f<Ship.Thrust?(Ship.Boost?Amber:Cyan):Dim);
                Line(v,new Vector2(165,y),new Vector2(197,y),7,i/16f<Mathf.Abs(Ship.Translation.y)?Cyan:Dim);
            }
            Line(v,new Vector2(-235,-140),new Vector2(225,-140),1,Dim);
        }
        void Systems(VertexHelper v)
        {
            Vector2 c=new Vector2(-118,10);
            Arc(v,c,100,0,360,1,Dim);Arc(v,c,89,-90,-90+Ship.Hull*3.6f,5,Ship.Hull>30?Cyan:Amber);
            // Top-down schematic of the actual single-seat spacecraft.
            Vector2[] p={new Vector2(0,65),new Vector2(19,21),new Vector2(52,-38),new Vector2(18,-28),new Vector2(14,-58),new Vector2(-14,-58),new Vector2(-18,-28),new Vector2(-52,-38),new Vector2(-19,21),new Vector2(0,65)};
            for(int i=1;i<p.Length;i++)Line(v,c+p[i-1],c+p[i],2,Cyan);
            Line(v,c+new Vector2(-8,-48),c+new Vector2(-8,-65-Ship.Thrust*15),4,Ship.EngineOn?Amber:Dim);
            Line(v,c+new Vector2(8,-48),c+new Vector2(8,-65-Ship.Thrust*15),4,Ship.EngineOn?Amber:Dim);
            for(int row=0;row<3;row++)for(int i=0;i<10;i++)
            {bool on=row==0?Ship.EngineOn:row==1?Ship.AssistOn:row==2?i<Ship.Hull/10:!Ship.Brake;
                Line(v,new Vector2(35+i*18,80-row*55),new Vector2(45+i*18,80-row*55),7,on?(row==2?Cyan:Amber):Dim);}
        }
        void Attitude(VertexHelper v)
        {
            Quaternion inverse=Quaternion.Inverse(Ship.transform.rotation);
            for(int j=-3;j<=3;j++)
            {
                Vector3 a=inverse*new Vector3(-55,j*24,0),b=inverse*new Vector3(55,j*24,0);
                Line(v,a,b,j==0?2:1,j==0?Cyan:Dim);
            }
            Arc(v,Vector2.zero,95,0,360,1,Dim);
            Line(v,new Vector2(-75,0),new Vector2(-18,0),2,Amber);Line(v,new Vector2(18,0),new Vector2(75,0),2,Amber);
            Line(v,new Vector2(-18,0),new Vector2(0,-9),2,Amber);Line(v,new Vector2(0,-9),new Vector2(18,0),2,Amber);
        }
        static Vector2 Polar(float r,float degrees){float a=degrees*Mathf.Deg2Rad;return new Vector2(Mathf.Sin(a),Mathf.Cos(a))*r;}
        static void Arc(VertexHelper v,Vector2 c,float r,float from,float to,float width,Color color)
        {int count=Mathf.Max(1,Mathf.CeilToInt((to-from)/4));for(int i=0;i<count;i++)Line(v,c+Polar(r,Mathf.Lerp(from,to,i/(float)count)),c+Polar(r,Mathf.Lerp(from,to,(i+1f)/count)),width,color);}
        static void Line(VertexHelper v,Vector2 a,Vector2 b,float width,Color color)
        {
            Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int index=v.currentVertCount;
            v.AddVert(a-n,color,Vector2.zero);v.AddVert(a+n,color,Vector2.zero);v.AddVert(b+n,color,Vector2.zero);v.AddVert(b-n,color,Vector2.zero);
            v.AddTriangle(index,index+1,index+2);v.AddTriangle(index,index+2,index+3);
        }
        public static FlightInstrument Create(Transform parent,Instrument kind,Vector2 position,ShipFlightController ship)
        {
            var f=new GameObject(kind+" / vector graphics",typeof(RectTransform),typeof(CanvasRenderer),typeof(FlightInstrument)).GetComponent<FlightInstrument>();
            f.transform.SetParent(parent,false);f.rectTransform.anchoredPosition=position;f.rectTransform.sizeDelta=new Vector2(600,400);
            f.raycastTarget=false;f.Kind=kind;f.Ship=ship;
            // Keep vector instruments on Unity UI material so their batching matches the canvas.
            return f;
        }
    }
}
