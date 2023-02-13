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
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rollercoaster
{
    [RequireComponent(typeof(CoasterTrack))]
    public abstract class ATrackController : MonoBehaviour
    {

        [System.Serializable]
        public class BlockSection
        {
            public TrackSection[] sections;
            public bool free;

            public TrackSection getLast()
            {
                return sections[sections.Length - 1];
            }
            public TrackSection getFirst()
            {
                return sections[0];
            }

            public BlockSection Next;
            public BlockSection Prev;
        };

        public List<BlockSection> BlockSections;

        public CoasterTrain[] Trains;

        public CoasterTrack track;
        public TrackSection[] StationSections;

        //Events
        public enum SensorEventType
        {
            TRAIN_ENTER, BLOCK_CHECK, UNITY_EVENT
        }
        [System.Serializable]
        public struct SensorEvent
        {
            public float t;
            public SensorEventType eventType;
            public UnityEvent uEvent;
        }
        public List<SensorEvent> sensorEvents;

        public void Start()
        {
            InitCoaster();
        }

        public void OnValidate()
        {
            InitCoaster();
        }

        private void InitCoaster()
        {
            track = this.GetComponent<CoasterTrack>();
            Trains = this.GetComponentsInChildren<CoasterTrain>();

            if (BlockSections == null || BlockSections.Count == 0)
                return;

            BlockSection s = BlockSections[0];
            s.free = true;
            for (int i=1; i<BlockSections.Count; ++i)
            {
                BlockSection s1 = BlockSections[i];
                s1.free = true;
                s1.Prev = s;
                s.Next = s1;
                if(i == BlockSections.Count - 1)
                {
                    s = BlockSections[0];
                    s1.Next = s;
                    s.Prev = s1;
                }
                s = s1;
            }

            s = BlockSections[0];
            int j = BlockSections.Count;
            foreach (var t in Trains)
            {
                var endSec = s.getLast();

                t.t_global = endSec.getTMax() + track.GetTStart(endSec) - 0.05f;
                t.PlaceTrainOnTrack();

                OnTrainEnterSection(s);

                endSec.StopTrain = true;

                s = BlockSections[--j];
            }
        }

        public BlockSection GetBlockSection(TrackSection section)
        {
            foreach(var b in BlockSections)
            {
                foreach(var s in b.sections)
                {
                    if (s == section)
                        return b;
                }
            }

            return null;
        }

        public virtual void OnTrainEnterSection(BlockSection section)
        {
            section.free = false;
            if(section.Prev != null)
                section.Prev.free = true;
        }

        public abstract void OnEventTrigger(SensorEvent e);

        //
        public void TriggerEvents(float t0, float t1)
        {
            if(sensorEvents != null)
            {
                foreach(var e in sensorEvents)
                {
                    if(t0 < e.t && t1 > e.t)
                    {
                        float f;
                        if (e.eventType == SensorEventType.TRAIN_ENTER)
                            OnTrainEnterSection(GetBlockSection(track.GetSection(e.t, out f)));
                        else
                            OnEventTrigger(e);

                        e.uEvent?.Invoke();
                    }
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (sensorEvents != null)
            {
                Gizmos.color = Color.yellow;

                foreach (var e in sensorEvents)
                {
                    float t;
                    var sec = track.GetSection(e.t, out t);

                    if(sec)
                        Gizmos.DrawWireSphere(sec.transform.TransformPoint(sec.EvaluatePosition(t)), sec.TrackDesc.TrackWidth * 0.75f);
                }
            }
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ATrackController), true)]
    public class TrackControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            ATrackController con = (ATrackController)target;

            if(GUILayout.Button("Auto place sensor events"))
            {
                if (con.sensorEvents == null)
                    con.sensorEvents = new List<ATrackController.SensorEvent>();

                foreach(var bs in con.BlockSections)
                {
                    var entry = new ATrackController.SensorEvent();
                    entry.eventType = ATrackController.SensorEventType.TRAIN_ENTER;
                    entry.t = con.track.GetTStart(bs.getFirst()) + 0.25f;

                    con.sensorEvents.Add(entry);

                    entry = new ATrackController.SensorEvent();
                    entry.eventType = ATrackController.SensorEventType.BLOCK_CHECK;
                    entry.t = con.track.GetTStart(bs.getLast()) + bs.getLast().getTMax() - 0.25f;

                    con.sensorEvents.Add(entry);
                }
            }
        }

        private void OnSceneGUI()
        {
            ATrackController con = (ATrackController) target;
            
            if (con.sensorEvents == null)
                return;

            EditorGUI.BeginChangeCheck();

            Handles.color = Color.yellow;

            int eventID = -1;
            float eventT = 0; 
            for(int i=0; i<con.sensorEvents.Count; ++i){
                var e = con.sensorEvents[i];

                float t_local;
                var sec = con.track.GetSection(e.t, out t_local);

                float3 p = sec.EvaluatePosition(t_local);
                float3 der = sec.EvaluateDerivative(t_local);

                float3 forward = sec.transform.TransformDirection(normalize(der));
                p = sec.transform.TransformPoint(p);

                float3 p1 = Handles.Slider(p, forward);
                float dt = dot(p1 - p, forward);
                if (abs(dt) > 0.01f)
                {
                    eventID = i;
                    eventT = (e.t + dt / length(der) + con.track.TMax) % con.track.TMax;
                    break;
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Changed event position");

                if (eventID != -1)
                {
                    var e = con.sensorEvents[eventID];
                    e.t = eventT;
                    con.sensorEvents[eventID] = e;
                }
            }
        }
    }
#endif
}
