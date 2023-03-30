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
    public class TrackSupportGenerator : MonoBehaviour
    {

        public GameObject SupportPrefab;

        public float[] SupportPositions;

        public void AutoPlaceSupports(float distance, float offset = 0)
        {
            var section = this.GetComponent<TrackSection>();

            float secLength = section.SplineLength - offset;
            int iterations = (int)(secLength / distance);

            float delta = (iterations * distance) / secLength;
            float t = section.DeltaMToDeltaT(0, offset + (delta + distance) * 0.5f);

            SupportPositions = new float[iterations];
            for (int i = 0; i < iterations; ++i)
            {
                SupportPositions[i] = t;

                t += section.DeltaMToDeltaT(t, distance);
            }
        }

        public void GenerateSupports()
        {
            DeleteSupports();

            GameObject supportParent = new GameObject("Supports");
            supportParent.transform.parent = this.transform;
            supportParent.transform.localPosition = Vector3.zero;
            supportParent.transform.localRotation = Quaternion.identity;

            var section = this.GetComponent<TrackSection>();
            foreach(float t in SupportPositions)
            {
                float3 p;
                quaternion r;
                section.EvaluateSpline(t, out p, out r);

                //Grad
                float eps = 1e-3f;
                float3 p0, p1;
                quaternion r1;
                section.EvaluateSpline(t - eps, out p0, out r1);
                section.EvaluateSpline(t + eps, out p1, out r1);
                float3 fw = normalize(p1 - p0);
                r = Quaternion.LookRotation(fw, mul(r, Vector3.up));
                //

                GameObject instance = GameObject.Instantiate(SupportPrefab, supportParent.transform);
                instance.transform.localPosition = p;
                instance.transform.localRotation = r;

                float rollAngle = section.EvaluateRoll(t);
                if (rollAngle == 0)
                    rollAngle = 1;
                foreach (var supPart in instance.GetComponentsInChildren<TrackSupportPart>())
                {
                    supPart.Angle *= -sign(rollAngle);
                    supPart.OrientPart();
                }
            }
        }

        public void DeleteSupports()
        {
            Transform supportParent = this.transform.Find("Supports");
            if (supportParent == null)
                return;

            GameObject.DestroyImmediate(supportParent.gameObject);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;

            var section = this.GetComponent<TrackSection>();
            Gizmos.matrix = section.transform.localToWorldMatrix;

            foreach (float t in SupportPositions)
            {
                float3 p;
                quaternion r;
                section.EvaluateSpline(t, out p, out r);

                Gizmos.DrawLine(p, p - mul(r, float3(0, 2 * section.TrackDesc.TrackWidth, 0)));
            }
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(TrackSupportGenerator))]
    public class TrackSupportGeneratorEditor : Editor
    {
        private static float supDist = 10;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            TrackSupportGenerator gen = (TrackSupportGenerator)target;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Support Distance: ");
            supDist = EditorGUILayout.FloatField(supDist);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Auto Place Supports"))
                gen.AutoPlaceSupports(supDist);
            if (GUILayout.Button("Generate Supports"))
                gen.GenerateSupports();
            if (GUILayout.Button("Delete Supports"))
                gen.DeleteSupports();
        }
    }
#endif
}