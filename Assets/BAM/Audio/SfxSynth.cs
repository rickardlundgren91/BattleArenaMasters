using System;

namespace BAM.Audio
{
    enum Wave : byte { Sine, Tri, Square, Saw }

    enum Filt : byte { Low, Band, High }

    /// <summary>
    /// A tiny offline synth: layers are mixed into one scratch buffer, then <see cref="Finish"/> cleans, levels and trims it.
    /// Plain System code (no UnityEngine) so the same recipes can be rendered to WAV outside Unity for checking.
    /// 22050 Hz is enough: nothing we want is above about 8 kHz, and it halves the build time and memory.
    /// </summary>
    sealed class SfxSynth
    {
        public const int Rate = 22050;
        const double TwoPi = Math.PI * 2;

        float[] buf = new float[Rate * 2];
        float[] gain = new float[Rate * 2];
        int used;
        uint rng = 1;

        public void Begin(int seed)
        {
            Array.Clear(buf, 0, used);
            used = 0;
            rng = (uint)seed * 2654435761u + 0x9E3779B9u;
            if (rng == 0) rng = 1;
        }

        /// <summary>Deterministic per clip, so the same recipe always renders the same sound.</summary>
        public float Rand()
        {
            rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
            return (rng & 0xFFFFFF) / 16777216f;
        }

        public float Range(float a, float b) => a + (b - a) * Rand();

        int Room(float at, float dur, out int s0)
        {
            s0 = Math.Max(0, (int)(at * Rate));
            int n = (int)(dur * Rate);
            int need = s0 + n;
            if (need > buf.Length)
            {
                int size = Math.Max(need, buf.Length * 2);
                Array.Resize(ref buf, size);
                Array.Resize(ref gain, size);
            }
            if (need > used) used = need;
            return n;
        }

        // Envelope: linear attack, optional flat hold, then exponential decay to -60 dB at the end of the layer.
        struct Env
        {
            int na, nh; float e, k;
            public Env(int n, float attack, float hold)
            {
                na = Math.Max(1, (int)(attack * Rate));
                nh = Math.Max(0, (int)(hold * Rate));
                int nd = Math.Max(1, n - na - nh);
                e = 1f; k = (float)Math.Pow(0.001, 1.0 / nd);
            }
            public float Next(int i)
            {
                if (i < na) return (float)i / na;
                if (i < na + nh) return 1f;
                e *= k; return e;
            }
        }

        // PolyBLEP smooths the jumps of square and saw so they do not alias into a harsh fizz at 22 kHz.
        static float Blep(double t, double dt)
        {
            if (t < dt) { t /= dt; return (float)(t + t - t * t - 1); }
            if (t > 1 - dt) { t = (t - 1) / dt; return (float)(t * t + t + t + 1); }
            return 0f;
        }

        static float Osc(Wave w, double ph, double dt)
        {
            switch (w)
            {
                case Wave.Tri: return (float)(ph < 0.5 ? 4 * ph - 1 : 3 - 4 * ph);
                case Wave.Square:
                {
                    double p2 = ph + 0.5; if (p2 >= 1) p2 -= 1;
                    return (ph < 0.5 ? 1f : -1f) + Blep(ph, dt) - Blep(p2, dt);
                }
                case Wave.Saw: return (float)(2 * ph - 1) - Blep(ph, dt);
                default: return (float)Math.Sin(TwoPi * ph);
            }
        }

        /// <summary>One oscillator whose pitch glides exponentially from f0 to f1 over dur.</summary>
        public void Tone(Wave w, float f0, float f1, float dur, float vol, float at = 0, float attack = 0.004f, float hold = 0,
            float vibHz = 0, float vibDepth = 0, float tremHz = 0, float tremDepth = 0)
        {
            int n = Room(at, dur, out int s0);
            if (n <= 0) return;
            var env = new Env(n, attack, hold);
            double ph = 0, f = f0, fk = Math.Pow(f1 / f0, 1.0 / n);
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Rate, fe = f;
                if (vibDepth > 0) fe *= 1 + vibDepth * Math.Sin(TwoPi * vibHz * t);
                double dt = fe / Rate;
                float a = env.Next(i) * vol;
                if (tremDepth > 0) a *= 1 - tremDepth * (0.5f + 0.5f * (float)Math.Sin(TwoPi * tremHz * t));
                buf[s0 + i] += Osc(w, ph, dt) * a;
                ph += dt; if (ph >= 1) ph -= 1;
                f *= fk;
            }
        }

