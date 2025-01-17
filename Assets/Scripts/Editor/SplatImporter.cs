using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using System;
using System.IO;
using System.Runtime.InteropServices;

[ScriptedImporter(1, "splat"), BurstCompile]
public sealed class SplatImporter : ScriptedImporter
{
    #region ScriptedImporter implementation

    public override void OnImportAsset(AssetImportContext context)
    {
        var gameObject = new GameObject();
        var data = ImportAsSplatData(context.assetPath);

        var setter = gameObject.AddComponent<SplatDataSetter>();
        setter.SplatData = data;

        context.AddObjectToAsset("prefab", gameObject);
        if (data != null) context.AddObjectToAsset("data", data);

        context.SetMainObject(gameObject);
    }

    #endregion

    #region Reader implementation

    SplatData ImportAsSplatData(string path)
    {
        var data = ScriptableObject.CreateInstance<SplatData>();
        data.name = Path.GetFileNameWithoutExtension(path);

        var arrays = LoadDataArrays(path);
        data.PositionArray = arrays.position;
        data.AxisArray = arrays.axis;
        data.ColorArray = arrays.color;
        data.ReleaseGpuResources();

        return data;
    }

#pragma warning disable CS0649

    struct ReadData
    {
        public float px, py, pz;
        public float sx, sy, sz;
        public byte r, g, b, a;
        public byte rw, rx, ry, rz;
    }

#pragma warning restore CS0649

    (Vector3[] position, Vector3[] axis, Color[] color)
        LoadDataArrays(string path)
    {
        var bytes = (Span<byte>)File.ReadAllBytes(path);
        var count = bytes.Length / 32;
        Debug.Log(count);

        var source = MemoryMarshal.Cast<byte, ReadData>(bytes);

        var position = new Vector3[count];
        var axis = new Vector3[count * 3];
        var color = new Color[count];

        for (var i = 0; i < count; i++)
            ParseReadData(source[i],
                          out position[i],
                          out axis[i * 3],
                          out axis[i * 3 + 1],
                          out axis[i * 3 + 2],
                          out color[i]);

        return (position, axis, color);
    }

    [BurstCompile]
    void ParseReadData(in ReadData src,
                       out Vector3 position,
                       out Vector3 axis1,
                       out Vector3 axis2,
                       out Vector3 axis3,
                       out Color color)
    {
        var rv = (math.float4(src.rx, src.ry, src.rz, src.rw) - 128) / 128;
        var q = math.quaternion(-rv.x, -rv.y, rv.z, rv.w);
        position = math.float3(src.px, src.py, -src.pz);
        axis1 = math.mul(q, math.float3(src.sx, 0, 0));
        axis2 = math.mul(q, math.float3(0, src.sy, 0));
        axis3 = math.mul(q, math.float3(0, 0, src.sz));
        color = (Vector4)math.float4(src.r, src.g, src.b, src.a) / 255;
    }

    #endregion
}
