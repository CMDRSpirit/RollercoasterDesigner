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
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rollercoaster
{

    public class TrackSection : MonoBehaviour
    {
        [System.Serializable]
        public enum SplineType
        {
            CUBIC, NURBS
        }
        [System.Serializable]
        public enum RollType
        {
            ANGLE, VECTOR
        }

        public TrackDescription TrackDesc;
        public Material TrackMaterial;

        public TrackSection NextSection;
        public TrackSection PrevSection;

        //Physics
        public bool AffectsTrain;
        public float Acceleration;
        public float Breaking;
        public float TargetVelocity;
        public bool PhysicsActive;
        public bool StopTrain;
        //

        public SplineType splineType = SplineType.CUBIC;
        public float3 StartSlope;
        public float3 EndSlope;
        public List<float3> NodesPosition;
        public List<float2> NodesRoll;

        public float SplineLength { get; private set; }

        private ASpline splineX, splineY, splineZ;
        private ASpline splineRX, splineRY, splineRZ;
        private ASpline splineRoll;

        private GameObject trackMeshObject;

        private bool IsFitted;

        private void Awake()
        {
            UpdateSpline();
        }

        private void OnValidate()
        {
            if (NodesPosition == null)
            {
                NodesPosition = new List<float3>();
                NodesPosition.Add(0);
                NodesPosition.Add(this.transform.InverseTransformDirection(this.transform.forward) * 4);
            }
            if (NodesRoll == null)
            {
                NodesRoll = new List<float2>();
                NodesRoll.Add(0);
                NodesRoll.Add(float2(1, 0));
            }

            UpdateSpline();
        }


        // ################# Physics ######################
        public void AffectTrain(CoasterTrain train, float deltaTime)
        {
            if (!AffectsTrain || !PhysicsActive)
                return;

            float targetVel = StopTrain ? 0 : TargetVelocity;
            float vel = train.velocity;
            float deltaSpeed = targetVel - vel;
            float acc = deltaSpeed > 0 ? (1 - exp(- deltaSpeed * 8)) * Acceleration : -Breaking;

            train.velocity = StopTrain ? vel + max(deltaSpeed, acc * deltaTime) : vel + acc * deltaTime;
        }

        // ################# Spline Generation ######################
        public void UpdateSpline()
        {
            float[] ts = new float[NodesPosition.Count];
            float[] xs = new float[NodesPosition.Count];
            float[] ys = new float[NodesPosition.Count];
            float[] zs = new float[NodesPosition.Count];

            for (int i = 0; i < ts.Length; ++i)
            {
                var cp = NodesPosition[i];
                ts[i] = i;

                xs[i] = cp.x;
                ys[i] = cp.y;
                zs[i] = cp.z;
            }

            float3 startSlope = lengthsq(StartSlope) != 0 ? StartSlope : float.NaN;
            float3 endSlope = lengthsq(EndSlope) != 0 ? EndSlope : float.NaN;

            if(splineType == SplineType.CUBIC)
            {
                splineX = new CubicSpline();
                splineY = new CubicSpline();
                splineZ = new CubicSpline();
            }
            else if (splineType == SplineType.NURBS)
            {
                splineX = new NurbsSpline();
                splineY = new NurbsSpline();
                splineZ = new NurbsSpline();
            }

            splineX.Fit(ts, xs, startSlope: startSlope.x, endSlope: endSlope.x);
            splineY.Fit(ts, ys, startSlope: startSlope.y, endSlope: endSlope.y);
            splineZ.Fit(ts, zs, startSlope: startSlope.z, endSlope: endSlope.z);

            //Roll          
            float[] trs = new float[NodesRoll.Count];
            float[] rs = new float[NodesRoll.Count];
            for (int i = 0; i < trs.Length; ++i)
            {
                trs[i] = NodesRoll[i].x;
                rs[i] = NodesRoll[i].y;
            }

            splineRoll = new CubicSpline();
            splineRoll.Fit(trs, rs, startSlope: 0, endSlope: 0);

            //Right vector
            splineRX = new CubicSpline();
            splineRY = new CubicSpline();
            splineRZ = new CubicSpline();

            float[] rxs = new float[NodesRoll.Count];
            float[] rys = new float[NodesRoll.Count];
            float[] rzs = new float[NodesRoll.Count];
            for (int i = 0; i < trs.Length; ++i)
            {
                float tr = NodesRoll[i].x;
                float3 r = EvaluateOrthogonal(tr, RollType.ANGLE);
                rxs[i] = r.x;
                rys[i] = r.y;
                rzs[i] = r.z;
            }
            splineRX.Fit(trs, rxs, startSlope: 0, endSlope: 0);
            splineRY.Fit(trs, rys, startSlope: 0, endSlope: 0);
            splineRZ.Fit(trs, rzs, startSlope: 0, endSlope: 0);


            //Calculate spline length
            SplineLength = 0;
            float t = 0;
            for (int i = 1; i < ts.Length; ++i)
            {
                for (int j = 0; j < 64; ++j)
                {
                    SplineLength += length(EvaluateDerivative(t)) / 64.0f;
                    t += 1.0f / 64.0f;
                }
            }

            IsFitted = true;
        }

        // ################# Evaluation ######################
        public float getTMax()
        {
            return NodesPosition.Count - 1;
        }
        public float DeltaMToDeltaT(float t0, float deltaM)
        {
            int steps = 64;
            float t = t0;
            float m0 = 0;
            for (int j = 0; j < steps; ++j)
            {
                float m1 = m0 + deltaM * length(EvaluateDerivative(t)) / (float)steps;
                if (m1 > deltaM)
                {
                    float d0 = m0 - deltaM;
                    float d1 = m1 - deltaM;

                    return t - (deltaM * d0/((d1 - d0) * steps)) - t0;
                }
                t += deltaM / (float)steps;
                m0 = m1;
            }

            return t;
        }

        public float3 EvaluatePosition(float t)
        {
            if (!IsFitted || splineX == null)
                return 0;
            return float3(splineX.Eval(t), splineY.Eval(t), splineZ.Eval(t));
        }

        public float3 EvaluateDerivative(float t)
        {
            if (!IsFitted || splineX == null)
                return float3(0, 0, 1);

            return float3(splineX.EvalSlope(t), splineY.EvalSlope(t), splineZ.EvalSlope(t));
        }

        public float3 EvaluateOrthogonal(float t, RollType rollType)//Right vector
        {
            if (t < NodesRoll[0].x || t > NodesRoll[NodesRoll.Count - 1].x)
                rollType = RollType.ANGLE;

            if (rollType == RollType.ANGLE)
            {
                float3 forward = math.normalize(EvaluateDerivative(t));
                float roll = EvaluateRoll(t);
                float3 right_default = -math.cross(forward, new float3(0, 1, 0));

                return math.mul(Quaternion.AngleAxis(-roll, forward), right_default);
            }
            else
            {
                if (!IsFitted || splineRX == null)
                    return float3(1, 0, 0);

                return float3(splineRX.Eval(t), splineRY.Eval(t), splineRZ.Eval(t));
            }
        }

        public float3 EvaluateUp(float3 forward, float3 right)//Up vector
        {
            return math.normalize(math.cross(forward, right));
        }

        public float EvaluateRoll(float t)
        {
            if (NodesRoll.Count == 0 || !IsFitted || splineRoll == null)
                return 0;
            return t < NodesRoll[0].x ? NodesRoll[0].y : (t > NodesRoll[NodesRoll.Count - 1].x ? NodesRoll[NodesRoll.Count - 1].y : splineRoll.Eval(t));
        }

        public void EvaluateSpline(float t, out float3 position, out quaternion rotation)
        {
            float3 pos = EvaluatePosition(t);
            float3 forward = EvaluateDerivative(t);
            float3 right = EvaluateOrthogonal(t, RollType.VECTOR);
            float3 up = EvaluateUp(forward, right);

            position = pos - up * TrackDesc.HeartlineOffset;
            rotation = Quaternion.LookRotation(math.normalize(forward), up);
        }

        //####################### Gizmos ##################
        private void OnDrawGizmos()
        {

            if (NodesPosition != null && TrackDesc)
            {
                if (!trackMeshObject)
                    trackMeshObject = this.transform.Find("Track Mesh")?.gameObject;

                if (trackMeshObject)
                    return;

                Gizmos.color = TrackDesc.TrackGizmoColor;
                Gizmos.matrix = this.transform.localToWorldMatrix;

                float3 p0;
                quaternion r0;
                EvaluateSpline(0, out p0, out r0);

                float3 secEnd = mul(r0, float3(TrackDesc.TrackWidth, 0, 0));
                Gizmos.DrawLine(p0 - secEnd, p0 + secEnd);

                float tmax = getTMax();

                float gap = 1.0f;
                int iterations = (int)max(1, SplineLength / gap);
                gap = SplineLength / (float)iterations;
                float t = 0;
                for (int i = 0; i < iterations; ++i)
                {
                    float3 der = EvaluateDerivative(t);
                    t += gap / length(der);
                    if (i == iterations - 1)
                        t = tmax;
                    t = min(t, tmax);

                    float3 p1;
                    quaternion r1;
                    EvaluateSpline(t, out p1, out r1);

                    float3 p2 = p1 - mul(r1, float3(TrackDesc.TrackWidth * 0.5f, 0, 0));
                    float3 p3 = p1 + mul(r1, float3(TrackDesc.TrackWidth * 0.5f, 0, 0));
                    float3 p4 = p0 - mul(r0, float3(TrackDesc.TrackWidth * 0.5f, 0, 0));
                    float3 p5 = p0 + mul(r0, float3(TrackDesc.TrackWidth * 0.5f, 0, 0));


                    //Left and right tracks
                    Gizmos.DrawLine(p4, p2);
                    Gizmos.DrawLine(p5, p3);

                    //Center
                    Gizmos.DrawLine(0.5f * (p2 + p4), 0.5f * (p3 + p5));
                    

                    //Heartline
                    Gizmos.DrawLine(p0 + mul(r0, float3(0, TrackDesc.HeartlineOffset, 0)), p1 + mul(r1, float3(0, TrackDesc.HeartlineOffset, 0)));

                    p0 = p1;
                    r0 = r1;
                }
            }
        }

        //Helpers
        public void InsertPositionNode(int index, float3 value)
        {
            NodesPosition.Insert(index, value);
            for (int i = 0; i < NodesRoll.Count; ++i)
            {
                float2 n = NodesRoll[i];
                if (n.x > index - 1)
                    NodesRoll[i] += float2(1, 0);
            }
        }
        public void RemovePositionNode(int index)
        {
            NodesPosition.RemoveAt(index);
            for (int i = 0; i < NodesRoll.Count; ++i)
            {
                float2 n = NodesRoll[i];
                if (n.x > index + 1)
                    NodesRoll[i] -= float2(1, 0);
                else if(n.x >= index)
                    NodesRoll[i] += float2(-0.5f*frac(n.x), 1);
            }
        }
        public void InsertRollNode(int index, float2 value)
        {
            NodesRoll.Insert(index, value);
        }
        public void RemoveRollNode(int index)
        {
            NodesRoll.RemoveAt(index);
        }

        public void GenerateTrackMesh()
        {
            DeleteTrackMesh();

            trackMeshObject = TrackDesc.GenerateMesh(this);
        }
        public void DeleteTrackMesh()
        {
            if (!trackMeshObject)
                trackMeshObject = this.transform.Find("Track Mesh")?.gameObject;
            if (trackMeshObject)
                GameObject.DestroyImmediate(trackMeshObject);
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(TrackSection)), CanEditMultipleObjects]
    public class TrackSectionEditor : Editor
    {

        private static bool axisGlobal, axisVisible = true, buttonsVisible = true, splitSectionMode = false;

        private const float MouseDistThreshhold = 10;
        private Ray mouseRay;

        private bool mouseDown;

        private float distanceToRay(float3 posWS, Ray ray)
        {
            return length(cross(ray.direction, posWS - (float3)ray.origin));
        }

        private void drawWireSphere(float3 pos, float scale, Color col)
        {
            Handles.color = col;
            Handles.DrawWireDisc(pos, Vector3.forward, scale);
            Handles.DrawWireDisc(pos, Vector3.right, scale);
        }

        private void OnSceneGUI()
        {
            TrackSection track = (TrackSection)target;
            if (!track.TrackDesc)
                return;

            if (Event.current.type == EventType.MouseDown)
                mouseDown = true;
            else if (Event.current.type == EventType.MouseUp)
                mouseDown = false;

            if (Event.current.type == EventType.Layout && !mouseDown)
                mouseRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);


            Handles.matrix = track.transform.localToWorldMatrix;
            EditorGUI.BeginChangeCheck();

            Handles.BeginGUI();
            Rect area_rect = new Rect(64, 8, 256, 128);
            GUILayout.BeginArea(area_rect);
            GUILayout.BeginVertical();

            if (GUILayout.Button("Toggle Handle Axis Orientation", GUILayout.Height(24)))
                axisGlobal = !axisGlobal;
            if (GUILayout.Button("Toggle Handle Axis Visibility", GUILayout.Height(24)))
                axisVisible = !axisVisible;
            if (GUILayout.Button("Toggle Buttons", GUILayout.Height(24)))
                buttonsVisible = !buttonsVisible;
            if (GUILayout.Button("Toggle Split Mode", GUILayout.Height(24)))
                splitSectionMode = !splitSectionMode;

            GUILayout.EndVertical();
            GUILayout.EndArea();
            Handles.EndGUI();

            //Slopes
            float3 ps = 0, pe = 0;
            if (lengthsq(track.StartSlope) != 0)
                ps = (float3)Handles.PositionHandle(track.StartSlope + track.NodesPosition[0], Quaternion.LookRotation(normalize(track.EvaluateDerivative(0)), Vector3.up)) - track.NodesPosition[0];
            if (lengthsq(track.EndSlope) != 0)
                pe = (float3)Handles.PositionHandle(track.EndSlope + track.NodesPosition[track.NodesPosition.Count - 1], Quaternion.LookRotation(normalize(track.EvaluateDerivative(track.getTMax())), Vector3.up)) - track.NodesPosition[track.NodesPosition.Count - 1];


            //Positions
            int p_modID = -1;
            int p_modType = 0;
            float3 p_value = 0;
            for (int i = 0; i < track.NodesPosition.Count; ++i)
            {
                var node = track.NodesPosition[i];
                if (distanceToRay(track.transform.TransformPoint(node), mouseRay) > MouseDistThreshhold)
                {
                    drawWireSphere(node, 0.5f, Color.green);
                    continue;
                }

                float3 der = track.EvaluateDerivative(i);
                float3 forward = math.normalize(der);

                if (axisVisible)
                {
                    float3 newPos = axisGlobal ? Handles.PositionHandle(node, Quaternion.identity) : Handles.PositionHandle(node, Quaternion.LookRotation(forward, Vector3.up));
                    
                    if (length(newPos - node) > 0.01f)
                    {
                        p_modID = i;
                        p_value = newPos;
                        break;
                    }
                }

                if (buttonsVisible)
                {
                    //2D buttons
                    Handles.BeginGUI();

                    float3 p = node + float3(0, 1.5f, 0) * HandleUtility.GetHandleSize(node);
                    float3 screenPoint = Camera.current.WorldToScreenPoint(track.transform.TransformPoint(p));
                    float sizeX = 64;
                    float sizeY = 16;
                    Rect rect = new Rect(screenPoint.x - sizeX * 0.5f + step(screenPoint.z, 0) * 1e6f, Camera.current.pixelHeight - screenPoint.y, sizeX, sizeY);
                    GUILayout.BeginArea(rect);
                    if (GUILayout.Button("- Node", GUILayout.Height(sizeY)))
                    {
                        p_modID = i;
                        p_modType = 2;
                        break;
                    }
                    GUILayout.EndArea();

                    p = track.EvaluatePosition(i + 0.5f);
                    screenPoint = Camera.current.WorldToScreenPoint(track.transform.TransformPoint(p));
                    rect = new Rect(screenPoint.x - sizeX * 0.5f + step(screenPoint.z, 0) * 1e6f, Camera.current.pixelHeight - screenPoint.y, sizeX, sizeY);
                    GUILayout.BeginArea(rect);
                    if (GUILayout.Button("+ Node", GUILayout.Height(sizeY)))
                    {
                        p_modID = i + 1;
                        p_modType = 1;
                        p_value = p;
                        break;
                    }
                    GUILayout.EndArea();

                    Handles.EndGUI();
                }

                if (splitSectionMode)
                {
                    //2D buttons
                    Handles.BeginGUI();

                    float3 p = node + float3(0, 1.5f, 0) * HandleUtility.GetHandleSize(node);
                    float3 screenPoint = Camera.current.WorldToScreenPoint(track.transform.TransformPoint(p));
                    float sizeX = 64;
                    float sizeY = 16;
                    Rect rect = new Rect(screenPoint.x - sizeX * 0.5f + step(screenPoint.z, 0) * 1e6f, Camera.current.pixelHeight - screenPoint.y, sizeX, sizeY);
                    GUILayout.BeginArea(rect);
                    if (GUILayout.Button("Split Here", GUILayout.Height(sizeY)))
                    {
                        var ct = track.GetComponentInParent<CoasterTrack>();
                        ct.SplitSection(track, i);

                        ps = track.StartSlope;
                        pe = track.EndSlope;

                        break;
                    }
                    GUILayout.EndArea();
                    Handles.EndGUI();
                }
            }

            int r_modID = -1;
            int r_modType = 0;
            float2 r_value = 0;
            
            Handles.color = Color.white;
            for (int i = 0; i < track.NodesRoll.Count; ++i)
            {
                float2 rollNode = track.NodesRoll[i];

                float3 p;
                quaternion r;
                track.EvaluateSpline(rollNode.x, out p, out r);

                if (distanceToRay(track.transform.TransformPoint(p), mouseRay) > MouseDistThreshhold)
                {
                    drawWireSphere(p, 0.5f, Color.white);
                    continue;
                }

                float3 der = track.EvaluateDerivative(rollNode.x);
                float3 forward = math.normalize(der);

                p += mul(r, float3(0,1,0)) * track.TrackDesc.HeartlineOffset;

                if (axisVisible)
                {
                    Quaternion r1 = Handles.Disc(r, p, mul(r, Vector3.forward), max(0.5f, track.TrackDesc.HeartlineOffset), false, 5);
                    Handles.Label(p, rollNode.y.ToString("F1"));
                    float3 p1 = Handles.Slider(p, forward);
                    float n1 = dot(p1 - p, forward);

                    float newAngle = -(r1.eulerAngles.z > 180 ? r1.eulerAngles.z - 360 : r1.eulerAngles.z);
                    float angleDelta = Mathf.DeltaAngle(rollNode.y, newAngle);
                    if (abs(angleDelta) > 0.01f || abs(n1) > 0.01f)
                    {
                        r_modID = i;
                        r_value = float2(clamp(rollNode.x + n1 / length(der), 0, track.getTMax()), rollNode.y + angleDelta);
                        break;
                    }
                }

                //2D buttons
                if (buttonsVisible)
                {
                    Handles.BeginGUI();

                    p += float3(0, -1.5f, 0) * track.TrackDesc.HeartlineOffset;
                    float3 screenPoint = Camera.current.WorldToScreenPoint(track.transform.TransformPoint(p));
                    float sizeX = 64;
                    float sizeY = 16;
                    Rect rect = new Rect(screenPoint.x - sizeX * 0.5f + step(screenPoint.z, 0) * 1e6f, Camera.current.pixelHeight - screenPoint.y, sizeX, sizeY);
                    GUILayout.BeginArea(rect);
                    if (GUILayout.Button("- Roll", GUILayout.Height(sizeY)))
                    {
                        r_modID = i;
                        r_modType = 2;
                        break;
                    }
                    GUILayout.EndArea();

                    float2 rollNodeNew = i < track.NodesRoll.Count - 1 ? 0.5f * (rollNode + track.NodesRoll[i + 1]) : rollNode + float2(0.5f, 0);
                    p = track.EvaluatePosition(rollNodeNew.x) + float3(0, -1, 0) * track.TrackDesc.HeartlineOffset;
                    screenPoint = Camera.current.WorldToScreenPoint(track.transform.TransformPoint(p));
                    rect = new Rect(screenPoint.x - sizeX * 0.5f + step(screenPoint.z, 0) * 1e6f, Camera.current.pixelHeight - screenPoint.y, sizeX, sizeY);
                    GUILayout.BeginArea(rect);
                    if (GUILayout.Button("+ Roll", GUILayout.Height(sizeY)))
                    {
                        r_modID = i + 1;
                        r_modType = 1;
                        r_value = rollNodeNew;
                        break;
                    }
                    GUILayout.EndArea();

                    Handles.EndGUI();
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Changed node position");

                if (p_modID != -1)
                {
                    if (p_modType == 0)
                        track.NodesPosition[p_modID] = p_value;
                    else if (p_modType == 1)
                    {
                        track.InsertPositionNode(p_modID, p_value);
                    }
                    else if (p_modType == 2)
                    {
                        track.RemovePositionNode(p_modID);
                    }
                }

                if (r_modID != -1)
                {
                    if (r_modType == 0)
                        track.NodesRoll[r_modID] = r_value;
                    else if (r_modType == 1)
                        track.InsertRollNode(r_modID, r_value);
                    else if (r_modType == 2)
                        track.RemoveRollNode(r_modID);
                }

                if (length(ps - track.StartSlope) > 0.01f)
                    track.StartSlope = ps;
                if (length(pe - track.EndSlope) > 0.01f)
                    track.EndSlope = pe;

                track.UpdateSpline();

                track.DeleteTrackMesh();
            }
        }
    }
#endif
}