        /// <summary>Two-operator FM: a sine carrier bent by a sine at ratio * f. Inharmonic ratios make metal and bells.</summary>
        public void Fm(float f0, float f1, float ratio, float index, float dur, float vol, float at = 0, float attack = 0.002f, float hold = 0)
        {
            int n = Room(at, dur, out int s0);
            if (n <= 0) return;
            var env = new Env(n, attack, hold);
            double pc = 0, pm = 0, f = f0, fk = Math.Pow(f1 / f0, 1.0 / n);
            for (int i = 0; i < n; i++)
            {
                float e = env.Next(i);
                double mod = index * e * Math.Sin(TwoPi * pm);
                buf[s0 + i] += (float)Math.Sin(TwoPi * pc + mod) * e * vol;
                pc += f / Rate; if (pc >= 1) pc -= 1;
                pm += f * ratio / Rate; if (pm >= 1) pm -= 1;
                f *= fk;
            }
        }

        // Zavalishin's TPT state-variable filter: stable at any cutoff we sweep to, and one structure gives low, band and high pass.
        struct Svf
        {
            float g, k, a1, a2, a3, ic1, ic2;
            public void Set(float fc, float q)
            {
                fc = Math.Min(Math.Max(fc, 20f), Rate * 0.45f);
                g = (float)Math.Tan(Math.PI * fc / Rate); k = 1f / Math.Max(0.1f, q);
                a1 = 1f / (1f + g * (g + k)); a2 = g * a1; a3 = g * a2;
            }
            public float Run(Filt type, float x)
            {
                float v3 = x - ic2, v1 = a1 * ic1 + a2 * v3, v2 = ic2 + a2 * ic1 + a3 * v3;
                ic1 = 2 * v1 - ic1; ic2 = 2 * v2 - ic2;
                switch (type)
                {
                    case Filt.Band: return v1 * k; // unity gain at the centre
                    case Filt.High: return x - k * v1 - v2;
                    default: return v2;
                }
            }
        }

        /// <summary>White noise through a filter whose cutoff glides from f0 to f1; tremolo makes spins and flutters.</summary>
        public void Noise(Filt type, float f0, float f1, float q, float dur, float vol, float at = 0, float attack = 0.003f, float hold = 0,
            float tremHz = 0, float tremDepth = 0)
        {
            int n = Room(at, dur, out int s0);
            if (n <= 0) return;
            var env = new Env(n, attack, hold);
            var svf = new Svf();
            double f = f0, fk = Math.Pow(f1 / f0, 1.0 / n);
            for (int i = 0; i < n; i++)
            {
                if ((i & 15) == 0) svf.Set((float)f, q);
                float a = env.Next(i) * vol;
                if (tremDepth > 0) a *= 1 - tremDepth * (0.5f + 0.5f * (float)Math.Sin(TwoPi * tremHz * i / Rate));
                buf[s0 + i] += svf.Run(type, Rand() * 2 - 1) * a;
                f *= fk;
            }
        }

        /// <summary>Notes one after another (step apart), each held a little before it rings out.</summary>
        public void Notes(Wave w, float[] fs, float step, float dur, float vol, float at = 0)
        {
            for (int i = 0; i < fs.Length; i++) Tone(w, fs[i], fs[i], dur, vol, at + i * step, 0.006f, dur * 0.3f);
        }

        /// <summary>Bubble pops: short sines whose pitch jumps up (the old game's recipe).</summary>
        public void Bubbles(int count, float span, float fLo, float fHi, float vol, float at = 0)
        {
            for (int i = 0; i < count; i++)
            {
                float t = at + Rand() * span, f = Range(fLo, fHi), d = Range(0.02f, 0.045f);
                Tone(Wave.Sine, f, f * 2.4f, d, vol * Range(0.5f, 1f), t, 0.003f);
            }
        }

