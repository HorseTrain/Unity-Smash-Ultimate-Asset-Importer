using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using RayAssets;

public class UnityWrangler : MonoBehaviour
{
    public static void LoadModel(RayFile file)
    {
        LoadModelObject(file,out var a,out var b, out var c);
    }

    public static GameObject LoadModelObject(RayFile file,out Material[] Mats,out Texture2D[] Textures,out UnityEngine.Mesh[] Meshes)
    {
        GameObject Root = new GameObject("Root");

        GameObject MeshD = new GameObject("MeshData");
        MeshD.transform.parent = Root.transform;

        Matrix4x4[] INV;

        ParseModel(RayFile.ParseFile(file.Buffers[1]), MeshD, ParseSkeleton(RayFile.ParseFile(file.Buffers[0]), Root, out INV), INV, ParseMaterial(RayFile.ParseFile(file.Buffers[2])), LoadTextures(RayFile.ParseFile(file.Buffers[3]),out Textures),out Mats,out Meshes);

        return Root;
    }

    public static unsafe Transform[] ParseSkeleton(RayFile file,GameObject Root,out Matrix4x4[] InverseTransforms)
    {
        Transform[] Out = new Transform[file.Header.sec0];
        InverseTransforms = new Matrix4x4[file.Header.sec0];

        int[] Parents = new int[file.Header.sec0];

        for (int i = 0; i < file.Header.sec0; i++)
        {
            string[] Parts = file.GetStringBuffer(i * 2).Buffer.Split('|');

            GameObject Temp = new GameObject(Parts[0]);

            fixed (byte* Adress = &file.Buffers[(i * 2) + 1][0])
            {
                Matrix4x4* Data = GetMatrixBuffer(Adress);

                Temp.transform.position = Data[2].ExtractPosition();
                Temp.transform.rotation = Data[2].ExtractRotation();
                Temp.transform.localScale = Data[2].ExtractScale();

                InverseTransforms[i] = Data[3];
            }

            Out[int.Parse(Parts[1])] = Temp.transform;

            Parents[int.Parse(Parts[1])] = int.Parse(Parts[2]);
        }

        for (int i = 0; i < Parents.Length; i++)
        {
            if (Parents[i] != -1)
                Out[i].transform.parent = Out[Parents[i]];
            else
                Out[i].transform.parent = Root.transform;
        }

        return Out;
    }

    public static unsafe Matrix4x4* GetMatrixBuffer(byte* Adress)
    {
        return (Matrix4x4*)Adress;
    }

    public static GameObject[] ParseModel(RayFile file,GameObject Parent,Transform[] Skeleton,Matrix4x4[] inv, Dictionary<string,string> Material,Dictionary<string, Texture2D> TextureLibs,out Material[] Mats,out UnityEngine.Mesh[] Meshes)
    {
        List<GameObject> Out = new List<GameObject>();

        Mats = new Material[file.Header.sec0];
        Meshes = new UnityEngine.Mesh[file.Header.sec0];

        for (int i = 0; i < file.Header.sec0; i++)
        {
            int index = i * 2;

            RayFile meshfile = RayFile.ParseFile(file.Buffers[index + 1]);

            GameObject Mesh = ParseMesh(meshfile, file.GetStringBuffer(index).Buffer, Skeleton, inv,out Mats[i],out Meshes[i]);

            if (Material.ContainsKey(meshfile.GetStringBuffer(6).Buffer))
            {
                string key = Material[meshfile.GetStringBuffer(6).Buffer];

                if (TextureLibs.ContainsKey(key))
                {
                    Mesh.GetComponent<Renderer>().sharedMaterial.mainTexture = TextureLibs[key];
                }
            }

            Mesh.transform.parent = Parent.transform;
        }

        return Out.ToArray();
    }

    public static GameObject ParseMesh(RayFile file,string name,Transform[] Skeleton,Matrix4x4[] inv,out Material mat,out UnityEngine.Mesh OutMesh)
    {
        GameObject Out = new GameObject(name);

        OutMesh = new UnityEngine.Mesh();
        OutMesh.name = name;

        OutMesh.bindposes = inv;
        OutMesh.vertices = LoadV3Buffer(file.Buffers[0]);
        OutMesh.normals = LoadV3Buffer(file.Buffers[1]);
        OutMesh.uv = LoadV2Buffer(file.Buffers[2]);

        OutMesh.triangles = LoadintBuffer(file.Buffers[5]);

        OutMesh.boneWeights = LoadWeightBuffer(LoadintBuffer(file.Buffers[3]),LoadV4Buffer(file.Buffers[4]));

        Out.gameObject.AddComponent<SkinnedMeshRenderer>();
        Out.gameObject.GetComponent<SkinnedMeshRenderer>().sharedMesh = OutMesh;
        Out.gameObject.GetComponent<SkinnedMeshRenderer>().rootBone = Skeleton[0];
        Out.gameObject.GetComponent<SkinnedMeshRenderer>().bones = Skeleton;
        mat = new Material(Shader.Find("Diffuse"));
        mat.name = Out.name + "_mat";
        Out.GetComponent<Renderer>().material = mat;

        return Out;
    }

