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
    public class TrainAudioManager : MonoBehaviour
    {

        // #### Train Audio ####
        [System.Serializable]
        public struct CarAudio
        {
            public AudioClip clip;
            [Range(0, 1)]
            public float baseVolume;
            public float baseVelocity;
            public float sigma;
        }
        public CarAudio[] carAudio;
        public float2 CarVelocityInterpolation;
        // ####

        private CoasterTrain train;
        

        private TrackSection prevSec;
        private List<GameObject> sectionAudioEffects;

        private void Start()
        {
            train = this.GetComponentInParent<CoasterTrain>();

            createCarSources();
        }

        private void createCarSources()
        {
            int i = 0;
            foreach(var ca in carAudio)
            {
                GameObject o = new GameObject("car_audio_" + i);
                o.transform.parent = this.transform;
                o.transform.localPosition = Vector3.zero;
                o.transform.localRotation = Quaternion.identity;

                var audioSource = o.AddComponent<AudioSource>();
                audioSource.clip = ca.clip;
                audioSource.loop = true;
                audioSource.spatialBlend = 1.0f;
                audioSource.minDistance = 5;
                audioSource.Play();

                ++i;
            }
        }

        private void createTrackSources(TrackAudio audio)
        {
            if(sectionAudioEffects != null)
            {
                foreach (var a in sectionAudioEffects)
                    GameObject.Destroy(a);
            }
            if (!audio)
                return;

            sectionAudioEffects = new List<GameObject>();

            int i = 0;
            foreach(var aud in audio.OnTrainAudioEffects)
            {
                GameObject o = new GameObject("track_car_audio_" + i);
                o.transform.parent = this.transform;
                o.transform.localPosition = Vector3.zero;
                o.transform.localRotation = Quaternion.identity;

                var audioSource = o.AddComponent<AudioSource>();
                audioSource.clip = aud.clip;
                audioSource.loop = true;
                audioSource.spatialBlend = 1.0f;
                audioSource.minDistance = 2;
                audioSource.Play();

                sectionAudioEffects.Add(o);

                ++i;
            }
        }

        private float calculateVolume(CarAudio audio)
        {
            float v = abs(train.velocity) - audio.baseVelocity;
            float fv = exp(-v * v / (audio.sigma * audio.sigma));

            float total = 0;
            foreach (var ca in carAudio)
            {
                v = abs(train.velocity) - ca.baseVelocity;
                total += exp(-v * v / (ca.sigma * ca.sigma));
            }

            return fv / total * audio.baseVolume;
        }

        private float linstep(float a, float b, float x)
        {
            if (abs(a - b) == 0)
                return 1;
            return clamp((x - a) / (b - a), 0, 1);
        }

        private void Update()
        {
            float t_local;
            var sec = train.Track.GetSection(train.t_global, out t_local);
            var aud = sec.GetComponent<TrackAudio>();
            //var paud = prevSec?.GetComponent<TrackAudio>();
            if (prevSec != sec)
            {
                createTrackSources(aud);

                prevSec = sec;
            }


            //Car audio
            float volumeModifier = linstep(CarVelocityInterpolation.x, CarVelocityInterpolation.y, abs(train.velocity));
            int i = 0;
            foreach (var ca in carAudio)
            {
                GameObject o = this.transform.Find("car_audio_" + i).gameObject;
                var audioSource = o.GetComponent<AudioSource>();

                audioSource.volume = calculateVolume(ca) * volumeModifier;

                ++i;
            }

            //Track car audio
            if (aud)
            {
                i = 0;

                volumeModifier = smoothstep(0, 0.5f, t_local) * (1 - smoothstep(sec.getTMax() - 0.5f, sec.getTMax(), t_local));
                foreach (var e in aud.OnTrainAudioEffects)
                {
                    GameObject o = this.transform.Find("track_car_audio_" + i).gameObject;
                    var audioSource = o.GetComponent<AudioSource>();

                    audioSource.volume = e.baseVolume * linstep(e.velocityInterpolation.x, e.velocityInterpolation.y, abs(train.velocity)) * volumeModifier;

                    ++i;
                }
            }
        }
    }
}