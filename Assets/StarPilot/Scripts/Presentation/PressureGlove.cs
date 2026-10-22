using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarPilot
{
    /// <summary>
    /// A continuous tailored glove, extracted once from a smooth anatomical volume.
    /// The palm, finger webs and thumb share vertices; a 16-bone rig bends the fabric.
    /// All dimensions are metres. No sphere primitives or rigid finger caps are used.
    /// </summary>
    public sealed class PressureGlove
    {
        public readonly Transform Root;
        public readonly Transform[] Fingers=new Transform[5];
        public readonly Transform[] Bones=new Transform[16];
        readonly Vector3[] bases=new Vector3[5], directions=new Vector3[5];
        readonly float[,] lengths={{.034f,.022f,.019f},{.04f,.026f,.022f},{.037f,.024f,.021f},{.029f,.018f,.017f},{.026f,.023f,.019f}};
        readonly float[] radii={.0118f,.0125f,.0117f,.0101f,.0142f};
        readonly int side;
        readonly List<Vector3> vertices=new List<Vector3>(), normals=new List<Vector3>();
        readonly List<int> triangles=new List<int>();
        readonly List<Color> colors=new List<Color>();
        readonly List<BoneWeight> weights=new List<BoneWeight>();
        readonly Dictionary<ulong,int> edgeVertices=new Dictionary<ulong,int>();
        readonly Vector3 minimum=new Vector3(-.103f,-.046f,-.142f);
        const float Step=.003f;
        const int X=70,Y=32,Z=100;
        readonly float[] field=new float[X*Y*Z];
        static readonly int[,] Tetra={{0,5,1,6},{0,1,2,6},{0,2,3,6},{0,3,7,6},{0,7,4,6},{0,4,5,6}};
        static readonly int[,] Edges={{0,1},{0,2},{0,3},{1,2},{1,3},{2,3}};
        readonly int[] crossings=new int[4];

        public PressureGlove(Transform parent,int handedness,Material material,List<Mesh> owned)
        {
            side=handedness;
            Root=new GameObject(side<0?"Left / tailored pressure glove":"Right / tailored pressure glove").transform;
            Root.SetParent(parent,false);Bones[0]=Root;
            float[] xs={-.032f,-.011f,.012f,.032f}, zs={.030f,.036f,.029f,.016f};
            for(int f=0;f<5;f++)
            {
                bases[f]=f<4?new Vector3(xs[f]*side,0,zs[f]):new Vector3(-side*.037f,-.007f,-.030f);
                directions[f]=f<4?new Vector3((f-1.5f)*side*.035f,0,1).normalized:new Vector3(-side*.64f,-.1f,.76f).normalized;
                Transform parentBone=Root;
                for(int j=0;j<3;j++)
                {
                    int index=1+f*3+j;
                    var bone=new GameObject($"Digit {f+1} / joint {j+1}").transform;
                    bone.SetParent(parentBone,false);
                    bone.localPosition=j==0?bases[f]:new Vector3(0,0,lengths[f,j-1]);
                    bone.localRotation=j==0?Quaternion.LookRotation(directions[f],Vector3.up):Quaternion.identity;
                    Bones[index]=bone;parentBone=bone;
                    if(j==0)Fingers[f]=bone;
                }
            }
            BuildSurface();
            AddSeams();
            var mesh=new Mesh { name="Continuous pressure glove / articulated cloth",indexFormat=IndexFormat.UInt32 };
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);
            mesh.SetColors(colors);mesh.boneWeights=weights.ToArray();
            var bindposes=new Matrix4x4[Bones.Length];
            for(int b=0;b<Bones.Length;b++)bindposes[b]=Bones[b].worldToLocalMatrix*Root.localToWorldMatrix;
            mesh.bindposes=bindposes;mesh.RecalculateBounds();owned.Add(mesh);
            var renderer=Root.gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh=mesh;renderer.sharedMaterial=material;renderer.bones=Bones;renderer.rootBone=Root;
            renderer.quality=SkinQuality.Bone4;
            // Bounds include the closed fist and thumb, without disabling normal visibility culling.
            renderer.localBounds=new Bounds(new Vector3(0,-.025f,0),new Vector3(.28f,.28f,.38f));
        }

        public void Pose(float grip)
        {
            grip=Mathf.Clamp01(grip);
            for(int f=0;f<5;f++)
            {
                float curl=grip*(f==3?1.08f:1);
                var basis=Quaternion.LookRotation(directions[f],Vector3.up);
                if(f==4)
                {
                    Fingers[f].localRotation=basis*Quaternion.Euler(Mathf.Lerp(8,32,grip),side*Mathf.Lerp(0,25,grip),0);
                    Bones[14].localRotation=Quaternion.Euler(Mathf.Lerp(8,48,grip),0,0);
                    Bones[15].localRotation=Quaternion.Euler(Mathf.Lerp(5,30,grip),0,0);
                }
                else
                {
                    Fingers[f].localRotation=basis*Quaternion.Euler(Mathf.Lerp(4,52,curl),0,0);
                    Bones[2+f*3].localRotation=Quaternion.Euler(Mathf.Lerp(7,82,curl),0,0);
                    Bones[3+f*3].localRotation=Quaternion.Euler(Mathf.Lerp(4,45,curl),0,0);
                }
            }
        }

        static float Union(float a,float b,float width)
        {
            float h=Mathf.Clamp01(.5f+.5f*(b-a)/width);
            return Mathf.Lerp(b,a,h)-width*h*(1-h);
        }
        static float Ellipsoid(Vector3 p,Vector3 radii)
        {
            var q=new Vector3(p.x/radii.x,p.y/radii.y,p.z/radii.z);
            if(q.sqrMagnitude<1e-10f)return -Mathf.Min(radii.x,Mathf.Min(radii.y,radii.z));
            var q2=new Vector3(q.x/radii.x,q.y/radii.y,q.z/radii.z);
            return q.magnitude*(q.magnitude-1)/Mathf.Max(q2.magnitude,.001f);
        }
        float Distance(Vector3 p)
        {
            // A flattened, tapered palm with a narrower wrist and a soft thenar pad.
            float palm=Ellipsoid(p-new Vector3(0,-.003f,-.012f),new Vector3(.044f,.021f,.055f));
            float wrist=Ellipsoid(p-new Vector3(0,0,-.073f),new Vector3(.032f,.019f,.036f));
            float result=Union(palm,wrist,.018f);
            result=Union(result,Ellipsoid(p-new Vector3(-side*.027f,-.009f,-.032f),new Vector3(.024f,.018f,.032f)),.011f);
            for(int f=0;f<5;f++)
            {
                Vector3 d=p-bases[f];float along=Vector3.Dot(d,directions[f]);
                float total=lengths[f,0]+lengths[f,1]+lengths[f,2];
                Vector3 nearest=d-directions[f]*Mathf.Clamp(along,0,total-.005f);
                nearest.y*=1.12f;
                float t=Mathf.Clamp01(along/total);
                float radius=radii[f]*Mathf.Lerp(1,.72f,t);
                // Small compression folds on the dorsal side, not black joint rings.
                float jointDistance=Mathf.Min(Mathf.Abs(along-lengths[f,0]),Mathf.Abs(along-lengths[f,0]-lengths[f,1]));
                float fold=.00045f*Mathf.Cos(jointDistance*1100)*Mathf.Exp(-jointDistance*jointDistance/.000035f)*Mathf.Clamp01(d.y*80);
                float digit=nearest.magnitude-radius-fold;
                result=Union(result,digit,f==4?.012f:.005f);
            }
            // Irregular cloth creases across the back and gathered wrist.
            float back=Mathf.Clamp01(p.y*55);
            float wristFold=Mathf.Exp(-Mathf.Pow((p.z+.081f)/.012f,2));
            float creaseA=Mathf.Exp(-Mathf.Pow((p.z-.009f+p.x*.12f)/.0025f,2));
            float creaseB=Mathf.Exp(-Mathf.Pow((p.z-.022f-p.x*.15f)/.002f,2));
            result-=back*(.0007f*wristFold*Mathf.Sin(p.z*640+p.x*80)-.0006f*(creaseA+creaseB));
            return result;
        }
        Vector3 Point(int index)
        {
            int x=index%X,y=index/X%Y,z=index/(X*Y);
            return minimum+new Vector3(x,y,z)*Step;
        }
        Vector3 Normal(Vector3 p)
        {
            const float e=.00025f;
            return new Vector3(Distance(p+Vector3.right*e)-Distance(p-Vector3.right*e),
                Distance(p+Vector3.up*e)-Distance(p-Vector3.up*e),Distance(p+Vector3.forward*e)-Distance(p-Vector3.forward*e)).normalized;
        }
        int Vertex(int a,int b)
        {
            ulong key=((ulong)(uint)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);
            if(edgeVertices.TryGetValue(key,out int existing))return existing;
            Vector3 p=Vector3.Lerp(Point(a),Point(b),field[a]/(field[a]-field[b]));
            Vector3 n=Normal(p);
            int index=vertices.Count;vertices.Add(p);normals.Add(n);weights.Add(Weight(p));
            float palm=Mathf.SmoothStep(0,1,Mathf.Clamp01((-n.y-.05f)*2))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.105f,-.065f,p.z));
            float cuff=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.106f,-.096f,p.z));
            float seam=Mathf.Exp(-Mathf.Pow((Mathf.Abs(p.x)-.031f)/.0015f,2))*Mathf.Clamp01(n.y)*Mathf.Clamp01((.035f-p.z)*40);
            float shade=Mathf.Lerp(1,.23f,Mathf.Max(palm*.85f,cuff*.7f));
            float pad=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.031f,.025f,Mathf.Abs(p.x)))*
                Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.065f,-.055f,p.z))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.022f,.012f,p.z))*Mathf.Clamp01(n.y*3);
            shade*=Mathf.Lerp(1,.62f,pad);
            shade*=1-seam*.24f;
            colors.Add(new Color(shade,shade,shade,.92f));edgeVertices.Add(key,index);return index;
        }
        BoneWeight Weight(Vector3 p)
        {
            int finger=-1;float closest=100,axis=0;
            for(int f=0;f<5;f++)
            {
                Vector3 d=p-bases[f];float t=Vector3.Dot(d,directions[f]);
                float total=lengths[f,0]+lengths[f,1]+lengths[f,2];
                float distance=(d-directions[f]*Mathf.Clamp(t,0,total)).magnitude;
                if(t>-.02f && distance<closest){closest=distance;finger=f;axis=t;}
            }
            if(finger<0)return new BoneWeight {boneIndex0=0,weight0=1};
            int bone=1+finger*3;
            float rootWeight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.016f,.014f,axis));
            if(rootWeight<.999f)return new BoneWeight{boneIndex0=0,weight0=1-rootWeight,boneIndex1=bone,weight1=rootWeight};
            float first=lengths[finger,0],second=first+lengths[finger,1];
            if(axis<first+.008f)
            {
                float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(first-.009f,first+.008f,axis));
                return new BoneWeight{boneIndex0=bone,weight0=1-blend,boneIndex1=bone+1,weight1=blend};
            }
            float last=Mathf.SmoothStep(0,1,Mathf.InverseLerp(second-.007f,second+.007f,axis));
            return new BoneWeight{boneIndex0=bone+1,weight0=1-last,boneIndex1=bone+2,weight1=last};
        }
        void Triangle(int a,int b,int c)
        {
            Vector3 cross=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);
            if(cross.sqrMagnitude<1e-16f)return;
            if(Vector3.Dot(cross,normals[a]+normals[b]+normals[c])<0){int swap=b;b=c;c=swap;}
            triangles.Add(a);triangles.Add(b);triangles.Add(c);
        }
        void BuildSurface()
        {
            for(int i=0;i<field.Length;i++)field[i]=Distance(Point(i));
            var cube=new int[8];var tet=new int[4];
            for(int z=0;z<Z-1;z++)for(int y=0;y<Y-1;y++)for(int x=0;x<X-1;x++)
            {
                int a=x+X*(y+Y*z);
                cube[0]=a;cube[1]=a+1;cube[2]=a+X+1;cube[3]=a+X;
                cube[4]=a+X*Y;cube[5]=a+X*Y+1;cube[6]=a+X*Y+X+1;cube[7]=a+X*Y+X;
                int inside=0;for(int c=0;c<8;c++)if(field[cube[c]]<0)inside++;
                if(inside==0 || inside==8)continue;
                for(int t=0;t<6;t++)
                {
                    for(int c=0;c<4;c++)tet[c]=cube[Tetra[t,c]];
                    int count=0;
                    for(int e=0;e<6;e++)
                    {
                        int from=tet[Edges[e,0]],to=tet[Edges[e,1]];
                        if((field[from]<0)==(field[to]<0))continue;
                        crossings[count++]=Vertex(from,to);
                    }
                    if(count==3)Triangle(crossings[0],crossings[1],crossings[2]);
                    else if(count==4)
                    {
                        // Order the four edge intersections around their face centre.
                        Vector3 center=Vector3.zero,normal=Vector3.zero;
                        for(int i=0;i<4;i++){center+=vertices[crossings[i]]*.25f;normal+=normals[crossings[i]];}
                        Vector3 u=(vertices[crossings[0]]-center).normalized,v=Vector3.Cross(normal.normalized,u);
                        int[] ordered={crossings[0],crossings[1],crossings[2],crossings[3]};
                        System.Array.Sort(ordered,(i,j)=>Mathf.Atan2(Vector3.Dot(vertices[i]-center,v),Vector3.Dot(vertices[i]-center,u)).CompareTo(Mathf.Atan2(Vector3.Dot(vertices[j]-center,v),Vector3.Dot(vertices[j]-center,u))));
                        Triangle(ordered[0],ordered[1],ordered[2]);Triangle(ordered[0],ordered[2],ordered[3]);
                    }
                }
            }
            edgeVertices.Clear();
        }
        Vector3 BackSurface(float x,float z)
        {
            float lo=0,hi=.043f;
            for(int i=0;i<14;i++){float mid=(lo+hi)*.5f;if(Distance(new Vector3(x,mid,z))<0)lo=mid;else hi=mid;}
            return new Vector3(x,hi+.0004f,z);
        }
        void Stitch(Vector3 a,Vector3 b)
        {
            Vector3 direction=(b-a).normalized;
            Vector3 u=Vector3.Cross(direction,Vector3.up).normalized*.00036f;
            Vector3 v=Vector3.Cross(direction,u).normalized*.00036f;
            int start=vertices.Count;
            for(int end=0;end<2;end++)for(int j=0;j<6;j++)
            {
                float angle=j*Mathf.PI/3;Vector3 offset=u*Mathf.Cos(angle)+v*Mathf.Sin(angle);
                Vector3 p=(end==0?a:b)+offset;
                vertices.Add(p);normals.Add(offset.normalized);weights.Add(Weight(p));colors.Add(new Color(1.25f,1.21f,1.1f,1));
            }
            for(int j=0;j<6;j++){int n=(j+1)%6;Triangle(start+j,start+n,start+6+j);Triangle(start+n,start+6+n,start+6+j);}
        }
        void AddSeams()
        {
            for(int s=-1;s<=1;s+=2)for(int i=0;i<28;i++)
            {
                float z=-.095f+i*.0045f,x=s*(.024f+.008f*Mathf.Sin((z+.095f)*18));
                Stitch(BackSurface(x,z),BackSurface(x,z+.0025f));
            }
            for(int i=0;i<16;i++)
            {
                float x=-.027f+i*.0035f;
                Stitch(BackSurface(x,-.097f),BackSurface(x+.002f,-.097f));
            }
        }
    }
}