    public unsafe static Vector4[] LoadV4Buffer(byte[] buffer)
    {
        Vector4[] Out = new Vector4[buffer.Length / sizeof(Vector4)];

        fixed (byte* buff = &buffer[0])
        {
            Vector4* Buffer = (Vector4*)buff;

            for (int i = 0; i < Out.Length; i++)
            {
                Out[i] = Buffer[i];
            }
        }

        return Out;
    }

    public unsafe static Vector3[] LoadV3Buffer(byte[] buffer)
    {
        Vector3[] Out = new Vector3[buffer.Length / sizeof(Vector3)];

        fixed (byte* buff = &buffer[0])
        {
            Vector3* Buffer = (Vector3*)buff;

            for (int i = 0; i < Out.Length; i++)
            {
                Out[i] = Buffer[i];
            }
        }

        return Out;
    }

    public unsafe static Vector2[] LoadV2Buffer(byte[] buffer)
    {
        Vector2[] Out = new Vector2[buffer.Length / sizeof(Vector2)];

        fixed (byte* buff = &buffer[0])
        {
            Vector2* Buffer = (Vector2*)buff;

            for (int i = 0; i < Out.Length; i++)
            {
                Out[i] = Buffer[i];
            }
        }

        return Out;
    }

    public unsafe static int[] LoadintBuffer(byte[] buffer)
    {
        int[] Out = new int[buffer.Length / sizeof(int)];

        fixed (byte* buff = &buffer[0])
        {
            int* Buffer = (int*)buff;

            for (int i = 0; i < Out.Length; i++)
            {
                Out[i] = Buffer[i];
            }
        }

        return Out;
    }

    public unsafe static BoneWeight[] LoadWeightBuffer(int[] Indecies,Vector4[] Weights)
    {
        BoneWeight[] Out = new BoneWeight[Weights.Length];

        for (int i = 0; i < Weights.Length; i++)
        {
            Out[i].boneIndex0 = Indecies[(i * 4)];
            Out[i].weight0 = Weights[i].x;

            Out[i].boneIndex1 = Indecies[(i * 4) + 1];
            Out[i].weight1 = Weights[i].y;

            Out[i].boneIndex2 = Indecies[(i * 4) + 2];
            Out[i].weight2 = Weights[i].z;

            Out[i].boneIndex3 = Indecies[(i * 4) + 3];
            Out[i].weight3 = Weights[i].w;
        }

        return Out;
    }

    public static Dictionary<string, string> ParseMaterial(RayFile pac)
    {
        Dictionary<string, string> Out = new Dictionary<string, string>();

        for (int i = 0; i < pac.Header.HeaderCount; i++)
        {
            Out.Add(pac.GetStringBuffer(i).Buffer.Split('|')[0], pac.GetStringBuffer(i).Buffer.Split('|')[1]);
        }

        return Out;
    }

    public static Dictionary<string, Texture2D> LoadTextures(RayFile pac,out Texture2D[] Textures)
    {
        Dictionary<string, Texture2D> Out = new Dictionary<string, Texture2D>();

        Textures = new Texture2D[pac.Header.sec0];

        for (int i = 0; i < pac.Header.sec0; i++)
        {
            RayFile TextureFile = RayFile.ParseFile(pac.Buffers[i]);

            Texture2D Outt = new Texture2D(0,0);
            Outt.LoadImage(TextureFile.Buffers[1]);

            Outt.name = TextureFile.GetStringBuffer(0).Buffer;

            Textures[i] = Outt;

            Out.Add(TextureFile.GetStringBuffer(0).Buffer, Textures[i]);
        }

        return Out;
    }

    public static Dictionary<string,string> ParseBoneKey(RayFile In)
    {
        Dictionary<string, string> Out = new Dictionary<string, string>();

        for (int i = 0; i < In.Header.HeaderCount; i++)
        {
            string[] Parts = In.GetStringBuffer(i).Buffer.Split('=');

            Out.Add(Parts[0],Parts[1]);
        }

        return Out;
    }

