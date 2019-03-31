using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEditor;

public class SoftRenderTexture
{

    private string path;
    private Texture2D t;
    private Color[] cols;
    public readonly int width;
    public readonly int height;
    private bool isSaveable;

    public SoftRenderTexture(int width, int height, string savePath, string picName)
    {
        cols = new Color[width * height];
        this.width = width;
        this.height = height;
        path = Path.Combine(savePath, picName) + ".png";
        isSaveable = true;
    }

    public SoftRenderTexture(string path)
    {
        t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        cols = t.GetPixels();
        this.width = t.width;
        this.height = t.height;
        isSaveable = false;
    }

    /// <summary>
    /// 获取颜色值
    /// </summary>
    /// <returns></returns>
    public Color this[int x, int y]
    {
        get
        {
            //todo：t.GetPixels()是一个一维数组，优先排列width
            if (x > width - 1) x = width - 1;
            if (y > height - 1) y = height - 1;
            return cols[y * width + x];
        }
        set
        {
            cols[y * width + x] = value;
        }
    }
    /// <summary>
    /// 获取当前UV颜色值
    /// </summary>
    /// <param UV.x="x"></param>
    /// <param UV.y="y"></param>
    /// <returns></returns>
    public Color this[float x, float y]
    {
        get
        {
            //todo:(int)强转会去掉小数部分，那么此处加一个0.49f，也就是尽量去平衡颜色过度
            int m = (int)(x * width + 0.49f);
            int n = (int)(y * height + 0.49f);
            return this[m, n];
        }
    }

    public void Clear(float r = 0, float g = 0, float b = 0)
    {
        for (int i = 0; i < cols.Length; i++)
        {
            cols[i] = new Color(r, g, b);
        }
    }

    public void Save()
    {
        if (!isSaveable) return;
        Texture2D t = new Texture2D(width, height, TextureFormat.RGBA32, false);
        t.SetPixels(cols);
        byte[] bytes = t.EncodeToPNG();
        FileStream file = File.Open(path, FileMode.Create);
        BinaryWriter writer = new BinaryWriter(file);
        writer.Write(bytes);
        file.Close();
        Texture2D.DestroyImmediate(t);
        t = null;
    }
}
