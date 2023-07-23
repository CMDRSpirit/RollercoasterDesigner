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
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace Rollercoaster
{

    public class CoasterTrain : MonoBehaviour
    {

        public CoasterTrack Track;

        public bool EnablePhysics;
        public float RollCoefficient;
        public float CrossArea;


        public TrainAxis[] AxisDefinitions;


        public float velocity;
        public float t_global;

        private ATrackController trackController;

        // Use this for initialization
        void Start()
        {
            trackController = Track.GetComponent<ATrackController>();
        }

        private void OnValidate()
        {
            if (!Track)
                Track = this.GetComponentInParent<CoasterTrack>();

            AxisDefinitions = this.GetComponentsInChildren<TrainAxis>();

            PlaceTrainOnTrack();            
        }
        
        private void UpdatePhysics(float deltaT)
        {
            //Physics
            float totalMass = 0;
            float force = 0;
            foreach (TrainAxis axis in AxisDefinitions)
            {
                const float carMass = 550;
                totalMass += carMass;

                float3 forward = axis.transform.forward;
                float fdotUp = -dot(forward, new float3(0, 1, 0));

                float acc = fdotUp * 9.81f;
                force += acc * carMass;

                //roll resistance
                float NForce = dot(axis.transform.up, new float3(0, 1, 0)) * 9.81f;
                force += -sign(velocity) * this.RollCoefficient * NForce * carMass;

                //Drag
                const float rho_air = 1.293e-3f; //Density
                const float c_d = 0.6f;//Drag coefficient
                float v2 = velocity * velocity;
                force += -sign(velocity) * 0.5f * rho_air * v2 * c_d * CrossArea;
            }
            velocity += force * deltaT / totalMass;

            //Section
            float t_local;
            var sec = Track.GetSection(t_global, out t_local);
            sec.AffectTrain(this, deltaT);
        }
        
        public void PlaceTrainOnTrack()
        {
            if (!Track)
                return;

            if (Track.TMax != 0)
                t_global = (t_global + Track.TMax) % Track.TMax;
            else
                t_global = 0;

            float t_local;
            var sec = Track.GetSection(t_global, out t_local);
            if (!sec)
                return;

            float3 pos;
            quaternion rot;
            sec.EvaluateSpline(t_local, out pos, out rot);

            this.transform.position = sec.transform.TransformPoint(pos);
            this.transform.rotation = sec.transform.rotation * rot;

            float curT = t_global;
            for (int i = 0; i < AxisDefinitions.Length; ++i)
            {
                TrainAxis axis = AxisDefinitions[i];

                //float3 der = sec.EvaluateDerivative(t_local);
                //curT -= axis.OffsetFromPrev / length(der);
                curT += Track.DeltaMToDeltaTNegative(curT, -axis.OffsetFromPrev);
                curT = (curT + Track.TMax) % Track.TMax;

                sec = Track.GetSection(curT, out t_local);
                if (!sec)
                    return;
                sec.EvaluateSpline(t_local, out pos, out rot);

                axis.transform.position = sec.transform.TransformPoint(pos);
                axis.transform.rotation = sec.transform.rotation * rot;

                if (axis.GimbalAxis && axis.GimbalTarget)
                    axis.GimbalAxis.rotation = Quaternion.LookRotation(normalize(axis.GimbalTarget.position - axis.GimbalAxis.position), axis.transform.up);

                axis.curTGlobal = curT;
                AxisDefinitions[i] = axis;
            }
        }
        
        private void Update()
        {
            if (Track)
            {
                t_global = (t_global + Track.TMax) % Track.TMax;

                if (EnablePhysics)
                {
                    UpdatePhysics(Time.deltaTime);

                    float t_local;
                    var sec = Track.GetSection(t_global, out t_local);
                    float3 der = sec.EvaluateDerivative(t_local);

                    float tOld = t_global;
                    t_global += velocity * Time.deltaTime / length(der);
                    trackController?.TriggerEvents(tOld, t_global);
                    //
                }

                PlaceTrainOnTrack();
            }
        }
    }
}