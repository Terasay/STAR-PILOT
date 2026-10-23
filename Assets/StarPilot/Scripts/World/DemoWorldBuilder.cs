using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StarPilot
{
    /// <summary>Orbital scene and flight vehicle, assembled from reusable runtime modules.</summary>
    public sealed class DemoWorldBuilder
    {
        readonly Geometry g;
        readonly List<Object> owned;
        Material hull, trim, dark, cyan, white, amber, rock, solar;
        public ExplorationWorld ExplorationWorld {get;private set;}
        static readonly Color Cyan = new Color(0.18f, 0.8f, 0.92f);
        static readonly Color TextColor = new Color(0.72f, 0.84f, 0.89f);
        public DemoWorldBuilder(List<Object> ownedObjects) { owned = ownedObjects; g = new Geometry(owned); }

        public void Build(Transform root, out ShipFlightController ship, out Camera camera,
            out FlightRoute route, out CockpitDisplays displays)
        {
            hull = g.Material("Titanium / graphite", new Color(.18f,.22f,.26f), .5f, .48f);
            trim = g.Material("Machined aluminium", new Color(0.25f,0.3f,0.34f), 0.8f, 0.7f);
            dark = g.Material("Ceramic / charcoal", new Color(0.012f,0.019f,0.027f), 0.15f, 0.4f);
            cyan = g.Material("Cyan instrument light", Cyan, 0.2f, 0.6f, 3f);
            white = g.Material("Warm white light", new Color(0.85f,0.86f,0.8f), 0.2f, 0.5f, 3f);
            amber = g.Material("Amber caution light", new Color(1f,0.44f,0.12f), 0.2f, 0.5f, 2.2f);
            rock = g.Material("Regolith / scanned dry rock", new Color(.55f,.58f,.62f), .02f, .16f);
            rock.SetFloat("_Rock",1);
            rock.SetTexture("_RockColor",Resources.Load<Texture2D>("StarPilot/Textures/Rock_diffuse"));
            rock.SetTexture("_RockNormal",Resources.Load<Texture2D>("StarPilot/Textures/Rock_nor_gl"));
            rock.SetTexture("_RockRoughness",Resources.Load<Texture2D>("StarPilot/Textures/Rock_rough"));
            solar = g.Material("Photovoltaic cells", new Color(0.025f,0.055f,0.1f), 0.72f, 0.85f);

            var shipObject = new GameObject("SP-01 / Flight vehicle", typeof(Rigidbody));
            shipObject.transform.SetParent(root, false);
            var collision = shipObject.AddComponent<BoxCollider>();
            collision.center = new Vector3(0f, 1.2f, 0.4f);
            collision.size = new Vector3(3.4f, 2.7f, 5f);
            ship = shipObject.AddComponent<ShipFlightController>();
            camera = new GameObject("Pilot camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetParent(shipObject.transform, false);
            camera.transform.localPosition = new Vector3(0f,1.62f,-0.25f);
            camera.fieldOfView = 74f; camera.nearClipPlane = 0.04f; camera.farClipPlane = 25000f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = true;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            camera.allowMSAA=true;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.antialiasingQuality=AntialiasingQuality.High;

            displays = shipObject.AddComponent<CockpitDisplays>();
            displays.Ship = ship;
            new CockpitBuilder(g,hull,trim,dark,cyan,amber,white).Build(shipObject.transform, displays);
            BuildLighting(root);
            BuildSpace(root);
            route = BuildRoute(root, ship);
            displays.Route = route;
            foreach(var instrument in displays.Instruments) instrument.Route=route;
        }

        void BuildLighting(Transform root)
        {
            RenderSettings.skybox=new Material(Resources.Load<Shader>("StarPilot/DeepSpace"));owned.Add(RenderSettings.skybox);
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.16f,.20f,.27f);
            RenderSettings.fog=false;
            var sun=new GameObject("Kepler / primary star",typeof(Light)).GetComponent<Light>();sun.transform.SetParent(root,false);
            Vector3 sunDirection=new Vector3(.65f,.35f,.7f).normalized;
            sun.transform.rotation=Quaternion.LookRotation(-sunDirection);sun.type=LightType.Directional;
            sun.color=new Color(1f,.86f,.69f);sun.intensity=2.6f;sun.shadows=LightShadows.Soft;
            RenderSettings.skybox.SetVector("_SunDirection",sunDirection);
            RenderSettings.sun=sun;
            // Broad cool fill approximates light returned by the nearby planet.
            var bounce=new GameObject("Kepler / planetary bounce",typeof(Light)).GetComponent<Light>();
            bounce.transform.SetParent(root,false);bounce.type=LightType.Directional;
            bounce.transform.rotation=Quaternion.LookRotation(new Vector3(.5f,.25f,1));
            bounce.color=new Color(.48f,.69f,1f);bounce.intensity=.65f;bounce.shadows=LightShadows.None;
            var volume=new GameObject("Restrained cinematic grade",typeof(Volume)).GetComponent<Volume>();volume.transform.SetParent(root,false);volume.isGlobal=true;
            var profile=Resources.Load<VolumeProfile>("StarPilot/FlightGrade");
            if(!profile)
            {
                profile=ScriptableObject.CreateInstance<VolumeProfile>();owned.Add(profile);
                var tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.ACES);
                var bloom=profile.Add<Bloom>(true);bloom.threshold.Override(1.15f);bloom.intensity.Override(.28f);bloom.scatter.Override(.65f);
                var vignette=profile.Add<Vignette>(true);vignette.intensity.Override(.18f);vignette.smoothness.Override(.5f);
            }
            volume.sharedProfile=profile;
        }

        void BuildSpace(Transform root)
        {
            var space=new GameObject("Kepler / orbital environment").transform;space.SetParent(root,false);
            var planetMaterial=new Material(Resources.Load<Shader>("StarPilot/Planet")){name="Kepler planet"};owned.Add(planetMaterial);
            planetMaterial.SetTexture("_Surface",Resources.Load<Texture2D>("StarPilot/Textures/BlueMarble"));
            planetMaterial.SetTexture("_Night",Resources.Load<Texture2D>("StarPilot/Textures/BlackMarble"));
            Vector3 planetPosition=new Vector3(-5900,-2900,9800);
            var planet=g.Sphere("KEPLER / ocean world",space,planetPosition,Vector3.one*10500,planetMaterial);
            planet.transform.rotation=Quaternion.Euler(18,15,-24);
            g.RefineSphere(planet);
            var atmosphereMaterial=new Material(Resources.Load<Shader>("StarPilot/Atmosphere")){name="Blue atmospheric scattering"};owned.Add(atmosphereMaterial);
            var atmosphere=g.Sphere("Atmospheric limb",space,planetPosition,Vector3.one*10600,atmosphereMaterial);
            atmosphere.GetComponent<MeshFilter>().sharedMesh=planet.GetComponent<MeshFilter>().sharedMesh;
            Transform station=BuildStation(space,new Vector3(170,16,1060));
            var debris=new GameObject("Training debris field").transform;debris.SetParent(space,false);
            var random=new System.Random(1729);
            for (int i=0;i<94;i++)
            {
                float z=90+(float)random.NextDouble()*2100;
                float angle=(float)random.NextDouble()*Mathf.PI*2;
                float radius=75+(float)random.NextDouble()*440;
                Vector3 position=new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius*.65f,z);
                float size=8+(float)random.NextDouble()*38;
                if(i==0){position=new Vector3(71,32,205);size=37;}
                if(i==1){position=new Vector3(-83,62,310);size=44;}
                if(i==2){position=new Vector3(135,-72,410);size=52;}
                var asteroid=g.Sphere("Asteroid "+i,debris,position,new Vector3(size,size*(.65f+(float)random.NextDouble()*.35f),size*.86f),rock,true);
                asteroid.transform.rotation=RandomRotation(random);
                g.RefineSphere(asteroid,i<20?64:32,i<20?40:24);
                var filter=asteroid.GetComponent<MeshFilter>();var mesh=filter.sharedMesh;mesh.name="Fractured regolith "+i;
                Vector3[] vertices=mesh.vertices;
                float seed=i*7.31f;
                var cuts=new Vector3[9];var depths=new float[9];
                for(int cut=0;cut<cuts.Length;cut++)
                {
                    cuts[cut]=new Vector3((float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1).normalized;
                    depths[cut]=.33f+(float)random.NextDouble()*.14f;
                }
                for(int v=0;v<vertices.Length;v++)
                {
                    Vector3 p=vertices[v];
                    float broad=Mathf.PerlinNoise(p.x*3+seed,p.y*3+p.z*2+seed);
                    float fine=Mathf.PerlinNoise(p.x*18+seed,p.z*18+p.y*11+seed);
                    float fracture=Mathf.Abs(Mathf.PerlinNoise(p.z*6+seed,p.y*6+seed)-.5f);
                    Vector3 direction=p.normalized;
                    float radial=.5f*(.83f+broad*.3f-fracture*.12f);
                    for(int cut=0;cut<cuts.Length;cut++){float projection=Vector3.Dot(direction,cuts[cut]);if(projection>.1f)radial=Mathf.Min(radial,depths[cut]/projection);}
                    vertices[v]=direction*(radial+(fine-.5f)*.017f);
                }
                mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
                // Conservative sphere collisions are sufficient for this distant debris field.
            }
            ExplorationWorld=new GameObject("Exploration world",typeof(ExplorationWorld)).GetComponent<ExplorationWorld>();
            ExplorationWorld.transform.SetParent(root,false);
            ExplorationWorld.Initialize(root.GetComponentInChildren<ShipFlightController>(),space,debris,station,g,rock,hull,trim,dark,cyan,amber,solar);
        }

        Transform BuildStation(Transform root,Vector3 position)
        {
            var station=new GameObject("KEPLER / orbital research station").transform;station.SetParent(root,false);station.localPosition=position;
            station.localRotation=Quaternion.Euler(0,-18,-12);
            g.Torus("Habitat ring",station,Vector3.zero,65,4,hull,96);
            g.Torus("Habitat light ribbon",station,new Vector3(0,0,-4),65,.4f,cyan,96);
            g.Torus("Aft pressure ring",station,new Vector3(0,0,16),65,2.5f,hull,96);
            g.Torus("Docking collar",station,new Vector3(0,0,-43),10,1.8f,trim,64);
            g.Torus("Docking approach lights",station,new Vector3(0,0,-45),10,.35f,white,64);
            g.Panel("Docking bay face",station,new Vector3(0,0,-40),new Vector3(18,18,2),dark,default,4);
            for(int module=0;module<24;module++)
            {
                float angle=module*Mathf.PI*2/24;Vector3 point=new Vector3(Mathf.Sin(angle)*65,Mathf.Cos(angle)*65,0);
                var block=g.Panel("Habitat pressure compartment",station,point,new Vector3(13,7,14),hull,new Vector3(0,0,-module*15),1.5f);
                for(int window=0;window<4;window++)g.Box("Habitat observation window",block.transform,new Vector3(-4.5f+window*3,0,-7.1f),new Vector3(1.5f,.8f,.08f),module%3==0?amber:white);
                g.Beam("Ring axial truss",station,point,point+Vector3.forward*16,.65f,trim);
            }
            g.Box("Central command module",station,Vector3.zero,new Vector3(18,18,75),hull,default,true);
            for(int i=0;i<6;i++)
            {
                float a=i*Mathf.PI/3f;Vector3 end=new Vector3(Mathf.Cos(a)*65,Mathf.Sin(a)*65,0);
                g.Beam("Habitat spoke",station,Vector3.zero,end,2.8f,trim);
                g.Box("Habitat node",station,end,new Vector3(12,9,15),hull,new Vector3(0,0,i*60),true);
            }
            for(int side=-1;side<=1;side+=2)
            {
                g.Beam("Solar truss",station,new Vector3(side*8,0,25),new Vector3(side*155,0,25),2,trim);
                for(int i=0;i<4;i++)
                {
                    float x=side*(90+i*21);
                    g.Box("Solar array frame",station,new Vector3(x,0,25),new Vector3(19,65,1.2f),trim);
                    g.Box("Solar cells",station,new Vector3(x,0,24.3f),new Vector3(18,64,.15f),solar);
                    for(int row=-3;row<=3;row++)g.Box("Array busbar",station,new Vector3(x,row*8,24.15f),new Vector3(18,.3f,.1f),trim);
                }
            }
            var sign=CockpitDisplays.Screen(station,"Station callsign",new Vector3(0,18,-40),new Vector2(1100,220),Vector3.zero);
            sign.transform.localScale=Vector3.one*.018f;
            CockpitDisplays.Text(sign.transform,"KEPLER","K E P L E R",100,TextColor,Vector2.zero,new Vector2(1100,220),TextAnchor.MiddleCenter);
            return station;
        }

        FlightRoute BuildRoute(Transform root,ShipFlightController ship)
        {
            var routeObject=new GameObject("Flight corridor / 4 beacons");routeObject.transform.SetParent(root,false);
            var route=routeObject.AddComponent<FlightRoute>();route.Ship=ship;
            route.ActiveMaterial=cyan;route.InactiveMaterial=g.Material("Inactive beacon",new Color(.08f,.25f,.3f),.2f,.5f,.8f);
            route.CompleteMaterial=g.Material("Cleared beacon",new Color(.3f,.8f,.4f),.2f,.5f,2);
            Vector3[] positions={new Vector3(0,0,150),new Vector3(18,8,340),new Vector3(-20,2,550),new Vector3(0,-12,790)};
            route.Gates=new Transform[positions.Length];route.GateLights=new Renderer[positions.Length][];
            for(int i=0;i<positions.Length;i++)
            {
                var gate=new GameObject("Beacon "+(i+1)).transform;gate.SetParent(routeObject.transform,false);gate.localPosition=positions[i];route.Gates[i]=gate;
                g.Torus("Structural ring",gate,Vector3.zero,17.4f,.48f,hull);
                var light=g.Torus("Guidance ring",gate,new Vector3(0,0,-.51f),17.4f,.16f,cyan);
                route.GateLights[i]=new[]{light.GetComponent<Renderer>()};
                for(int segment=0;segment<24;segment++)
                {
                    float angle=segment*Mathf.PI*2/24;
                    var ringCollider=new GameObject("Ring collision");ringCollider.transform.SetParent(gate,false);
                    ringCollider.transform.localPosition=new Vector3(Mathf.Cos(angle)*17.4f,Mathf.Sin(angle)*17.4f,0);
                    ringCollider.transform.localRotation=Quaternion.Euler(0,0,segment*15f);
                    ringCollider.AddComponent<BoxCollider>().size=new Vector3(.96f,4.6f,.96f);
                }
                for(int j=0;j<4;j++)
                {
                    float a=j*Mathf.PI*.5f;
                    g.Box("Beacon housing",gate,new Vector3(Mathf.Cos(a)*17.4f,Mathf.Sin(a)*17.4f,0),new Vector3(1.5f,1.5f,1.8f),hull,new Vector3(0,0,j*90),true);
                }
                var sign=CockpitDisplays.Screen(gate,"Beacon number",new Vector3(0,21,0),new Vector2(300,140),Vector3.zero);sign.transform.localScale=Vector3.one*.024f;
                CockpitDisplays.Text(sign.transform,"Number",$"{i+1:00}",100,Cyan,Vector2.zero,new Vector2(300,140),TextAnchor.MiddleCenter);
            }
            return route;
        }
        static Quaternion RandomRotation(System.Random r)=>Quaternion.Euler((float)r.NextDouble()*360,(float)r.NextDouble()*360,(float)r.NextDouble()*360);
    }
}
