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
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rollercoaster
{
    public class CoasterTrack : MonoBehaviour
    {
        public TrackDescription DefaultTrackDescription;
        public Material DefaultTrackMaterial;

        public bool IsClosed = false;

        public List<TrackSection> TrackSections;
        public float TMax { get; private set; }

        private void Awake()
        {
            CombineSections();
        }

        private void OnValidate()
        {
            if (TrackSections == null)
                TrackSections = new List<TrackSection>();
            
            if(TrackSections.Count == 0 && DefaultTrackDescription && DefaultTrackMaterial)
                AddSection();

            CombineSections();
        }

        public void CombineSections()
        {
            if (TrackSections.Count < 2)
                return;

            var sa = TrackSections[0];
            sa.UpdateSpline();

            TMax = sa.getTMax();
            for (int i = 1; i < TrackSections.Count; ++i)
            {
                var sb = TrackSections[i];
                sb.PrevSection = sa;
                sa.NextSection = sb;

                sb.transform.position = sa.transform.TransformPoint(sa.NodesPosition[sa.NodesPosition.Count - 1]);
                sb.StartSlope = sa.EvaluateDerivative(sa.getTMax());

                if(IsClosed && i == TrackSections.Count - 1)
                {
                    sa = TrackSections[0];
                    sb.EndSlope = sa.EvaluateDerivative(0);
                    sb.NodesPosition[sb.NodesPosition.Count - 1] = sb.transform.InverseTransformPoint(sa.transform.TransformPoint(sa.NodesPosition[0]));

                    sa.PrevSection = sb;
                    sb.NextSection = sa;
                }

                sb.UpdateSpline();
                TMax += sb.getTMax();

                sa = sb;
            }
        }


        public TrackSection GetSection(float t_global, out float local_t)
        {
            local_t = t_global;
            foreach (var s in TrackSections)
            {
                if (local_t < s.getTMax())
                    return s;

                local_t -= s.getTMax();
            }

            return null;
        }
        public float GetTStart(TrackSection sec)
        {
            float t = 0;
            foreach (var s in TrackSections)
            {
                if (s == sec)
                    return t;

                t += s.getTMax();
            }
            return 0;
        }

        public float DeltaMToDeltaTNegative(float t0, float deltaM)
        {
            int steps = 64;
            float m0 = 0;

            float t;
            var sec = GetSection(t0, out t);
            float ts = GetTStart(sec);
            float dt = deltaM / (float)steps;
            for (int j = 0; j < steps; ++j)
            {
                float m1 = m0 + dt * length(sec.EvaluateDerivative(t));
                if (m1 < deltaM)
                {
                    float d0 = m0 - deltaM;
                    float d1 = m1 - deltaM;

                    return t + ts - d0 / (d1 - d0) * dt - t0;
                }
                t += dt;
                
                if (t < 0)
                {
                    sec = sec.PrevSection;
                    t += sec.getTMax();
                    ts = GetTStart(sec);
                }

                m0 = m1;
            }
            return 0;
        }


        public TrackSection AddSection(int index = -1)
        {
            GameObject obj = new GameObject("TrackSection_" + TrackSections.Count);
            obj.transform.parent = this.transform;
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;

            var sec = obj.AddComponent<TrackSection>();
            sec.TrackDesc = DefaultTrackDescription;
            sec.TrackMaterial = DefaultTrackMaterial;

            if (TrackSections.Count != 0) {
                var s = TrackSections[TrackSections.Count - 1];
                sec.NodesPosition[1] = sec.NodesPosition[0] + s.EvaluateDerivative(s.getTMax());
            }

            sec.UpdateSpline();

#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(obj, "Create Track Section");
#endif

            if (index != -1)
                TrackSections.Insert(index, sec);
            else
                TrackSections.Add(sec);

            CombineSections();

            return sec;
        }

        public void RemoveSection(int index)
        {
            //GameObject.DestroyImmediate(TrackSections[index].gameObject);
#if UNITY_EDITOR
            Undo.DestroyObjectImmediate(TrackSections[index].gameObject);

            Undo.RecordObject(this, "Remove Track Section");
#endif

            TrackSections.RemoveAt(index);

            CombineSections();
        }

        public void SplitSection(TrackSection sec, int nodeIndex)
        {
            float3 slope = sec.EvaluateDerivative(nodeIndex);
            float roll = sec.EvaluateRoll(nodeIndex);

            var np = sec.NodesPosition;
            var nr = sec.NodesRoll;

            var np1 = np.GetRange(nodeIndex, np.Count - nodeIndex);
            for (int i = 0; i < np1.Count; ++i)
                np1[i] = (float3)sec.transform.TransformPoint(np1[i]) - (float3)sec.transform.TransformPoint(sec.NodesPosition[nodeIndex]);

            var nr1 = new List<float2>();
            foreach(var r in nr)
            {
                if (r.x >= nodeIndex)
                    nr1.Add(float2(r.x - nodeIndex, r.y));
            }
            if(nr1.Count == 0 || nr1[0].x != 0)
                nr1.Insert(0, float2(0, roll));

            var nr2 = new List<float2>();
            foreach (var r in nr)
            {
                if (r.x < nodeIndex)
                    nr2.Add(r);
            }
            if (nr2.Count == 0 || nr2[nr2.Count - 1].x != nodeIndex)
                nr2.Add(float2(nodeIndex, roll));

            //
            sec.NodesPosition = np.GetRange(0, nodeIndex + 1);
            sec.NodesRoll = nr2;
            sec.EndSlope = slope;

            //
            int secidx = TrackSections.IndexOf(sec);
            var sec1 = AddSection(secidx + 1);
            sec1.splineType = sec.splineType;

            sec1.NodesPosition = np1;
            sec1.NodesRoll = nr1;
            sec1.StartSlope = slope;
            sec1.UpdateSpline();

            CombineSections();
        }

        public void GenerateTrackMesh()
        {
            foreach (var s in TrackSections)
                s.GenerateTrackMesh();
        }
        public void DeleteTrackMesh()
        {
            foreach (var s in TrackSections)
                s.DeleteTrackMesh();
        }

    }


#if UNITY_EDITOR
    [CustomEditor(typeof(CoasterTrack))]
    public class CoasterTrackEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            CoasterTrack track = (CoasterTrack)target;

            /*if (GUILayout.Button("Add Section"))
            {
                track.AddSection();
            }*/
            if (GUILayout.Button("Update Conenctions"))
            {
                track.CombineSections();
            }
            if(GUILayout.Button("Generate Track Mesh"))
            {
                track.GenerateTrackMesh();
            }
            if (GUILayout.Button("Delete Track Mesh"))
            {
                track.DeleteTrackMesh();
            }
        }

        private void OnSceneGUI()
        {
            CoasterTrack track = (CoasterTrack)target;

            EditorGUI.BeginChangeCheck();

            int secID = -1;
            int secType = 0;

            Handles.BeginGUI();
            for (int i=0; i<track.TrackSections.Count; ++i)
            {
                var sec = track.TrackSections[i];

                float t = sec.getTMax() * 0.5f;
                float3 p = sec.transform.TransformPoint(sec.EvaluatePosition(t));


                float3 screenPoint = Camera.current.WorldToScreenPoint(p);
                float size = 24;
                Rect rect = new Rect(screenPoint.x + step(screenPoint.z, 0) * 1e6f, Camera.current.pixelHeight - screenPoint.y, 3 * size, size);
                GUILayout.BeginArea(rect);
                if (GUILayout.Button("- Section", GUILayout.Height(size)))
                {
                    secID = i;
                    break;
                }
                GUILayout.EndArea();

                //End are
                if(!track.IsClosed && i == track.TrackSections.Count - 1)
                {
                    p = sec.transform.TransformPoint(sec.EvaluatePosition(sec.getTMax() + 0.5f));

                    screenPoint = Camera.current.WorldToScreenPoint(p);
                    rect = new Rect(screenPoint.x + step(screenPoint.z, 0) * 1e6f, Camera.current.pixelHeight - screenPoint.y, 4 * size, 2 * size);
                    GUILayout.BeginArea(rect);
                    GUILayout.BeginVertical();
                    if (GUILayout.Button("+ Section", GUILayout.Height(size)))
                    {
                        secID = i;
                        secType = 1;
                        break;
                    }
                    if (GUILayout.Button("Close Track", GUILayout.Height(size)))
                    {
                        secID = i;
                        secType = 2;
                        break;
                    }
                    GUILayout.EndVertical();
                    GUILayout.EndArea();
                }
            }

            Handles.EndGUI();

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Changed sections");

                if (secID != -1)
                {
                    if (secType == 0)
                    {
                        track.IsClosed = false;
                        track.RemoveSection(secID);
                    }
                    else if (secType == 1)
                        track.AddSection();
                    else if (secType == 2)
                    {
                        track.IsClosed = true;
                        track.AddSection();
                    }
                }
            }
        }
    }
#endif
}
