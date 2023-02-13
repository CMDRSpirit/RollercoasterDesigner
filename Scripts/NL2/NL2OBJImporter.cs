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

using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rollercoaster
{

    [RequireComponent(typeof(CoasterTrack))]
    public class NL2OBJImporter : MonoBehaviour
    {
        private const int BLOCKSIZE = 8;


        public string FilePath;
        public int SkipVertices = 25;
        public float Scale = 1f;

        private float3 readVertex(string line)
        {
            string[] s = line.Split(" ");

            return float3(-float.Parse(s[1], System.Globalization.NumberStyles.AllowDecimalPoint | System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(s[2], System.Globalization.NumberStyles.AllowDecimalPoint | System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(s[3], System.Globalization.NumberStyles.AllowDecimalPoint | System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture));

        }

        private float3[] readVertexBlock(string[] lines, int idx)
        {
            float3[] block = new float3[BLOCKSIZE];

            for(int i=0; i< BLOCKSIZE; ++i)
            {
                block[i] = readVertex(lines[i + idx]) * Scale;
            }

            return block;
        }

        public void LoadOBJ()
        {
            FileInfo fileinfo = new FileInfo(FilePath);

            if (!fileinfo.Exists)
            {
                Debug.LogError("[NL2CSVImporter] File not found!!!");
                return;
            }

            CoasterTrack track = this.GetComponent<CoasterTrack>();
            List<float3> nodes_pos = new List<float3>();
            List<float3> nodes_up = new List<float3>();

            string[] lines = File.ReadAllLines(FilePath);

            //Position
            for (int i = 4; i < lines.Length; i+= BLOCKSIZE * SkipVertices) //Jump first four lines, 
            {
                string line = lines[i];
                if (!line.StartsWith("v "))
                    break;

                float3[] vertices = readVertexBlock(lines, i);

                float3 center = 0;
                for (int j = 0; j < vertices.Length; ++j)
                    center += vertices[j];
                center /= vertices.Length;

                nodes_pos.Add(center);

                //up vector
                float3 up = normalize(vertices[0] - vertices[1]);
                nodes_up.Add(up);
            }

            var section = track.TrackSections[0];
            section.StartSlope = 0;
            section.EndSlope = 0;
            section.NodesPosition = nodes_pos;
            section.NodesRoll = new List<float2>(new float2[] { float2(0, 0), float2(1, 0) });
            section.UpdateSpline();

            //Roll
            List<float2> nodes_roll = new List<float2>();
            float3 prevUp = float3(0, 1, 0);
            float prevRoll = 0;
            for(int i = 0; i < nodes_pos.Count; ++i)
            {
                float2 roll = float2(i, 0);

                float3 up = nodes_up[i];

                float3 baseF = normalize(section.EvaluateDerivative(i));
                float3 baseR = normalize(section.EvaluateOrthogonal(i, TrackSection.RollType.ANGLE));
                float3 baseU = section.EvaluateUp(baseF, baseR);

                Quaternion rot = Quaternion.Inverse(Quaternion.LookRotation(baseF, baseU));
                up = rot * up;

                float angle = acos(clamp(dot(float3(0, 1, 0), up), -1, 1)) * sign(up.x);
                roll.y = degrees(angle);

                nodes_roll.Add(roll);

                prevUp = up;
                prevRoll = roll.y;
            }
            section.NodesRoll = nodes_roll;
            section.UpdateSpline();
        }

    }

#if UNITY_EDITOR
    [CustomEditor(typeof(NL2OBJImporter), true)]
    public class NL2CSVImporterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            NL2OBJImporter importer = (NL2OBJImporter)target;

            if(GUILayout.Button("Load OBJ"))
            {
                importer.LoadOBJ();
            }
        }
    }
#endif
}
