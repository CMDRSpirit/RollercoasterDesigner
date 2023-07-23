/*
MIT License

Copyright (c) 2023 Pascal Zwick

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace Rollercoaster
{
    [CreateAssetMenu(fileName = "NewTrackDescription", menuName = "Rollercoaster/Track Description", order = 2)]
    public class TrackDescription : ScriptableObject
    {
        public float HeartlineOffset = 0.75f;
        public float TrackWidth = 1;

        //Graphics
        public Mesh TrackMesh;
        public float MeshLength = 1;

        [ColorUsage(false, false)]
        public Color TrackGizmoColor = Color.green;
        
        [System.Serializable]
        public struct TrackObject
        {
            public GameObject Prefab;
            public float Distance;
            public float Offset;
        }
        public TrackObject[] TrackObjects;

        //Mesh Generation
        public GameObject GenerateMesh(TrackSection section)
        {
            if (!TrackMesh)
                return null;

            GameObject trackMeshObj = new GameObject("Track Mesh");
            trackMeshObj.transform.parent = section.transform;
            trackMeshObj.transform.localPosition = Vector3.zero;
            trackMeshObj.transform.localRotation = Quaternion.identity;
            trackMeshObj.isStatic = true;

            //Generate Mesh
            int[] bi = TrackMesh.GetIndices(0);
            Vector3[] bv = TrackMesh.vertices;
            Vector3[] bn = TrackMesh.normals;
            Vector2[] bu = TrackMesh.uv;

            List<Vector3> verts = new List<Vector3>(), norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> idx = new List<int>();


            float secLength = section.SplineLength;
            float baseLength = MeshLength;
            int iterations = (int)(secLength / baseLength);
            float scale = secLength / (baseLength * iterations);

            float t0 = 0;
            for (int i = 0; i < iterations; ++i)
            {
                //float3 der = section.EvaluateDerivative(t0);
                //float t1 = t0 + scale * baseLength / length(der);
                float t1 = t0 + section.DeltaMToDeltaT(t0, scale * baseLength);
                if (i == iterations - 1)
                    t1 = section.getTMax();

                //loop tris
                int baseIndex = verts.Count;
                for (int j = 0; j < bv.Length; ++j)
                {
                    float3 v = bv[j];
                    float3 n = bn[j];
                    float2 u = bu[j];

                    float t = lerp(t0, t1, clamp(v.z / baseLength, 0, 1));

                    float3 p1;
                    quaternion r1;
                    section.EvaluateSpline(t, out p1, out r1);

                    v = mul(r1, new float3(v.x, v.y, 0)) + p1;
                    n = mul(r1, n);

                    verts.Add(v);
                    norms.Add(n);
                    uvs.Add(u);
                }

                for (int j = 0; j < bi.Length; ++j)
                {
                    idx.Add(baseIndex + bi[j]);
                }

                t0 = t1;
            }
            //End generation

            //Add renderer
            MeshFilter filter = trackMeshObj.AddComponent<MeshFilter>();
            Mesh mesh = new Mesh();
            mesh.name = "mesh_" + section.name;
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;//TODO switch to multiple meshs

            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(idx, 0);
            filter.mesh = mesh;


            MeshRenderer renderer = trackMeshObj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = section.TrackMaterial;
#if UNITY_EDITOR
            renderer.receiveGI = ReceiveGI.LightProbes;
#endif

            //Add objects
            if (TrackObjects != null)
            {
                foreach (var to in TrackObjects)
                {
                    secLength = section.SplineLength - to.Offset;
                    iterations = (int)(secLength / to.Distance);

                    float delta = (iterations * to.Distance) / secLength;
                    float t = section.DeltaMToDeltaT(0, to.Offset + (delta + to.Distance) * 0.5f);

                    for (int i = 0; i < iterations; ++i)
                    {
                        float3 p;
                        quaternion r;
                        section.EvaluateSpline(t, out p, out r);

                        GameObject instance = GameObject.Instantiate(to.Prefab, trackMeshObj.transform);
                        instance.transform.localPosition = p;
                        instance.transform.localRotation = r;

                        t += section.DeltaMToDeltaT(t, to.Distance);
                    }
                }
            }

            return trackMeshObj;
        }

    }
}
