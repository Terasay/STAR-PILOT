using UnityEngine;
using UnityEngine.UI;

namespace StarPilot
{
    /// <summary>SP-01 industrial flight deck: layered panels, functional instruments and physical controls.</summary>
    public sealed class CockpitBuilder
    {
        readonly Geometry g;
        readonly Material hull,trim,dark,cyan,amber,white;
        Material face,suit,gripRubber;
        static readonly Color Ink=new Color(.62f,.79f,.8f),Cyan=new Color(.28f,.85f,.87f),Amber=new Color(1,.57f,.24f);
        public CockpitBuilder(Geometry geometry,Material h,Material t,Material d,Material c,Material a,Material w)
        {g=geometry;hull=h;trim=t;dark=d;cyan=c;amber=a;white=w;}
        public void Build(Transform ship,CockpitDisplays displays)
        {
            face=g.Material("Anodised instrument face",new Color(.19f,.23f,.25f),.35f,.46f);
            suit=g.Material("Pilot / woven pressure suit",new Color(.16f,.18f,.165f),0,.08f);
            gripRubber=g.Material("Micrograin grip rubber",new Color(.085f,.105f,.11f),.04f,.32f);
            g.DetailMaterial(gripRubber,"leather_white",7,.75f);
            g.DetailMaterial(face,"blue_metal_plate",2.4f,.5f);
            g.DetailMaterial(suit,"cotton_jersey",7,1.15f);
            suit.SetFloat("_Fabric",1);
            suit.SetFloat("_VertexTint",1);
            g.DetailMaterial(hull,"blue_metal_plate",.9f,.3f);
            g.DetailMaterial(trim,"blue_metal_plate",2f,.16f);
            CockpitDisplays.ScreenMaterial=g.ShaderMaterial("Phosphor / instrument glass","StarPilot/Instrument");
            var cockpit=new GameObject("SP-01 / pressure cockpit").transform;cockpit.SetParent(ship,false);
            Shell(cockpit);
            g.Windshield(cockpit,g.ShaderMaterial("Coated pressure glazing","StarPilot/Canopy"));
            var reflection=new GameObject("Cabin reflection capture",typeof(CockpitReflection)).transform;
            reflection.SetParent(cockpit,false);reflection.localPosition=new Vector3(0,1.6f,.1f);
            var interaction=ship.gameObject.AddComponent<CockpitInteraction>();interaction.Ship=displays.Ship;
            displays.Interaction=interaction;
            var lighting=ship.gameObject.AddComponent<ShipLighting>();
            displays.Lighting=lighting;
            Overhead(cockpit,displays,interaction,lighting);
            var instruments=new System.Collections.Generic.List<FlightInstrument>();
            var left=Monitor(cockpit,"KINETICS",new Vector3(-.64f,1.04f,1.19f),new Vector2(580,400),new Vector3(16,-12,0));
            instruments.Add(FlightInstrument.Create(left.transform,FlightInstrument.Instrument.Velocity,new Vector2(0,0),displays.Ship));
            displays.SpeedText=Label(left.transform,"Speed","000.0",57,new Vector2(-105,8),new Vector2(195,80),TextAnchor.MiddleCenter);
            Label(left.transform,"Unit","METRES / SECOND",17,new Vector2(-105,-50),new Vector2(195,25),TextAnchor.MiddleCenter);
            Label(left.transform,"Thrust label","THR    VRT",18,new Vector2(145,135),new Vector2(120,25),TextAnchor.MiddleCenter);
            displays.FlightMode=CockpitDisplays.Text(left.transform,"Flight mode","",29,Ink,new Vector2(0,-164),new Vector2(520,40),TextAnchor.MiddleCenter);

            var right=Monitor(cockpit,"VEHICLE SYSTEMS",new Vector3(.64f,1.04f,1.19f),new Vector2(580,400),new Vector3(16,12,0));
            instruments.Add(FlightInstrument.Create(right.transform,FlightInstrument.Instrument.Systems,Vector2.zero,displays.Ship));
            displays.SystemsText=CockpitDisplays.Text(right.transform,"Hull","",26,Ink,new Vector2(-118,-103),new Vector2(240,36),TextAnchor.MiddleCenter);
            for(int i=0;i<3;i++)Label(right.transform,"Channel "+i,new[]{"ENGINE","ASSIST","HULL"}[i],28,new Vector2(128,112-i*55),new Vector2(200,36));
            displays.StatusText=CockpitDisplays.Text(right.transform,"Primary status","",30,Cyan,new Vector2(0,-146),new Vector2(530,42),TextAnchor.MiddleCenter);
            displays.InteractionText=CockpitDisplays.Text(right.transform,"Control feedback","",24,Ink,new Vector2(0,-186),new Vector2(540,34),TextAnchor.MiddleCenter);
            var operations=CockpitDisplays.Rect(right.transform,"Survey operations",new Vector2(0,28),new Vector2(552,310),new Color(.003f,.013f,.019f));
            displays.OperationsPanel=operations.gameObject;
            displays.MissionTitle=CockpitDisplays.Text(operations.transform,"Operation title","",29,Cyan,new Vector2(0,112),new Vector2(530,44),TextAnchor.MiddleCenter);
            displays.MissionInstruction=CockpitDisplays.Text(operations.transform,"Flight task","",27,Ink,new Vector2(0,48),new Vector2(530,100),TextAnchor.MiddleCenter);
            CockpitDisplays.Rect(operations.transform,"Scan track",new Vector2(0,-23),new Vector2(480,7),new Color(.06f,.18f,.21f));
            displays.MissionProgress=CockpitDisplays.Rect(operations.transform,"Scan progress",new Vector2(-240,-23),new Vector2(0,7),Cyan).rectTransform;
            displays.MissionCargo=CockpitDisplays.Text(operations.transform,"Payload","",26,Ink,new Vector2(0,-65),new Vector2(530,40),TextAnchor.MiddleCenter);
            displays.MissionAccount=CockpitDisplays.Text(operations.transform,"Research account","",23,Ink,new Vector2(0,-103),new Vector2(540,35),TextAnchor.MiddleCenter);

            var navRoot=new GameObject("Central navigation housing").transform;navRoot.SetParent(cockpit,false);
            navRoot.localPosition=new Vector3(0,1.09f,1.1f);navRoot.localRotation=Quaternion.Euler(14,0,0);
            g.Panel("Navigation cradle",navRoot,new Vector3(0,0,.08f),new Vector3(.58f,.56f,.18f),dark,default,.12f);
            g.Torus("Machined instrument bezel",navRoot,Vector3.zero,.247f,.024f,trim,96);
            g.Torus("Inner gasket",navRoot,new Vector3(0,0,-.012f),.225f,.012f,dark,96);
            g.Torus("Navigation light guide",navRoot,new Vector3(0,0,-.017f),.232f,.002f,cyan,96);
            g.Sphere("Navigation LCD / circular black",navRoot,new Vector3(0,0,-.023f),new Vector3(.443f,.443f,.009f),dark);
            for(int j=0;j<8;j++){float a=j*Mathf.PI/4;g.Sphere("Bezel screw",navRoot,new Vector3(Mathf.Sin(a)*.25f,Mathf.Cos(a)*.25f,-.02f),Vector3.one*.012f,dark);}
            var nav=CockpitDisplays.Screen(navRoot,"Navigation / PPI",new Vector3(0,0,-.034f),new Vector2(420,420),Vector3.zero);
            instruments.Add(FlightInstrument.Create(nav.transform,FlightInstrument.Instrument.Navigation,Vector2.zero,displays.Ship));
            displays.RouteText=CockpitDisplays.Text(nav.transform,"Route","",27,Cyan,new Vector2(0,180),new Vector2(370,42),TextAnchor.MiddleCenter);
            displays.NavigationText=CockpitDisplays.Text(nav.transform,"Range","",38,Ink,new Vector2(0,-160),new Vector2(330,48),TextAnchor.MiddleCenter);
            displays.GuidanceText=CockpitDisplays.Text(nav.transform,"Course guidance","",26,Ink,new Vector2(0,-92),new Vector2(350,65),TextAnchor.MiddleCenter);

            for(int side=-1;side<=1;side+=2)
            {
                var auxiliary=Monitor(cockpit,side<0?"ATTITUDE / IMU":"FLIGHT CONTROL",new Vector3(side*1.13f,1.04f,.87f),new Vector2(280,430),new Vector3(20,side*28,0));
                if(side<0)
                {
                    instruments.Add(FlightInstrument.Create(auxiliary.transform,FlightInstrument.Instrument.Attitude,new Vector2(0,30),displays.Ship));
                    displays.AttitudeText=Label(auxiliary.transform,"Attitude data","P +000  Y +000\nR +000",17,new Vector2(0,-125),new Vector2(240,70),TextAnchor.MiddleCenter);
                }
                else
                {
                    for(int i=0;i<3;i++)
                    {
                        Label(auxiliary.transform,"Channel",new[]{"RCS / LATERAL","RCS / VERTICAL","RCS / ROLL"}[i],14,new Vector2(0,125-i*78),new Vector2(235,24));
                        CockpitDisplays.Rect(auxiliary.transform,"Channel track",new Vector2(0,90-i*78),new Vector2(230,6),new Color(.07f,.2f,.23f));
                        displays.AxisIndicators[i]=CockpitDisplays.Rect(auxiliary.transform,"Channel position",new Vector2(0,90-i*78),new Vector2(7,18),Cyan).rectTransform;
                    }
                    Label(auxiliary.transform,"RCS ready","6 DOF  /  MANUAL",14,new Vector2(0,-162),new Vector2(240,25));
                }
            }
            displays.Instruments=instruments.ToArray();
            Controls(cockpit,displays,interaction);
            SurveyControls(cockpit,displays,interaction);
            var hands=ship.gameObject.AddComponent<PilotHands>();hands.Interaction=interaction;
            hands.LeftRest=Rest(displays.Throttle,new Vector3(0,.30f,-.028f),new Vector3(8,-8,0));
            hands.RightRest=Rest(displays.Stick,new Vector3(.054f,.245f,-.025f),new Vector3(22,-12,-48));
            var gloveRubber=g.Material("Glove / flexible joints",new Color(.09f,.12f,.13f),0,.28f);
            var gloveArmour=g.Material("Glove / knuckle guards",new Color(.12f,.25f,.27f),.12f,.36f);
            g.DetailMaterial(gloveRubber,"leather_white",7,.65f);g.DetailMaterial(gloveArmour,"blue_metal_plate",3,.28f);
            hands.Build(g,cockpit,suit,gloveRubber,gloveArmour,cyan);

            BuildWorkingLights(cockpit,lighting);
        }
        void Shell(Transform p)
        {
            g.Box("Pressure floor",p,new Vector3(0,.15f,.15f),new Vector3(3.4f,.18f,4.5f),dark);
            g.Panel("Dashboard lower frame",p,new Vector3(0,.76f,1.42f),new Vector3(2.98f,.42f,.5f),hull,new Vector3(14,0,0),.15f);
            g.Panel("Glare shield",p,new Vector3(0,1.255f,1.5f),new Vector3(2.9f,.10f,.3f),dark,new Vector3(10,0,0),.04f);
            g.Box("Glare shield inset light",p,new Vector3(0,1.2f,1.32f),new Vector3(2.65f,.012f,.012f),cyan);
            for(int side=-1;side<=1;side+=2)
            {
                g.Panel("Side armour",p,new Vector3(side*1.48f,.68f,.2f),new Vector3(.18f,1.1f,3.9f),hull,new Vector3(0,0,side*-6));
                g.Panel("Console plinth",p,new Vector3(side*.83f,.69f,.22f),new Vector3(.68f,.18f,1.72f),hull,default,.06f);
                g.Panel("Console face",p,new Vector3(side*.83f,.792f,.22f),new Vector3(.61f,1.65f,.025f),face,new Vector3(90,0,0),.06f);
                g.Box("Console light channel",p,new Vector3(side*1.15f,.8f,.22f),new Vector3(.009f,.009f,1.5f),cyan);
                var service=new GameObject("Service access panel").transform;service.SetParent(p,false);
                service.localPosition=new Vector3(side*.88f,.809f,-.34f);service.localRotation=Quaternion.Euler(90,0,0);
                g.Panel("Service cover",service,Vector3.zero,new Vector3(.41f,.42f,.013f),dark,default,.03f);
                g.Panel("Service inset",service,new Vector3(0,0,-.008f),new Vector3(.37f,.38f,.007f),face,default,.026f);
                for(int j=0;j<8;j++)g.Box("Service ventilation",service,new Vector3(0,-.12f+j*.025f,-.016f),new Vector3(.26f,.008f,.007f),dark);
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)g.Sphere("Access bolt",service,new Vector3(x*.17f,y*.17f,-.015f),Vector3.one*.008f,trim);
                var serviceLabel=CockpitDisplays.Screen(service,"Access stencil",new Vector3(0,.135f,-.014f),new Vector2(340,45),Vector3.zero);
                Label(serviceLabel.transform,"Stencil",side<0?"LIFE SUPPORT / ACCESS 04":"RCS ACTUATORS / ACCESS 07",16,Vector2.zero,new Vector2(340,45),TextAnchor.MiddleCenter);
                Vector3 foot=new Vector3(side*1.43f,.95f,1.67f),roof=new Vector3(side*.87f,2.51f,1.35f);
                g.BeveledBeam("Canopy load frame",p,foot,roof,.10f,hull);
                g.Beam("Inner canopy seal",p,foot+new Vector3(-side*.05f,.01f,-.06f),roof+new Vector3(-side*.05f,0,-.06f),.024f,dark);
                g.Beam("Frame edge highlight",p,foot+new Vector3(side*.05f,0,-.04f),roof+new Vector3(side*.05f,0,-.04f),.014f,trim);
                g.BeveledBeam("Canopy roof spar",p,roof,new Vector3(side*.99f,2.47f,-1.6f),.13f,hull);
                g.BeveledBeam("Shoulder canopy brace",p,new Vector3(side*1.46f,.88f,-.32f),new Vector3(side*.99f,2.47f,-.55f),.13f,hull);
                g.BeveledBeam("Window sill",p,new Vector3(side*1.43f,1.01f,1.67f),new Vector3(side*1.46f,1.01f,-1.5f),.11f,hull);
                for(int bolt=1;bolt<7;bolt++)
                {
                    Vector3 location=Vector3.Lerp(foot,roof,bolt/7f)+new Vector3(0,0,-.052f);
                    g.Sphere("Canopy structural bolt",p,location,new Vector3(.018f,.018f,.009f),trim);
                    g.Box("Bolt slot",p,location-new Vector3(0,0,.006f),new Vector3(.009f,.002f,.001f),dark);
                }
                var frameLabel=CockpitDisplays.Screen(p,"Canopy safety stencil",Vector3.Lerp(foot,roof,.44f)+new Vector3(0,0,-.057f),new Vector2(250,100),new Vector3(12,0,-side*20));
                frameLabel.transform.localScale=Vector3.one*.0006f;
                Label(frameLabel.transform,"Pressure rating","CANOPY LOCK\nPRESSURISED",19,Vector2.zero,new Vector2(250,100),TextAnchor.MiddleCenter);
                g.Box("Window sill light",p,new Vector3(side*1.39f,1.056f,.2f),new Vector3(.014f,.014f,2.35f),white);
                for(int j=0;j<5;j++)
                {
                    g.Panel("Ribbed shell segment",p,new Vector3(side*1.39f,.54f,-1.2f+j*.57f),new Vector3(.06f,.5f,.51f),face,new Vector3(0,0,side*8));
                    g.Box("Floor reinforcement",p,new Vector3(side*.38f,.27f,-.1f+j*.3f),new Vector3(.58f,.025f,.035f),trim);
                }
                g.Panel("Rudder pedal",p,new Vector3(side*.31f,.38f,.95f),new Vector3(.24f,.31f,.06f),trim,new Vector3(55,0,0));
                for(int j=0;j<4;j++)g.Box("Pedal tread",p,new Vector3(side*.31f,.48f-j*.04f,.91f+j*.035f),new Vector3(.19f,.012f,.014f),dark);
            }
            g.BeveledBeam("Upper windscreen arch",p,new Vector3(-.87f,2.51f,1.35f),new Vector3(.87f,2.51f,1.35f),.13f,hull);
            g.Panel("Seat cushion",p,new Vector3(0,.45f,-.63f),new Vector3(.76f,.22f,.85f),dark);
            g.Panel("Seat back",p,new Vector3(0,1.04f,-1.05f),new Vector3(.82f,1.25f,.18f),dark,new Vector3(-8,0,0));
            g.Panel("Rear pressure bulkhead",p,new Vector3(0,1.4f,-2.08f),new Vector3(3.2f,2.6f,.2f),hull);
            g.Panel("Pressure hatch",p,new Vector3(0,1.35f,-1.95f),new Vector3(.95f,2.05f,.08f),dark,default,.16f);
        }
        void Overhead(Transform parent,CockpitDisplays displays,CockpitInteraction interaction,ShipLighting lighting)
        {
            var panel=new GameObject("Overhead / electrical distribution").transform;
            panel.SetParent(parent,false);panel.localPosition=new Vector3(0,2.30f,1.02f);
            panel.localRotation=Quaternion.Euler(-38,0,0);
            g.Panel("Overhead structural casting",panel,Vector3.zero,new Vector3(1.54f,.35f,.1f),hull,default,.065f);
            g.Panel("Overhead face gasket",panel,new Vector3(0,0,-.057f),new Vector3(1.49f,.31f,.018f),dark);
            g.Panel("Overhead brushed faceplate",panel,new Vector3(0,0,-.069f),new Vector3(1.46f,.29f,.012f),face);
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)
            {
                var bolt=g.Sphere("Overhead captive screw",panel,new Vector3(x*.718f,y*.13f,-.079f),new Vector3(.014f,.014f,.008f),trim);
                g.Box("Screw drive",bolt.transform,new Vector3(0,0,-.45f),new Vector3(.62f,.13f,.2f),dark);
            }
            var display=CockpitDisplays.Screen(panel,"Overhead annunciator",new Vector3(0,0,-.084f),new Vector2(570,250),Vector3.zero);
            CockpitDisplays.Rect(display.transform,"LCD",Vector2.zero,new Vector2(570,250),new Color(.007f,.025f,.034f));
            Label(display.transform,"Panel title","ELECTRICAL / FLIGHT DECK",23,new Vector2(0,96),new Vector2(530,34));
            CockpitDisplays.Rect(display.transform,"Title rule",new Vector2(0,63),new Vector2(530,2),Cyan);
            displays.OverheadText=Label(display.transform,"Lighting telemetry","",27,new Vector2(0,-17),new Vector2(530,132));
            for(int side=-1;side<=1;side+=2)
            {
                var module=new GameObject(side<0?"Floodlight control":"Cabin lighting control").transform;
                module.SetParent(panel,false);module.localPosition=new Vector3(side*.51f,0,-.082f);
                g.Panel("Switch module gasket",module,Vector3.zero,new Vector3(.37f,.25f,.02f),dark);
                g.Panel("Switch module plate",module,new Vector3(0,0,-.014f),new Vector3(.35f,.23f,.012f),hull);
                var labels=CockpitDisplays.Screen(module,"Laser etched legends",new Vector3(0,0,-.025f),new Vector2(330,220),Vector3.zero);
                Label(labels.transform,"Channel title",side<0?"EXTERIOR / FLOOD":"INTERIOR / CABIN",23,new Vector2(0,77),new Vector2(310,32));
                Label(labels.transform,"Control key",side<0?"ON / OFF    [ L ]":"BRIGHT / DIM    [ C ]",18,new Vector2(0,-84),new Vector2(310,28),TextAnchor.MiddleCenter);
                var control=new GameObject("Lighting rocker",typeof(BoxCollider),typeof(CockpitControl)).GetComponent<CockpitControl>();
                control.transform.SetParent(module,false);control.transform.localPosition=new Vector3(-side*.07f,-.008f,-.035f);
                control.Ship=displays.Ship;control.Lighting=lighting;control.Action=side<0?CockpitAction.Headlights:CockpitAction.CabinLight;
                control.Lit=white;control.Unlit=dark;
                var collider=control.GetComponent<BoxCollider>();collider.size=new Vector3(.14f,.13f,.12f);collider.isTrigger=true;
                g.Panel("Rocker surround",control.transform,Vector3.zero,new Vector3(.12f,.11f,.025f),trim,default,.018f);
                control.Handle=new GameObject("Rocker mechanism").transform;control.Handle.SetParent(control.transform,false);
                g.Panel("Rocker key",control.Handle,new Vector3(0,0,-.024f),new Vector3(.092f,.078f,.024f),gripRubber,default,.013f);
                for(int i=0;i<3;i++)g.Box("Rocker grip groove",control.Handle,new Vector3(0,-.018f+i*.018f,-.038f),new Vector3(.062f,.004f,.003f),trim);
                control.Lamp=g.Box("Rocker status",control.Handle,new Vector3(0,.027f,-.039f),new Vector3(.043f,.007f,.004f),white).GetComponent<Renderer>();
                interaction.Controls.Add(control);
                for(int j=0;j<6;j++)g.Panel("Module cooling slot",module,new Vector3(side*.105f,-.052f+j*.018f,-.028f),new Vector3(.065f,.006f,.007f),dark,default,.002f);
                for(int y=-1;y<=1;y+=2)g.Sphere("Module fastener",module,new Vector3(side*.158f,y*.097f,-.025f),new Vector3(.009f,.009f,.005f),trim);
            }
            g.Box("Overhead neutral strip",panel,new Vector3(0,-.173f,-.045f),new Vector3(1.32f,.009f,.02f),white);
        }
        void BuildWorkingLights(Transform cockpit,ShipLighting lighting)
        {
            var cabin=new System.Collections.Generic.List<Light>();
            cabin.Add(g.Lamp("Ceiling work light",cockpit,new Vector3(0,2.17f,.4f),new Color(.88f,.93f,1),4.6f,4));
            cabin[0].type=LightType.Spot; cabin[0].spotAngle=150; cabin[0].innerSpotAngle=110;
            cabin[0].transform.localRotation=Quaternion.Euler(90,0,0);
            cabin[0].shadows=LightShadows.Soft; cabin[0].shadowStrength=.55f; cabin[0].shadowNearPlane=.08f;
            cabin.Add(g.Lamp("Instrument face wash",cockpit,new Vector3(0,1.7f,-.35f),new Color(.87f,.93f,1),2.4f,3.2f));
            cabin.Add(g.Lamp("Port console practical",cockpit,new Vector3(-1.04f,1.32f,.25f),new Color(.53f,.8f,1),1.9f,2.4f));
            cabin.Add(g.Lamp("Starboard console practical",cockpit,new Vector3(1.04f,1.32f,.25f),new Color(1,.81f,.62f),1.9f,2.4f));
            var exterior=new System.Collections.Generic.List<Light>();
            var lenses=new System.Collections.Generic.List<Renderer>();
            for(int side=-1;side<=1;side+=2)
            {
                var light=new GameObject("Forward floodlight / "+side,typeof(Light)).GetComponent<Light>();
                light.transform.SetParent(cockpit,false);light.transform.localPosition=new Vector3(side*1.25f,.82f,3.15f);
                light.transform.localRotation=Quaternion.Euler(-3,side*6,0);
                light.type=LightType.Spot;light.color=new Color(.85f,.93f,1);light.intensity=28000;
                light.range=300;light.spotAngle=66;light.innerSpotAngle=38;
                light.shadows=LightShadows.Soft;light.shadowStrength=.75f;light.shadowNearPlane=.3f;
                exterior.Add(light);
                g.Panel("Floodlight housing",cockpit,light.transform.localPosition,new Vector3(.24f,.14f,.12f),hull,default,.04f);
                lenses.Add(g.Panel("Floodlight lens",cockpit,light.transform.localPosition+Vector3.forward*.065f,new Vector3(.19f,.09f,.01f),white,default,.025f).GetComponent<Renderer>());
            }
            lighting.HeadlightLenses=lenses.ToArray();lighting.LensOn=white;lighting.LensOff=dark;
            lighting.Configure(cabin.ToArray(),exterior.ToArray());
        }
        Canvas Monitor(Transform parent,string title,Vector3 position,Vector2 size,Vector3 euler)
        {
            var mount=new GameObject(title+" / instrument module").transform;mount.SetParent(parent,false);mount.localPosition=position;mount.localRotation=Quaternion.Euler(euler);
            Vector3 dimensions=new Vector3(size.x*.001f+.06f,size.y*.001f+.07f,.09f);
            g.Panel(title+" / chassis",mount,new Vector3(0,0,.045f),dimensions,hull,default,.055f);
            g.Panel(title+" / face",mount,new Vector3(0,0,-.004f),new Vector3(dimensions.x-.02f,dimensions.y-.02f,.02f),face,default,.045f);
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)
            {
                var screw=g.Sphere("Captive fastener",mount,new Vector3(x*(dimensions.x*.5f-.025f),y*(dimensions.y*.5f-.025f),-.019f),new Vector3(.012f,.012f,.006f),trim);
                g.Box("Fastener slot",screw.transform,Vector3.zero,new Vector3(.65f,.15f,1.05f),dark);
            }
            var canvas=CockpitDisplays.Screen(mount,title+" / LCD",new Vector3(0,0,-.018f),size,Vector3.zero);
            CockpitDisplays.Rect(canvas.transform,"LCD black",Vector2.zero,size,new Color(.004f,.012f,.018f));
            // Subtle fixed grid. Static marks do not masquerade as telemetry.
            for(float x=-size.x*.5f+30;x<size.x*.5f;x+=50)CockpitDisplays.Rect(canvas.transform,"Grid column",new Vector2(x,0),new Vector2(1.3f,size.y-84),new Color(.05f,.18f,.2f,.09f));
            for(float y=-size.y*.5f+42;y<size.y*.5f-42;y+=50)CockpitDisplays.Rect(canvas.transform,"Grid row",new Vector2(0,y),new Vector2(size.x-30,1.3f),new Color(.05f,.18f,.2f,.09f));
            Label(canvas.transform,"Instrument name",title,22,new Vector2(0,size.y*.5f-22),new Vector2(size.x-34,28));
            CockpitDisplays.Rect(canvas.transform,"Header rule",new Vector2(0,size.y*.5f-41),new Vector2(size.x-34,1.5f),new Color(.12f,.4f,.43f));
            return canvas;
        }
        void Controls(Transform p,CockpitDisplays d,CockpitInteraction interaction)
        {
            for(int side=-1;side<=1;side+=2)
            {
                Vector3 pos=new Vector3(side*.54f,.86f,.43f);
                g.Panel("Raised armrest",p,pos-Vector3.up*.075f,new Vector3(.34f,.13f,.43f),face);
                g.Panel("Control pedestal",p,pos,new Vector3(.32f,.07f,.4f),dark);
                g.Panel("Control mounting plate",p,pos+Vector3.up*.04f,new Vector3(.28f,.015f,.36f),trim);
                g.Torus("Control gimbal",p,pos+Vector3.up*.06f,.09f,.014f,dark,32).transform.localRotation=Quaternion.Euler(90,0,0);
                var pivot=new GameObject(side<0?"Throttle lever":"Flight stick").transform;pivot.SetParent(p,false);pivot.localPosition=pos+Vector3.up*.07f;
                g.Sphere("Flexible bellows",pivot,new Vector3(0,.035f,0),new Vector3(.14f,.10f,.14f),gripRubber);
                for(int b=0;b<5;b++)g.Torus("Gimbal rubber fold",pivot,new Vector3(0,.012f+b*.014f,0),.065f-b*.006f,.006f,gripRubber,48).transform.localRotation=Quaternion.Euler(90,0,0);
                g.Beam("Control shaft",pivot,new Vector3(0,.05f,0),new Vector3(0,.22f,0),.026f,trim);
                var gripShell=g.Sphere("Contoured grip",pivot,new Vector3(0,.23f,0),new Vector3(side<0?.14f:.072f,.095f,.078f),gripRubber);
                g.RefineSphere(gripShell,48,32);
                for(int rib=0;rib<5;rib++)g.Panel("Grip traction rib",pivot,new Vector3(0,.201f+rib*.014f,-.036f),new Vector3(side<0?.104f:.046f,.004f,.006f),dark,default,.002f);
                g.Panel("Grip top cap",pivot,new Vector3(0,.277f,0),new Vector3(side<0?.136f:.071f,.014f,.066f),face,default,.015f);
                g.Sphere("Grip thumb button",pivot,new Vector3(side<0?-.044f:0,.288f,0),new Vector3(.021f,.009f,.021f),side<0?cyan:amber);
                for(int mark=0;mark<9;mark++)g.Box("Travel graduation",p,pos+new Vector3(.12f,.051f,-.13f+mark*.0325f),new Vector3(mark%4==0?.025f:.014f,.0015f,.003f),white);
                var grip=Rest(pivot,new Vector3(0,.23f,0),Vector3.zero);
                if(side<0){d.Throttle=pivot;interaction.Throttle=pivot;interaction.ThrottleGrip=grip;}
                else{d.Stick=pivot;interaction.Stick=pivot;interaction.StickGrip=grip;}
                // Secondary switch bank is reachable from the seated controller position.
                var bank=new GameObject("Physical switch bank").transform;bank.SetParent(p,false);bank.localPosition=new Vector3(side*.94f,.82f,.53f);bank.localRotation=Quaternion.Euler(68,side*8,0);
                g.Panel("Switch bank plate",bank,Vector3.zero,new Vector3(.27f,.39f,.04f),face);
                for(int j=0;j<2;j++)
                {
                    CockpitAction action=side<0?(j==0?CockpitAction.Engine:CockpitAction.Assist):(j==0?CockpitAction.Boost:CockpitAction.Brake);
                    var control=new GameObject(action+" / physical control",typeof(BoxCollider),typeof(CockpitControl)).GetComponent<CockpitControl>();
                    control.transform.SetParent(bank,false);control.transform.localPosition=new Vector3(0,.085f-j*.18f,-.04f);
                    var hit=control.GetComponent<BoxCollider>();hit.size=new Vector3(.15f,.11f,.11f);hit.isTrigger=true;
                    control.Ship=d.Ship;control.Action=action;control.Lit=action==CockpitAction.Boost?amber:cyan;control.Unlit=dark;
                    g.Panel("Recessed switch socket",control.transform,Vector3.zero,new Vector3(.115f,.08f,.022f),dark);
                    control.Handle=new GameObject("Switch mechanism").transform;control.Handle.SetParent(control.transform,false);
                    if(control.IsToggle)
                    {
                        g.Beam("Toggle lever",control.Handle,Vector3.zero,new Vector3(0,0,-.05f),.013f,trim);
                        g.Sphere("Toggle cap",control.Handle,new Vector3(0,0,-.05f),new Vector3(.026f,.026f,.03f),white);
                        for(int s=-1;s<=1;s+=2)g.Box("Switch guard",control.transform,new Vector3(s*.07f,0,-.025f),new Vector3(.012f,.095f,.05f),trim);
                    }
                    else g.Panel("Momentary key",control.Handle,new Vector3(0,0,-.016f),new Vector3(.085f,.052f,.025f),action==CockpitAction.Brake?amber:trim,default,.008f);
                    control.Lamp=g.Box("Status indicator",control.transform,new Vector3(.047f,.035f,-.017f),new Vector3(.016f,.007f,.006f),cyan).GetComponent<Renderer>();
                    var caption=CockpitDisplays.Screen(control.transform,"Switch engraving",new Vector3(0,-.067f,-.025f),new Vector2(220,50),Vector3.zero);caption.transform.localScale=Vector3.one*.00075f;
                    Label(caption.transform,"Switch name",new[]{"ENGINE  [ I ]","ASSIST  [ T ]","BRAKE [ SPACE ]","BOOST [ SHIFT ]"}[(int)action],21,Vector2.zero,new Vector2(220,50),TextAnchor.MiddleCenter);
                    interaction.Controls.Add(control);
                }
            }
        }
        static Transform Rest(Transform parent,Vector3 position,Vector3 euler)
        {var t=new GameObject("Grip pose").transform;t.SetParent(parent,false);t.localPosition=position;t.localRotation=Quaternion.Euler(euler);return t;}
        void SurveyControls(Transform parent,CockpitDisplays displays,CockpitInteraction interaction)
        {
            var bank=new GameObject("Survey equipment console").transform;bank.SetParent(parent,false);
            bank.localPosition=new Vector3(0,.76f,.65f);bank.localRotation=Quaternion.Euler(62,0,0);
            g.Panel("Survey console surround",bank,Vector3.zero,new Vector3(.68f,.19f,.055f),hull);
            var captions=new[]{"SCAN [G]","SERVICE [X]","NEXT [V]","BASE [B]"};
            for(int i=0;i<4;i++)
            {
                var control=new GameObject(captions[i],typeof(BoxCollider),typeof(CockpitControl)).GetComponent<CockpitControl>();
                control.transform.SetParent(bank,false);control.transform.localPosition=new Vector3(-.24f+i*.16f,.015f,-.034f);
                control.Action=(CockpitAction)((int)CockpitAction.Scan+i);control.Ship=displays.Ship;control.Lit=i==1?amber:cyan;control.Unlit=dark;
                var collider=control.GetComponent<BoxCollider>();collider.size=new Vector3(.13f,.13f,.09f);collider.isTrigger=true;
                g.Panel("Equipment key socket",control.transform,Vector3.zero,new Vector3(.123f,.085f,.02f),dark);
                control.Handle=new GameObject("Equipment key mechanism").transform;control.Handle.SetParent(control.transform,false);
                g.Panel("Equipment key",control.Handle,new Vector3(0,0,-.018f),new Vector3(.10f,.067f,.025f),face);
                control.Lamp=g.Box("Equipment indicator",control.Handle,new Vector3(0,.024f,-.032f),new Vector3(.058f,.005f,.004f),cyan).GetComponent<Renderer>();
                var label=CockpitDisplays.Screen(control.transform,"Equipment legend",new Vector3(0,-.057f,-.029f),new Vector2(150,32),Vector3.zero);
                label.transform.localScale=Vector3.one*.00085f;
                Label(label.transform,"Legend",captions[i],20,Vector2.zero,new Vector2(150,32),TextAnchor.MiddleCenter);
                interaction.Controls.Add(control);
            }
        }
        static Font instrumentFont;
        static Text Label(Transform p,string name,string text,int size,Vector2 position,Vector2 rect,TextAnchor anchor=TextAnchor.UpperLeft)
        {
            var label=CockpitDisplays.Text(p,name,text,size,Ink,position,rect,anchor);
            if(!instrumentFont)instrumentFont=Resources.Load<Font>("StarPilot/Fonts/Rajdhani-Medium");
            if(instrumentFont)label.font=instrumentFont;
            return label;
        }
    }
}