    public static unsafe AnimationClip ParseAnimation(Dictionary<string,string> BoneKey,RayFile file)
    {
        AnimationClip Out = new AnimationClip();

        Out.name = file.GetStringBuffer(0).Buffer;

        int count = file.Header.HeaderCount - 1;

        for (int i = 0; i < count/2; i++)
        {
            int index = (i * 2) + 1;

            string[] name = file.GetStringBuffer(index).Buffer.Split('-');

            if (name[1] == "transform")
            {
                AnimationCurve XCurve = new AnimationCurve();
                AnimationCurve YCurve = new AnimationCurve();
                AnimationCurve ZCurve = new AnimationCurve();

                AnimationCurve RotationCurveX = new AnimationCurve();
                AnimationCurve RotationCurveY = new AnimationCurve();
                AnimationCurve RotationCurveZ = new AnimationCurve();
                AnimationCurve RotationCurveW = new AnimationCurve();

                AnimationCurve XScCurve = new AnimationCurve();
                AnimationCurve YScCurve = new AnimationCurve();
                AnimationCurve ZScCurve = new AnimationCurve();

                fixed (byte* b = &file.Buffers[index + 1][0])
                {
                    AnimKey* Data = AnimKey.GetBuffer(b);

                    int size = file.Buffers[index + 1].Length / sizeof(AnimKey);

                    for (int time = 0; time < size; time++)
                    {
                        AnimKey Key = Data[time];

                        XCurve.AddKey(new Keyframe(time/60.0f,Key.Position.x));
                        YCurve.AddKey(new Keyframe(time / 60.0f, Key.Position.y));
                        ZCurve.AddKey(new Keyframe(time / 60.0f, Key.Position.z));

                        RotationCurveX.AddKey(new Keyframe(time / 60.0f, Key.Rotation.x));
                        RotationCurveY.AddKey(new Keyframe(time / 60.0f, Key.Rotation.y));
                        RotationCurveZ.AddKey(new Keyframe(time / 60.0f, Key.Rotation.z));
                        RotationCurveW.AddKey(new Keyframe(time / 60.0f, Key.Rotation.w));

                        XScCurve.AddKey(new Keyframe(time / 60.0f, Key.Scale.x));
                        YScCurve.AddKey(new Keyframe(time / 60.0f, Key.Scale.y));
                        ZScCurve.AddKey(new Keyframe(time / 60.0f, Key.Scale.z));
                    }
                }

                if (BoneKey.ContainsKey(name[0]))
                {
                    Out.SetCurve(BoneKey[name[0]], typeof(Transform), "localPosition.x", XCurve);
                    Out.SetCurve(BoneKey[name[0]], typeof(Transform), "localPosition.y", YCurve);
                    Out.SetCurve(BoneKey[name[0]], typeof(Transform), "localPosition.z", ZCurve);

                    Out.SetCurve(BoneKey[name[0]], typeof(Transform), "localRotation.x", RotationCurveX);
                    Out.SetCurve(BoneKey[name[0]], typeof(Transform), "localRotation.y", RotationCurveY);
                    Out.SetCurve(BoneKey[name[0]], typeof(Transform), "localRotation.z", RotationCurveZ);
                    Out.SetCurve(BoneKey[name[0]], typeof(Transform), "localRotation.w", RotationCurveW);

                    Out.SetCurve(BoneKey[name[0]], typeof(Transform), "localScale.x", XScCurve);
                    Out.SetCurve(BoneKey[name[0]], typeof(Transform), "localScale.x", YScCurve);
                    Out.SetCurve(BoneKey[name[0]], typeof(Transform), "localScale.x", ZScCurve);
                }
            }
        }

        return Out;
    }

    public unsafe struct AnimKey
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public float scaler;

        public static AnimKey* GetBuffer(byte* offset)
        {
            return (AnimKey*)offset;
        }
    }
}

public static class MatrixExtensions
{
    public static Quaternion ExtractRotation(this Matrix4x4 matrix)
    {
        Vector3 forward;
        forward.x = matrix.m02;
        forward.y = matrix.m12;
        forward.z = matrix.m22;

        Vector3 upwards;
        upwards.x = matrix.m01;
        upwards.y = matrix.m11;
        upwards.z = matrix.m21;

        return Quaternion.LookRotation(forward, upwards);
    }

    public static Vector3 ExtractPosition(this Matrix4x4 matrix)
    {
        Vector3 position;
        position.x = matrix.m03;
        position.y = matrix.m13;
        position.z = matrix.m23;
        return position;
    }

    public static Vector3 ExtractScale(this Matrix4x4 matrix)
    {
        Vector3 scale;
        scale.x = new Vector4(matrix.m00, matrix.m10, matrix.m20, matrix.m30).magnitude;
        scale.y = new Vector4(matrix.m01, matrix.m11, matrix.m21, matrix.m31).magnitude;
        scale.z = new Vector4(matrix.m02, matrix.m12, matrix.m22, matrix.m32).magnitude;
        return scale;
    }
}
