﻿using UnityEngine;

namespace kmty.NURBS {
    [System.Serializable]
    public struct CP {
        public Vector3 pos;
        public float weight;
        public CP(Vector3 p, float w) { pos = p; weight = w; }
    }

    public enum KnotType { Uniform, OpenUniform }
    public enum SplineType { Standard, Loop, Clamped }

    public static class Shared {

        public static float[] GenKnotVectors(int order, int cpsNum, KnotType type) {
            int knotNum = cpsNum + order + 1;
            var knots = new float[knotNum];
            for (int i = 0; i < knotNum; i++) knots[i] = KnotVector(i, order, cpsNum, type);
            return knots;
        }

        public static float KnotVector(int j, int order, int cpsNum, KnotType type) {
            int knotNum = cpsNum + order + 1;
            if(type == KnotType.Uniform)     return UniformKnotVector(j, knotNum);
            if(type == KnotType.OpenUniform) return OpenUniformKnotVector(j, order, knotNum);
            throw new System.Exception();
        }

        public static float UniformKnotVector(int j, int knotNum) {
            var t0 = 0f;
            var t1 = 1f;
            return t0 + (t1 - t0) / (knotNum - 1) * j;
        }

        public static float OpenUniformKnotVector(int j, int order, int knotNum) {
            if (j <= order) return 0f;
            if (j >= knotNum - 1 - order) return 1f;
            return (float)j / (knotNum - order + 1);
        }
    }
}
