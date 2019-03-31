using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

[CustomEditor(typeof(SoftRenderInspector))]
[ExecuteInEditMode]
public class SoftRenderMain : Editor
{

    private const int width = 700; //图片宽
    private const int height = 700; //图片高

    SoftRenderInspector sri;

    Camera camera;
    Light[] lights;
    MeshFilter[] meshs;
    List<Vertex> vertexList; //存储顶点(单个mesh)
    List<Triangle> triangleList; //存储三角
    SoftRenderTexture frameBuffer;

    Matrix4x4 L2WMat;
    Matrix4x4 MVPMat;
    VAO vao;

    Func<v2f, Color> usedShader;
    float[,] depthBuffer = new float[width, height];
    bool blend;
    SoftRenderTexture dotLightAtten = new SoftRenderTexture("Assets/Scenes/dotlight.jpg");

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Capture"))
        {
            StartCapture();
        }
    }
    public void StartCapture()
    {
        sri = target as SoftRenderInspector;
        var mfs = sri.GetComponentsInChildren<MeshFilter>();
        var lights = sri.GetComponentsInChildren<Light>();
        var camera = sri.GetComponentInChildren<Camera>();
        var srm = new SoftRenderMain(camera, lights, mfs, sri.CaptureSavePath, sri.CaptureSaveName);
        srm.DrawFrame();
       
    }
    public SoftRenderMain(Camera camera, Light[] lights, MeshFilter[] mfs, string captureSavePath, string captureSaveName)
    {
        this.camera = camera;
        this.lights = lights;
        this.meshs = mfs;
        vertexList = new List<Vertex>(1024 * 4);
        triangleList = new List<Triangle>(1024 * 4);
        frameBuffer = new SoftRenderTexture(width, height, captureSavePath, captureSaveName);
    }

    public void DrawFrame()
    {
        this.frameBuffer.Clear(0.2f, 0.3f, 0.4f);
        usedShader = FragShader;
        //逐mesh画
        foreach (var mesh in meshs)
        {
            L2WMat = mesh.transform.localToWorldMatrix;
            MVPMat = camera.projectionMatrix * camera.worldToCameraMatrix * L2WMat;
            vao = new VAO(mesh);
            DrawElement();
            vertexList.Clear();
            triangleList.Clear();
        }
        frameBuffer.Save();

    }
    public void DrawElement()
    {
        //Vertexshader：逐顶点
        Debug.LogError("vertex num :" + vao.vbo.Length);
        for (int i = 0; i < vao.vbo.Length; i++)
        {
            Vertex v = VertShader(vao.vbo[i]);
            vertexList.Add(v);
        }
        //TriangleSetUp
        //由于顶点公用的情况，所有ebo会比vertexList大，每三个组成一个三角形
        Debug.LogError("triangle num :" + vao.ebo.Length / 3);
        for (int i = 0; i < vao.ebo.Length; i+=3)
        {
            Triangle t = new Triangle(
                vertexList[vao.ebo[i]],
                vertexList[vao.ebo[i + 1]],
                vertexList[vao.ebo[i + 2]]
                );
            triangleList.Add(t);
        }
        //逐三角形
        for (int i = 0; i < triangleList.Count; i++)
        {
            //Rasterization(片元)
            var fragList = Rast(triangleList[i]);
            foreach (var frag in fragList)
            {
                //depth test
                if (frag.z > depthBuffer[frag.x, frag.y] && depthBuffer[frag.x, frag.y] != 0) continue;
                Color col = usedShader(frag.data);
                if (blend)
                {
                    Color t = frameBuffer[frag.x, frag.y];
                    float r = t.r * (1 - col.a) + col.r * col.a;
                    float g = t.g * (1 - col.a) + col.g * col.a;
                    float b = t.b * (1 - col.b) + col.b * col.a;
                    frameBuffer[frag.x, frag.y] = new Color(r, g, b);
                }
                else
                {
                    frameBuffer[frag.x, frag.y] = col;

                }
                depthBuffer[frag.x, frag.y] = frag.z;

            }
        }
    }



    public class Vertex
    {
        //x,y,z都是屏幕像素坐标
        public float x;
        public float y;
        public float z;
        public v2f data;
    }
    //Shader
    //a2v:application to vertex 显存当中的vbo
    public struct a2v
    {
        public Vector3 postion;
        public Vector3 normal;
        public Vector2 uv;
    }
    //v2f:vertex to fragment
    public struct v2f
    {
        public Vector3 postion;
        public Vector3 normal;
        public Vector2 uv;
        public float this[int i]
        {
            get
            {
                switch (i)
                {
                    case 0:
                        return postion.x;
                    case 1:
                        return postion.y;
                    case 2:
                        return postion.z;
                    case 3:
                        return normal.x;
                    case 4:
                        return normal.y;
                    case 5:
                        return normal.z;
                    case 6:
                        return uv.x;
                    case 7:
                        return uv.y;
                    default:
                        return 0f;
                }
            }
            set
            {
                switch (i)
                {
                    case 0:
                        postion.x = value;
                        break;
                    case 1:
                        postion.y = value;
                        break;
                    case 2:
                        postion.z = value;
                        break;
                    case 3:
                        normal.x = value;
                        break;
                    case 4:
                        normal.y = value;
                        break;
                    case 5:
                        normal.z = value;
                        break;
                    case 6:
                        uv.x = value;
                        break;
                    case 7:
                        uv.y = value;
                        break;
                    default:
                        break;
                }
            }
        }
    }
    //顶点数组对象
    public class VAO 
    {
        //真实的VBO是无差别数据，这里设计成一个数组（顶点）
        public a2v[] vbo;//顶点缓冲对象，存了所有顶点数据
        public int[] ebo;//索引缓冲对象，存了所有顶点索引
        public VAO(MeshFilter mf)
        {
            Mesh m = mf.sharedMesh;
            vbo = new a2v[m.vertexCount];
            for (int i = 0; i < m.vertexCount; i++)
            {
                a2v a = new a2v();
                a.postion = m.vertices[i];
                a.normal = m.normals[i];
                a.uv = m.uv[i];
                vbo[i] = a;
            }
            ebo = m.triangles;
        }

    }
    //三角化
    public class Triangle
    {
        readonly Vertex[] verts;
        public Triangle(Vertex a,Vertex b,Vertex c)
        {
            verts = new Vertex[3];
            verts[0] = a;
            verts[1] = b;
            verts[2] = c;
        }
        public Vertex this[int index]
        {
            get
            {
                return verts[index];
            }
        }
    }
    //像素
    public class Fragment
    {
        public int x;
        public int y;
        public float z;
        public v2f data;
        public float fx
        {
            get
            {
                return x + 0.5f;
            }
        }
        public float fy
        {
            get
            {
                return y + 0.5f;
            }
        }
    }
    //光栅化
    public List<Fragment> Rast(Triangle t)
    {
        //最小包围盒
        int xMin = (int)Mathf.Min(t[0].x, t[1].x, t[2].x);
        int xMax = (int)Mathf.Max(t[0].x, t[1].x, t[2].x);
        int yMin = (int)Mathf.Min(t[0].y, t[1].y, t[2].y);
        int yMax = (int)Mathf.Max(t[0].y, t[1].y, t[2].y);
        var fragList = new List<Fragment>((xMax - xMin) * (yMax - yMin));
        //逐每个像素块（xMax - xMin / yMax - yMin）
        for (int m = xMin; m < xMax + 1; m++)
        {
            for (int n = yMin; n < yMax + 1; n++)
            {
                if (m < 0 || n < 0 || m > width - 1 || n > height - 1) continue;
                //判断像素是否在像素内
                if (isLeftPoint(t[0], t[1], m + 0.5f, n + 0.5f)) continue;
                if (isLeftPoint(t[1], t[2], m + 0.5f, n + 0.5f)) continue;
                if (isLeftPoint(t[2], t[0], m + 0.5f, n + 0.5f)) continue;
                var frag = new Fragment();
                frag.x = m;
                frag.y = n;
                LerpFragment(t[0], t[1], t[2], ref frag);
                fragList.Add(frag);
            }
        }
        return fragList;
    }
    
    public bool isLeftPoint(Vertex a, Vertex b, float x, float y)
    {
        float s = (a.x - x) * (b.y - y) - (a.y - y) * (b.x - x);
        return s > 0 ? true : false;
    }
    //插值算出该像素颜色
    public void LerpFragment(Vertex a, Vertex b, Vertex c, ref Fragment frag)
    {
        for (int i = 0; i < 8; i++)
        {
            frag.data[i] = LerpValue(a.data[i], a.x, a.y, b.data[i], b.x, b.y, c.data[i], c.x, c.y, frag.x, frag.y);
        }
        frag.z = LerpValue(a.z, a.x, a.y, b.z, b.x, b.y, c.z, c.x, c.y, frag.x, frag.y);
    }

    float LerpValue(float f1, float x1, float y1, float f2, float x2, float y2, float f3, float x3, float y3
        , float fragx, float fragy)
    {
        float left = (f1 * x2 - f2 * x1) / (y1 * x2 - y2 * x1) - (f1 * x3 - f3 * x1) / (y1 * x3 - y3 * x1);
        float right = (x2 - x1) / (y1 * x2 - y2 * x1) - (x3 - x1) / (y1 * x3 - y3 * x1);
        float c = left / right;
        left = (f1 * x2 - f2 * x1) / (x2 - x1) - (f1 * x3 - f3 * x1) / (x3 - x1);
        right = (y1 * x2 - y2 * x1) / (x2 - x1) - (y1 * x3 - y3 * x1) / (x3 - x1);
        float b = left / right;
        float a = (f1 - f3 - b * (y1 - y3)) / (x1 - x3);
        return fragx * a + fragy * b + c;
    }

    //顶点着色器
    Vertex VertShader(a2v a)
    {
        v2f v = new v2f();
        v.postion = L2WMat.MultiplyPoint3x4(a.postion);
        v.normal = L2WMat.MultiplyVector(a.normal);//todo:这里的向量转换貌似有问题
        v.uv = a.uv;
        Vertex vert = new Vertex();
        Vector4 svp = a.postion; //SV_POSITION
        svp.w = 1f;//将w设为1表示这是点，而不是向量。w向量便于后面的mvp变换，具体改变w是在投影变换
        svp = MVPMat * svp;//todo:这里可能运算有问题
        vert.data = v;
        vert.x = (svp.x / svp.w / 2 + 0.5f) * width;//透视除法NDC + 屏幕空间转换 
        vert.y = (svp.y / svp.w / 2 + 0.5f) * height;
        vert.z = (svp.z / svp.w / 2 + 0.5f);
        return vert;//最后返回的是屏幕上的坐标 + 顶点数据（世界坐标，世界法线，UV坐标）
    }

    //像素着色器
    Color FragShader(v2f v)
    {
        float r = 0f, g = 0f, b = 0f;
        float dis = 0f;
        float atten = 0f;
        Vector3 lightDir;
        foreach (var light in lights)
        {
            switch (light.type)
            {
                case LightType.Directional:
                    lightDir = -light.transform.forward;
                    atten = Vector3.Dot(Vector3.Normalize(lightDir), Vector3.Normalize(v.normal));
                    atten = Mathf.Max(0, atten);
                    r += light.color.r * atten;
                    g += light.color.g * atten;
                    b += light.color.b * atten;
                    break;
                case LightType.Point:
                    dis = Vector3.Distance(light.transform.position, v.postion);
                    if (dis > light.range) continue;
                    atten = dotLightAtten[(int)(dis / light.range), 2].r;
                    lightDir = light.transform.position - v.postion;
                    atten *= Vector3.Dot(Vector3.Normalize(lightDir), Vector3.Normalize(v.normal));
                    atten *= light.intensity;
                    r += light.color.r * atten;
                    g += light.color.g * atten;
                    b += light.color.b * atten;
                    break;
            }
        }
        return new Color(r, g, b);
    }



}