        /// <summary>Random tiny bursts of band-passed noise: fire crackle, rustle, gravel.</summary>
        public void Crackle(float dur, float perSecond, float fLo, float fHi, float vol, float at = 0)
        {
            int count = (int)(dur * perSecond);
            for (int i = 0; i < count; i++)
            {
                float t = at + Rand() * dur, fade = 1f - (t - at) / dur;
                Noise(Filt.Band, Range(fLo, fHi), Range(fLo, fHi), 2.5f, Range(0.004f, 0.014f), vol * Range(0.4f, 1f) * (0.4f + 0.6f * fade), t, 0.0005f);
            }
        }

        /// <summary>Little metal clinks (a chain, coins): short inharmonic FM pings at random times.</summary>
        public void Clinks(int count, float span, float fLo, float fHi, float vol, float at = 0)
        {
            for (int i = 0; i < count; i++)
            {
                float t = at + Rand() * span, f = Range(fLo, fHi);
                Fm(f, f * 0.97f, 1.41f, 1.6f, Range(0.03f, 0.06f), vol * Range(0.5f, 1f), t, 0.001f);
            }
        }

        /// <summary>
        /// Cleans the mix and returns the finished samples: high pass (phone speakers cannot play deep bass, it only eats
        /// headroom), low pass (nothing harsh above about 8 kHz), level to a common loudness times <paramref name="level"/>,
        /// a look-ahead limiter so no sample passes the ceiling, then trim the silent tail.
        /// </summary>
        public float[] Finish(float level)
        {
            int n = used;
            if (n <= 0) return new float[64];
            Svf hp = new Svf(), lp1 = new Svf(), lp2 = new Svf(), phone = new Svf();
            hp.Set(130f, 0.707f); phone.Set(350f, 0.707f); lp1.Set(7200f, 0.54f); lp2.Set(7200f, 1.31f); // two 2-pole stages with Butterworth Qs = a 4-pole low pass
            float peak = 0;
            for (int i = 0; i < n; i++)
            {
                float x = lp2.Run(Filt.Low, lp1.Run(Filt.Low, hp.Run(Filt.High, buf[i])));
                buf[i] = x;
                gain[i] = phone.Run(Filt.High, x);
                float a = Math.Abs(x); if (a > peak) peak = a;
            }
            if (peak < 1e-6f) return new float[64];

            // loudness = RMS of what a phone speaker plays (above about 350 Hz), over the part that is actually sounding,
            // so a deep boom is not turned down for bass nobody hears and a long quiet tail does not make a short hit look soft
            double sum = 0; int cnt = 0; float floor = peak * 0.05f;
            for (int i = 0; i < n; i++) { if (Math.Abs(buf[i]) > floor) { float a = gain[i]; sum += (double)a * a; cnt++; } }
            float rms = (float)Math.Sqrt(sum / Math.Max(1, cnt));
            const float TargetRms = 0.15f, Ceiling = 0.89f, MaxSquash = 2.2f; // ceiling about -1 dBFS
            float g = TargetRms * level / Math.Max(rms, 1e-6f);
            g = Math.Min(g, Ceiling * MaxSquash / peak); // the limiter may take at most about 7 dB off a peak

            // look-ahead limiter: the gain a sample needs, ramped in over 2 ms before it and released over 60 ms after it
            float att = 1f / (0.002f * Rate), rel = 1f / (0.06f * Rate);
            for (int i = 0; i < n; i++) { float a = Math.Abs(buf[i]) * g; gain[i] = a > Ceiling ? Ceiling / a : 1f; }
            float cur = 1f;
            for (int i = 0; i < n; i++) { cur = Math.Min(gain[i], cur + rel); gain[i] = cur; }
            cur = 1f;
            for (int i = n - 1; i >= 0; i--) { cur = Math.Min(gain[i], cur + att); gain[i] = cur; }

            int last = 0;
            for (int i = 0; i < n; i++)
            {
                float y = buf[i] * g * gain[i];
                if (y > Ceiling) y = Ceiling; else if (y < -Ceiling) y = -Ceiling; // rounding guard only
                buf[i] = y;
                if (Math.Abs(y) > 0.0006f) last = i;
            }
            int len = Math.Min(n, last + (int)(0.004f * Rate));
            int fade = Math.Min(len, (int)(0.004f * Rate));
            var data = new float[Math.Max(64, len)];
            Array.Copy(buf, data, len);
            for (int i = 0; i < fade; i++) data[len - 1 - i] *= (float)i / fade; // no click when the clip stops
            return data;
        }
    }
}
