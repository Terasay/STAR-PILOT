using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarPilot
{
    /// <summary>Small reusable construction helpers for original prototype geometry.</summary>
    public sealed class Geometry
    {
        readonly List<Object> owned;
        public Geometry(List<Object> ownedObjects) { owned = ownedObjects; }
        public Material ShaderMaterial(string name,string resource)
        {var material=new Material(Resources.Load<Shader>(resource)){name=name};owned.Add(material);return material;}

        public Material Material(string name, Color color, float metallic = 0.6f, float smoothness = 0.5f, float glow = 0f)
        {
            Shader shader = Resources.Load<Shader>("StarPilot/Surface");
            var m = new Material(shader) { name = name, enableInstancing = true };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            m.SetColor("_EmissionColor", color * glow);
            owned.Add(m);
            return m;
        }

        public GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material,
            Vector3 euler = default, bool collider = false)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            obj.transform.localRotation = Quaternion.Euler(euler);
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.Destroy(obj.GetComponent<Collider>());
            return obj;
        }

        public GameObject Sphere(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collider = false)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.Destroy(obj.GetComponent<Collider>());
            return obj;
        }

        public GameObject Beam(string name, Transform parent, Vector3 a, Vector3 b, float width, Material material)
        {
            var beam = Box(name, parent, (a + b) * 0.5f, new Vector3(width, width, Vector3.Distance(a,b)), material);
            beam.transform.localRotation = Quaternion.LookRotation(b-a);
            return beam;
        }

        public GameObject BeveledBeam(string name,Transform parent,Vector3 a,Vector3 b,float width,Material material)
        {
            var beam=Panel(name,parent,(a+b)*.5f,new Vector3(width,width,Vector3.Distance(a,b)),material,default,width*.2f);
            beam.transform.localRotation=Quaternion.LookRotation(b-a);return beam;
        }

        public void DetailMaterial(Material material,string texture,float scale,float strength)
        {
            material.SetFloat("_Rock",2);material.SetFloat("_TextureScale",scale);material.SetFloat("_NormalStrength",strength);
            material.SetTexture("_RockColor",Resources.Load<Texture2D>("StarPilot/Textures/"+texture+"_diffuse"));
            material.SetTexture("_RockNormal",Resources.Load<Texture2D>("StarPilot/Textures/"+texture+"_nor_gl"));
            material.SetTexture("_RockRoughness",Resources.Load<Texture2D>("StarPilot/Textures/"+texture+"_rough"));
        }

        public void Windshield(Transform parent,Material material)
        {
            var mesh=new Mesh{name="Trapezoid canopy glazing",vertices=new[]{new Vector3(-1.42f,.98f,1.66f),new Vector3(1.42f,.98f,1.66f),new Vector3(.87f,2.5f,1.35f),new Vector3(-.87f,2.5f,1.35f)},
                triangles=new[]{0,2,1,0,3,2},uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up}};
            mesh.RecalculateNormals();mesh.RecalculateBounds();owned.Add(mesh);
            var glass=new GameObject("Forward canopy / pressure glazing",typeof(MeshFilter),typeof(MeshRenderer));glass.transform.SetParent(parent,false);
            glass.GetComponent<MeshFilter>().sharedMesh=mesh;glass.GetComponent<MeshRenderer>().sharedMaterial=material;
            glass.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }

        // Eight-sided face with a bevel around the front and rear edges. Dimensions are in metres.
        public GameObject Panel(string name, Transform parent, Vector3 position, Vector3 size, Material material,
            Vector3 euler = default, float corner = .04f)
        {
            float x=size.x*.5f,y=size.y*.5f,z=size.z*.5f;
            float c=Mathf.Min(corner,Mathf.Min(x,y)*.45f), bevel=Mathf.Min(.012f,z*.35f);
            Vector2[] outline={new Vector2(-x+c,-y),new Vector2(x-c,-y),new Vector2(x,-y+c),new Vector2(x,y-c),
                new Vector2(x-c,y),new Vector2(-x+c,y),new Vector2(-x,y-c),new Vector2(-x,-y+c)};
            var verts=new List<Vector3>();var tris=new List<int>();
            System.Action<Vector3,Vector3,Vector3,Vector3> quad=(a,b,d,e)=>
            {int n=verts.Count;verts.Add(a);verts.Add(b);verts.Add(d);verts.Add(e);tris.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});};
            for(int i=0;i<8;i++)
            {
                Vector2 a=outline[i],b=outline[(i+1)%8];
                Vector2 ai=a-Vector2.ClampMagnitude(a,bevel),bi=b-Vector2.ClampMagnitude(b,bevel);
                quad(new Vector3(ai.x,ai.y,-z),new Vector3(bi.x,bi.y,-z),new Vector3(b.x,b.y,-z+bevel),new Vector3(a.x,a.y,-z+bevel));
                quad(new Vector3(a.x,a.y,-z+bevel),new Vector3(b.x,b.y,-z+bevel),new Vector3(b.x,b.y,z-bevel),new Vector3(a.x,a.y,z-bevel));
                quad(new Vector3(a.x,a.y,z-bevel),new Vector3(b.x,b.y,z-bevel),new Vector3(bi.x,bi.y,z),new Vector3(ai.x,ai.y,z));
                int n=verts.Count;verts.Add(new Vector3(0,0,-z));verts.Add(new Vector3(bi.x,bi.y,-z));verts.Add(new Vector3(ai.x,ai.y,-z));
                tris.AddRange(new[]{n,n+1,n+2});
                n=verts.Count;verts.Add(new Vector3(0,0,z));verts.Add(new Vector3(ai.x,ai.y,z));verts.Add(new Vector3(bi.x,bi.y,z));tris.AddRange(new[]{n,n+1,n+2});
            }
            var mesh=new Mesh{name=name+" / bevel",vertices=verts.ToArray(),triangles=tris.ToArray()};
            mesh.RecalculateNormals();mesh.RecalculateBounds();owned.Add(mesh);
            var obj=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(parent,false);
            obj.transform.localPosition=position;obj.transform.localRotation=Quaternion.Euler(euler);
            obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<Renderer>().sharedMaterial=material;return obj;
        }

        public GameObject PressureSleeve(Transform parent,Material material)
        {
            const int rings=48,sides=40;
            var vertices=new Vector3[(rings+1)*(sides+1)];var triangles=new List<int>();
            for(int row=0;row<=rings;row++)for(int col=0;col<=sides;col++)
            {
                float t=row/(float)rings,a=col*2*Mathf.PI/sides;
                float envelope=Mathf.Sin(t*Mathf.PI);
                float fold=.004f*Mathf.Sin(t*72+Mathf.Sin(a*3)*1.7f)*envelope*envelope;
                float radius=Mathf.Lerp(.082f,.055f,t)+.009f*envelope+fold;
                int index=row*(sides+1)+col;
                vertices[index]=new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius*.91f,t-.5f);
                if(row==rings || col==sides)continue;
                int next=index+sides+1;
                triangles.AddRange(new[]{index,index+1,next,index+1,next+1,next});
            }
            var mesh=new Mesh{name="Tailored sleeve / gathered fabric",vertices=vertices,triangles=triangles.ToArray()};
            mesh.RecalculateNormals();var normals=mesh.normals;
            for(int row=0;row<=rings;row++){int first=row*(sides+1),last=first+sides;normals[first]=normals[last]=(normals[first]+normals[last]).normalized;}
            mesh.normals=normals;mesh.RecalculateBounds();owned.Add(mesh);
            var obj=new GameObject("Tailored pressure sleeve",typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(parent,false);
            obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=material;return obj;
        }
        public void RefineSphere(GameObject sphere, int longitude = 128, int latitude = 64)
        {
            var vertices = new Vector3[(longitude + 1) * (latitude + 1)];
            var triangles = new List<int>(longitude * latitude * 6);
            for (int y = 0; y <= latitude; y++)
            for (int x = 0; x <= longitude; x++)
            {
                float pitch = y * Mathf.PI / latitude, yaw = x * Mathf.PI * 2 / longitude;
                vertices[y*(longitude+1)+x] = new Vector3(Mathf.Sin(pitch)*Mathf.Cos(yaw), Mathf.Cos(pitch), Mathf.Sin(pitch)*Mathf.Sin(yaw))*.5f;
                if (y == latitude || x == longitude) continue;
                int a = y*(longitude+1)+x, b = a+longitude+1;
                triangles.Add(a);triangles.Add(a+1);triangles.Add(b);
                triangles.Add(a+1);triangles.Add(b+1);triangles.Add(b);
            }
            var mesh = new Mesh { name = "High resolution planet", vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals();mesh.RecalculateBounds();owned.Add(mesh);
            sphere.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        public GameObject Torus(string name, Transform parent, Vector3 position, float radius, float thickness, Material material, int segments = 64)
        {
            const int sides = 8;
            var vertices = new Vector3[(segments + 1) * (sides + 1)];
            var triangles = new int[segments * sides * 6];
            for (int i = 0; i <= segments; i++)
            for (int j = 0; j <= sides; j++)
            {
                float u = i * Mathf.PI * 2f / segments, v = j * Mathf.PI * 2f / sides;
                vertices[i*(sides+1)+j] = new Vector3((radius + Mathf.Cos(v)*thickness)*Mathf.Cos(u),
                    (radius + Mathf.Cos(v)*thickness)*Mathf.Sin(u), Mathf.Sin(v)*thickness);
            }
            int t = 0;
            for (int i = 0; i < segments; i++)
            for (int j = 0; j < sides; j++)
            {
                int a = i * (sides+1) + j, b = a + sides+1;
                triangles[t++] = a; triangles[t++] = b; triangles[t++] = a+1;
                triangles[t++] = a+1; triangles[t++] = b; triangles[t++] = b+1;
            }
            var mesh = new Mesh { name = name + " mesh", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); owned.Add(mesh);
            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(parent, false); obj.transform.localPosition = position;
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            obj.GetComponent<MeshRenderer>().sharedMaterial = material;
            return obj;
        }

        public Light Lamp(string name, Transform parent, Vector3 position, Color color, float intensity, float range)
        {
            var light = new GameObject(name, typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(parent, false); light.transform.localPosition = position;
            light.type = LightType.Point; light.color = color; light.intensity = intensity;
            light.range = range; light.shadows = LightShadows.None;
            return light;
        }
    }
}
