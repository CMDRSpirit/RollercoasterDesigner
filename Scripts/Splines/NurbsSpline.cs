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
using kmty.NURBS;

namespace Rollercoaster
{
    public class NurbsSpline : ASpline
    {

        private Spline spline;
        private float scale;

        private float startSlope;
        private float endSlope;

        private CubicSpline fittedSpline;

        public NurbsSpline()
        {

        }

        public override void Fit(float[] x, float[] y, float startSlope = float.NaN, float endSlope = float.NaN)
        {
            List<CP> cps = new List<CP>();

            for (int i=0; i<x.Length; ++i)
            {
                cps.Add(new CP { pos = new Vector3(y[i], 0, 0), weight = 1 });
            }

            if (!float.IsNaN(startSlope))
                //cps.Insert(1, new CP { pos = new Vector3(y[0] + startSlope, 0, 0), weight = 1 });
                cps[1] = new CP { pos = new Vector3(y[0] + startSlope, 0, 0), weight = 1 };
            if (!float.IsNaN(endSlope))
                //cps.Insert(cps.Count - 2, new CP { pos = new Vector3(y[y.Length - 1] - endSlope, 0, 0), weight = 1 });
                cps[cps.Count - 2] = new CP { pos = new Vector3(y[y.Length - 1] - endSlope, 0, 0), weight = 1 };

            spline = new Spline(cps.ToArray(), min(3, cps.Count - 1), SplineType.Clamped);

            scale = x.Length - 1;

            this.startSlope = float.IsNaN(startSlope) ? (y[1] - y[0]) / (x[1] - x[0]) : startSlope;
            this.endSlope = float.IsNaN(endSlope) ? (y[y.Length - 1] - y[y.Length - 2]) / (x[x.Length - 1] - x[x.Length - 2]) : endSlope;

            //Fit
            fittedSpline = new CubicSpline();
            List<float> xs = new List<float>();
            List<float> ys = new List<float>();
            int res = (int)(scale * 4);
            for (int i=0; i< res; ++i)
            {
                float nt = i / (float)(res - 1);
                Vector3 v;
                spline.GetCurve(nt, out v);
                
                xs.Add(nt * scale);
                ys.Add(nt == 1 ? y[y.Length - 1] : v.x);
            }
            fittedSpline.Fit(xs.ToArray(), ys.ToArray(), startSlope, endSlope);
        }

        public override float Eval(float x)
        {
            return fittedSpline.Eval(x);
        }

        public override float EvalSlope(float x)
        {
            return fittedSpline.EvalSlope(x);
        }
        
    }
}
