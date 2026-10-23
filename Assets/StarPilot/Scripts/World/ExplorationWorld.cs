using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarPilot
{
    /// <summary>Bounded resident sectors, stable coordinates and a floating physics origin.</summary>
    [DefaultExecutionOrder(70)]
    public sealed class ExplorationWorld : MonoBehaviour
    {
        public const float SectorSize=1000;
        public int Seed=1729;
        public ShipFlightController Ship;
        public ExplorationController Expedition;
        public bool ExplorationEnabled {get;private set;}
        public SectorAddress Origin {get;private set;}
        public SectorAddress CurrentSector=>new SectorAddress(Origin.X+(long)Math.Floor(Ship.Body.position.x/SectorSize),
            Origin.Y+(long)Math.Floor(Ship.Body.position.y/SectorSize),Origin.Z+(long)Math.Floor(Ship.Body.position.z/SectorSize));
        public int LoadedSectorCount=>sectors.Count;
        public IReadOnlyList<ExplorationSite> Sites=>sites;
        public ExplorationSite Home {get;private set;}
        public int GeneratedSectorCount {get;private set;}
        public readonly ISectorLayoutSource LayoutSource=new SeededSectorLayoutSource();
        readonly Dictionary<SectorAddress,GameObject> sectors=new Dictionary<SectorAddress,GameObject>();
        readonly List<ExplorationSite> sites=new List<ExplorationSite>();
        readonly Queue<SectorAddress> pending=new Queue<SectorAddress>();
        readonly GameObject[] templates=new GameObject[4];
        readonly List<Mesh> rockMeshes=new List<Mesh>();
        Transform environment,legacyDebris,celestial,streamRoot,prototypeRoot;
        Material rockMaterial;
        SectorAddress previousCenter;
        bool ready,centerKnown;

        public void Initialize(ShipFlightController ship,Transform originalEnvironment,Transform debris,Transform station,Geometry g,
            Material rock,Material hull,Material trim,Material dark,Material cyan,Material amber,Material solar)
        {
            Ship=ship;environment=originalEnvironment;legacyDebris=debris;rockMaterial=rock;
            streamRoot=new GameObject("Resident exploration sectors").transform;streamRoot.SetParent(transform,false);
            prototypeRoot=new GameObject("Shared sector prototypes").transform;prototypeRoot.SetParent(transform,false);prototypeRoot.gameObject.SetActive(false);
            celestial=new GameObject("Distant orbital backdrop").transform;celestial.SetParent(transform,false);
            for(int i=environment.childCount-1;i>=0;i--)
            {
                Transform child=environment.GetChild(i);
                if(child.name=="KEPLER / ocean world" || child.name=="Atmospheric limb")child.SetParent(celestial,true);
            }
            for(int i=0;i<6;i++)rockMeshes.Add(debris.Find("Asteroid "+i).GetComponent<MeshFilter>().sharedMesh);
            Home=station.gameObject.AddComponent<ExplorationSite>();Home.Id="kepler";Home.Kind=SiteKind.Station;sites.Add(Home);
            var salvage=g.Material("Survey wreck / weathered plating",new Color(.19f,.27f,.3f),.45f,.28f);
            g.DetailMaterial(salvage,"blue_metal_plate",3,.55f);
            for(int kind=0;kind<4;kind++)
            {
                var model=new GameObject(((SiteKind)kind)+" / shared model").transform;model.SetParent(prototypeRoot,false);templates[kind]=model.gameObject;
                if(kind==(int)SiteKind.Wreck)
                {
                    var fuselage=g.Panel("Broken survey fuselage",model,Vector3.zero,new Vector3(10,7,23),salvage,new Vector3(12,17,22),1.2f).transform;
                    g.Panel("Collapsed forward viewport",fuselage,new Vector3(0,.65f,-11.4f),new Vector3(6,2.7f,.3f),dark,default,.5f);
                    for(int i=0;i<4;i++)
                    {
                        g.Panel("Belly access hatch",fuselage,new Vector3(-3.3f+i*2.2f,-1.5f,-11.6f),new Vector3(1.1f,1.6f,.15f),i==0?amber:trim,new Vector3(0,0,i*9));
                        g.Box("Hull cross rib",fuselage,new Vector3(0,0,-8+i*5),new Vector3(10.2f,7.2f,.25f),trim);
                        g.Box("Dark rib inset",fuselage,new Vector3(0,-.1f,-8.2f+i*5),new Vector3(9.8f,6.8f,.18f),salvage);
                    }
                    g.Box("Pressure hull collision",model,Vector3.zero,new Vector3(9,6,20),dark,new Vector3(12,17,22),true).GetComponent<Renderer>().enabled=false;
                    g.Torus("Exposed engine coupling",model,new Vector3(0,0,11),3,.5f,trim,32);
                    for(int s=-1;s<=1;s+=2)
                    {
                        g.Beam("Fractured panel support",model,new Vector3(s*4,0,0),new Vector3(s*15,s*3,2),.6f,trim);
                        g.Panel("Damaged solar wing",model,new Vector3(s*13,s*3,2),new Vector3(10,.3f,9),solar,new Vector3(s*20,0,s*13));
                    }
                    for(int i=0;i<5;i++)g.Panel("Torn armour",model,new Vector3(-4+i*2,-2,9+i%2),new Vector3(1.5f,2.5f,.4f),trim,new Vector3(25+i*9,i*17,0));
                    g.Box("Emergency transponder",model,new Vector3(0,4,0),new Vector3(1,.5f,2),amber);
                    var cargo=new GameObject("Recoverable black box").transform;cargo.SetParent(model,false);cargo.localPosition=new Vector3(0,-2,-14);
                    g.Panel("Recorder canister",cargo,Vector3.zero,new Vector3(2.5f,1.6f,3.3f),amber,default,.3f);
                    for(int s=-1;s<=1;s+=2)g.Box("Recorder straps",cargo,new Vector3(s*.8f,0,0),new Vector3(.18f,1.8f,3.5f),dark);
                }
                else if(kind==(int)SiteKind.Anomaly)
                {
                    var core=g.Sphere("Ionised core",model,Vector3.zero,Vector3.one*8,cyan);g.RefineSphere(core,32,24);
                    for(int i=0;i<7;i++)
                    {
                        var shard=g.Panel("Resonant crystal",model,new Vector3(Mathf.Sin(i)*7,Mathf.Cos(i*2)*5,Mathf.Cos(i)*7),new Vector3(1.1f,12,1.8f),cyan,new Vector3(i*27,i*43,i*19),.3f);
                        shard.transform.localScale=Vector3.one*(.55f+i*.09f);
                    }
                    g.Torus("Field emitter",model,Vector3.zero,10,.23f,trim,48).transform.localRotation=Quaternion.Euler(35,20,18);
                }
                else if(kind==(int)SiteKind.Mineral)
                {
                    var asteroid=new GameObject("Mineral host",typeof(MeshFilter),typeof(MeshRenderer),typeof(SphereCollider));asteroid.transform.SetParent(model,false);
                    asteroid.transform.localScale=new Vector3(25,21,28);asteroid.GetComponent<MeshFilter>().sharedMesh=rockMeshes[3];asteroid.GetComponent<Renderer>().sharedMaterial=rock;
                    asteroid.GetComponent<SphereCollider>().radius=.48f;
                    for(int i=0;i<8;i++)g.Panel("Exposed mineral vein",model,new Vector3(Mathf.Sin(i*1.9f)*8,Mathf.Cos(i)*7,-7),new Vector3(.8f,4.5f,1.1f),cyan,new Vector3(i*22,0,i*37),.2f);
                }
                else
                {
                    g.Panel("Survey outpost",model,Vector3.zero,new Vector3(18,14,34),hull,default,3);
                    g.Box("Outpost collision",model,Vector3.zero,new Vector3(18,14,34),dark,default,true).GetComponent<Renderer>().enabled=false;
                    g.Torus("Service collar",model,new Vector3(0,0,-19),7,1.1f,trim,48);
                    g.Torus("Service lamps",model,new Vector3(0,0,-20),7,.16f,cyan,48);
                    for(int s=-1;s<=1;s+=2)
                    {
                        g.Beam("Array spar",model,new Vector3(0,0,5),new Vector3(s*42,0,5),1,trim);
                        g.Panel("Power array",model,new Vector3(s*32,0,5),new Vector3(26,34,.5f),solar);
                        for(int i=0;i<5;i++)g.Box("Array grid",model,new Vector3(s*32,-12+i*6,4.6f),new Vector3(26,.2f,.1f),trim);
                        g.Box("Dock approach mast",model,new Vector3(s*11,-7,-30),new Vector3(.5f,12,.5f),hull);
                        g.Box("Dock approach light",model,new Vector3(s*11,-1,-30),new Vector3(1,1,1),cyan);
                    }
                }
            }
            StartCoroutine(EnableAfterPrototypeCleanup());
        }
        IEnumerator EnableAfterPrototypeCleanup(){yield return null;ready=true;}
        public void SetExploration(bool enabled)
        {
            ExplorationEnabled=enabled;streamRoot.gameObject.SetActive(enabled);legacyDebris.gameObject.SetActive(!enabled);
            centerKnown=false;
        }
        void Update()
        {
            if(!ready || !ExplorationEnabled)return;
            RebaseIfNeeded();
            var center=CurrentSector;
            if(!centerKnown || !center.Equals(previousCenter))RefreshNeighborhood(center);
            // At most two sectors per frame; models and materials are shared, never regenerated on return.
            for(int count=0;count<2 && pending.Count>0;count++)
            {
                var address=pending.Dequeue();if(!sectors.ContainsKey(address))BuildSector(address);
            }
        }
        void LateUpdate(){if(celestial)celestial.position=ExplorationEnabled?Ship.transform.position:Vector3.zero;}
        void RefreshNeighborhood(SectorAddress center)
        {
            previousCenter=center;centerKnown=true;pending.Clear();
            var remove=new List<SectorAddress>();
            foreach(var entry in sectors)
                if(Math.Abs(entry.Key.X-center.X)>1 || Math.Abs(entry.Key.Y-center.Y)>1 || Math.Abs(entry.Key.Z-center.Z)>1)remove.Add(entry.Key);
            foreach(var key in remove)
            {
                var root=sectors[key];foreach(var site in root.GetComponentsInChildren<ExplorationSite>())sites.Remove(site);
                root.SetActive(false);Destroy(root);sectors.Remove(key);
            }
            if(!sectors.ContainsKey(center))pending.Enqueue(center);
            for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
            {
                var key=new SectorAddress(center.X+x,center.Y+y,center.Z+z);
                if(!key.Equals(center) && !sectors.ContainsKey(key))pending.Enqueue(key);
            }
        }
        Vector3 LocalSectorPosition(SectorAddress address)=>new Vector3((float)(address.X-Origin.X)*SectorSize,(float)(address.Y-Origin.Y)*SectorSize,(float)(address.Z-Origin.Z)*SectorSize);
        void BuildSector(SectorAddress address)
        {
            var layout=LayoutSource.Generate(Seed,address);
            var root=new GameObject("Sector "+address);root.transform.SetParent(streamRoot,false);root.transform.localPosition=LocalSectorPosition(address);
            sectors.Add(address,root);GeneratedSectorCount++;
            for(int i=0;i<layout.Rocks.Length;i++)
            {
                var asteroid=new GameObject("Sector regolith",typeof(MeshFilter),typeof(MeshRenderer),typeof(SphereCollider));asteroid.transform.SetParent(root.transform,false);
                asteroid.transform.localPosition=layout.Rocks[i];asteroid.transform.localScale=Vector3.one*layout.RockSizes[i];
                asteroid.transform.localRotation=Quaternion.Euler(i*39+layout.Theme*23,i*71,i*17);
                asteroid.GetComponent<MeshFilter>().sharedMesh=rockMeshes[(i+layout.Theme)%rockMeshes.Count];asteroid.GetComponent<Renderer>().sharedMaterial=rockMaterial;
                asteroid.GetComponent<SphereCollider>().radius=.48f;
            }
            for(int i=0;i<layout.Sites.Length;i++)
            {
                var data=layout.Sites[i];var model=Instantiate(templates[(int)data.Kind],root.transform);model.name=data.Kind+" / survey destination";
                model.transform.localPosition=data.Position;model.transform.localRotation=Quaternion.Euler(0,data.Yaw,0);
                var site=model.AddComponent<ExplorationSite>();site.Id=$"{Seed}/{address}/{i}";site.Kind=data.Kind;
                site.CargoVisual=model.transform.Find("Recoverable black box");Expedition?.RestoreSite(site);model.SetActive(true);sites.Add(site);
            }
        }
        public void RebaseIfNeeded()
        {
            Vector3 p=Ship.Body.position;
            if(Mathf.Max(Mathf.Abs(p.x),Mathf.Abs(p.y),Mathf.Abs(p.z))<2400)return;
            var cells=new SectorAddress((long)Math.Floor(p.x/SectorSize),(long)Math.Floor(p.y/SectorSize),(long)Math.Floor(p.z/SectorSize));
            Vector3 shift=new Vector3(cells.X*SectorSize,cells.Y*SectorSize,cells.Z*SectorSize);
            Origin=new SectorAddress(Origin.X+cells.X,Origin.Y+cells.Y,Origin.Z+cells.Z);
            var interpolation=Ship.Body.interpolation;Ship.Body.interpolation=RigidbodyInterpolation.None;
            Ship.Body.position=p-shift;Ship.transform.position=Ship.Body.position;
            foreach(var sector in sectors)sector.Value.transform.localPosition=LocalSectorPosition(sector.Key);
            PlaceHomeEnvironment();Physics.SyncTransforms();StartCoroutine(RestoreInterpolation(interpolation));
        }
        IEnumerator RestoreInterpolation(RigidbodyInterpolation mode){yield return new WaitForFixedUpdate();if(Ship)Ship.Body.interpolation=mode;}
        void PlaceHomeEnvironment()
        {
            bool nearby=Math.Abs(Origin.X)<8 && Math.Abs(Origin.Y)<8 && Math.Abs(Origin.Z)<8;
            environment.gameObject.SetActive(nearby);
            if(nearby)environment.position=new Vector3(-Origin.X*SectorSize,-Origin.Y*SectorSize,-Origin.Z*SectorSize);
        }
        public void ReturnToOrigin()
        {
            Origin=new SectorAddress();foreach(var entry in sectors){entry.Value.SetActive(false);Destroy(entry.Value);}sectors.Clear();pending.Clear();
            sites.Clear();sites.Add(Home);environment.gameObject.SetActive(true);environment.position=Vector3.zero;celestial.position=Vector3.zero;centerKnown=false;
        }
    }
}
