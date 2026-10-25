using System;

namespace BAM.Sim
{
    /// <summary>Float helpers on top of System.Math (the rules never touch UnityEngine).</summary>
    public static class SimMath
    {
        public const float PI = (float)Math.PI;
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Hypot(float x, float y) => (float)Math.Sqrt(x * x + y * y);
        public static float Cos(float a) => (float)Math.Cos(a);
        public static float Sin(float a) => (float)Math.Sin(a);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static float Dist(Unit a, Unit b) => Hypot(a.X - b.X, a.Y - b.Y);
        public static float Dist(Unit a, float x, float y) => Hypot(a.X - x, a.Y - y);

        /// <summary>Shortest distance from point p to segment a-b.</summary>
        public static float PtSeg(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay, l2 = vx * vx + vy * vy, k = l2 < 1e-8f ? 0 : Clamp(((px - ax) * vx + (py - ay) * vy) / l2, 0, 1);
            return Hypot(px - ax - vx * k, py - ay - vy * k);
        }

        /// <summary>Angle difference folded into -PI..PI.</summary>
        public static float AngDiff(float a, float b)
        {
            float d = a - b;
            while (d > PI) d -= 2 * PI;
            while (d < -PI) d += 2 * PI;
            return d;
        }
    }

    /// <summary>
    /// The match's random numbers (SplitMix64). Our own generator, not System.Random, so a seed gives the same match in the
    /// headless runner (.NET 8) and on the phone (IL2CPP). Only the rules draw from it; pictures and sounds use their own.
    /// </summary>
    public sealed class Rng
    {
        ulong s;
        public Rng(int seed) { s = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL; }
        public ulong Next()
        {
            ulong z = s += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
        /// <summary>0 (inclusive) to 1 (exclusive).</summary>
        public float Float() => (Next() >> 40) * (1f / (1 << 24));
        public float Range(float a, float b) => a + Float() * (b - a);
        public int Int(int n) => n <= 1 ? 0 : (int)((Next() >> 33) % (ulong)n);
    }
}
