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
    public class TrackSupportPart : MonoBehaviour
    {

        public float Angle = 0;
        public Vector3 LocalOffset;
        public Vector3 OffsetGlobal;

        private void OnValidate()
        {
            OrientPart();
        }

        public void OrientPart()
        {
            this.transform.localPosition = LocalOffset;
            this.transform.position += OffsetGlobal;

            if (this.transform.position.y < 0)
                return;

            Quaternion q = this.transform.rotation;

            float3 v = float3(sin(radians(Angle)), cos(radians(Angle)), 0);
            float distGround = this.transform.position.y / abs(v.y);

            float3 f = mul(q, float3(0, 0, 1)) * float3(1,0,1);
            this.transform.rotation = Quaternion.LookRotation(f, Vector3.up) * Quaternion.AngleAxis(Angle, Vector3.forward);
            this.transform.localScale = new float3(1, distGround, 1);
        }

    }

}