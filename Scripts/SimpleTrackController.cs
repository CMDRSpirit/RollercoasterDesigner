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
using System.Linq;

namespace Rollercoaster
{
    public class SimpleTrackController : ATrackController
    {

        public float WaitTimeAtStationSeconds = 10;

        private HashSet<BlockSection> stationSectionsWaiting;

        private void Awake()
        {
            stationSectionsWaiting = new HashSet<BlockSection>();
        }

        public new void Start()
        {
            base.Start();

            StationSections[0].StopTrain = false;
        }

        private void activateSection(BlockSection sec)
        {
            if (stationSectionsWaiting != null && stationSectionsWaiting.Contains(sec))
                return;

            foreach (var s in sec.sections)
            {
                s.PhysicsActive = true;
                s.StopTrain = false;
            }
        }

        public override void OnTrainEnterSection(BlockSection section)
        {
            base.OnTrainEnterSection(section);

            activateSection(section);

            //Update previous
            if(section.Prev != null)
                activateSection(section.Prev.Prev);
        }

        public override void OnEventTrigger(SensorEvent e)
        {
            float f;
            BlockSection s = GetBlockSection(track.GetSection(e.t, out f));
            if (e.eventType == SensorEventType.BLOCK_CHECK)
            {
                bool isStation = StationSections.Contains(s.getLast());

                s.getLast().StopTrain = !s.Next.free || isStation;
                s.getLast().PhysicsActive = true;

                if (isStation)
                {
                    StartCoroutine(WaitStation(s));
                }
            }
        }

        private IEnumerator WaitStation(BlockSection section)
        {
            stationSectionsWaiting.Add(section);

            yield return new WaitForSeconds(WaitTimeAtStationSeconds);

            section.getLast().StopTrain = !section.Next.free;
            stationSectionsWaiting.Remove(section);
        }
    }
}
