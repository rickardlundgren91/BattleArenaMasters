using System;

namespace BAM.Sim
{
    /// <summary>Float helpers on top of System.Math (the rules never touch UnityEngine).</summary>
    /// <remarks>Sin, Cos, Atan2, Exp and Pow are our own, made of + - * / and Sqrt only (each exact in IEEE 754), not the phone's
    /// math library: online the phones each run the same match from the same taps (lockstep), and two Android versions' libm
    /// may round a sine differently in the last bit, which would slowly pull the two matches apart.</remarks>
    public static class SimMath
    {
        public const float PI = (float)Math.PI;
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Hypot(float x, float y) => (float)Math.Sqrt(x * x + y * y);
        public static float Cos(float a) => (float)DSin(a + HalfPiD);
        public static float Sin(float a) => (float)DSin(a);
        public static float Atan2(float y, float x) => (float)DAtan2(y, x);
        public static float Exp(float v) => (float)DExp(v);
        /// <summary>b to the power e, for b >= 0.</summary>
        public static float Pow(float b, float e) => b <= 0 ? (e > 0 ? 0f : 1f) : (float)DExp(e * DLog(b));

        const double PiD = Math.PI, HalfPiD = Math.PI / 2, TwoPiD = Math.PI * 2, Ln2D = 0.69314718055994530942;

        static double DSin(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return double.NaN;
            x -= Math.Floor(x / TwoPiD + 0.5) * TwoPiD; // -pi..pi
            if (x > HalfPiD) x = PiD - x; else if (x < -HalfPiD) x = -PiD - x; // -pi/2..pi/2
            double x2 = x * x; // Taylor to x^19: the error is below 1e-13 on this range
            return x * (1 + x2 * (-1 / 6.0 + x2 * (1 / 120.0 + x2 * (-1 / 5040.0 + x2 * (1 / 362880.0 + x2 * (-1 / 39916800.0
                + x2 * (1 / 6227020800.0 + x2 * (-1 / 1307674368000.0 + x2 * (1 / 355687428096000.0 + x2 * (-1 / 121645100408832000.0))))))))));
        }

        static double DAtan(double x) // any x
        {
            if (double.IsNaN(x)) return x;
            bool neg = x < 0; if (neg) x = -x;
            bool inv = x > 1; if (inv) x = 1 / x;
            // halve the angle twice: atan(x) = 2 atan(x / (1 + sqrt(1 + x^2))), so |x| <= tan(pi/16) ~ 0.2
            x = x / (1 + Math.Sqrt(1 + x * x));
            x = x / (1 + Math.Sqrt(1 + x * x));
            double x2 = x * x, r = 0, p = x;
            for (int k = 0; k < 12; k++) { r += (k % 2 == 0 ? p : -p) / (2 * k + 1); p *= x2; }
            r *= 4;
            if (inv) r = HalfPiD - r;
            return neg ? -r : r;
        }

        static double DAtan2(double y, double x)
        {
            if (double.IsNaN(x) || double.IsNaN(y)) return double.NaN;
            if (x == 0 && y == 0) return 0;
            if (Math.Abs(y) <= Math.Abs(x))
            {
                double a = DAtan(y / x);
                return x > 0 ? a : y >= 0 ? a + PiD : a - PiD;
            }
            return (y > 0 ? HalfPiD : -HalfPiD) - DAtan(x / y);
        }

        static double DExp(double x)
        {
            if (double.IsNaN(x)) return x;
            if (x > 700) return double.PositiveInfinity;
            if (x < -700) return 0;
            double k = Math.Floor(x / Ln2D + 0.5), r = x - k * Ln2D; // |r| <= ln2/2
            double t = 1, sum = 1;
            for (int i = 1; i <= 16; i++) { t *= r / i; sum += t; }
            return sum * BitConverter.Int64BitsToDouble((long)(k + 1023) << 52); // * 2^k, exact
        }

        static double DLog(double x) // x > 0
        {
            long bits = BitConverter.DoubleToInt64Bits(x);
            int e = (int)((bits >> 52) & 0x7FF);
            if (e == 0) { x *= 4503599627370496.0; bits = BitConverter.DoubleToInt64Bits(x); e = (int)((bits >> 52) & 0x7FF) - 52; } // subnormal
            int exp = e - 1023;
            double m = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L); // 1..2
            if (m > 1.4142135623730951) { m *= 0.5; exp++; }
            double s = (m - 1) / (m + 1), s2 = s * s, p = s, r = 0; // log m = 2 atanh s, |s| <= 0.172
            for (int k = 0; k < 14; k++) { r += p / (2 * k + 1); p *= s2; }
            return 2 * r + exp * Ln2D;
        }
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
        /// <summary>For Match.Checksum.</summary>
        public ulong State => s;
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
