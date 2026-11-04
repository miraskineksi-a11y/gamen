using System;
using UnityEngine;

namespace Caribbean
{
    /// <summary>
    /// Процедурный синтез всех звуков и музыки (никаких аудиофайлов не нужно).
    /// Музыка — оригинальная мелодия в ре миноре: «щипковые» струны (алгоритм
    /// Карплуса — Стронга), свистулька и барабаны.
    /// </summary>
    public static class AudioSynth
    {
        public const int Rate = 22050;
        static System.Random rng = new System.Random(1234);

        static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);

        static AudioClip Make(string name, float[] data)
        {
            Normalize(data, 0.92f);
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void Normalize(float[] d, float peak)
        {
            float m = 0f;
            for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
            if (m < 1e-5f) return;
            float k = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        static float[] Buf(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

        // ------------------------------------------------------------------ SFX
        public static AudioClip Cannon()
        {
            var d = Buf(1.8f);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t * 3.2f);
                float n = Noise();
                lp += (n - lp) * Mathf.Lerp(0.5f, 0.04f, Mathf.Clamp01(t * 3f));
                lp2 += (lp - lp2) * 0.2f;
                float thump = Mathf.Sin(2f * Mathf.PI * (55f - 25f * t) * t) * Mathf.Exp(-t * 6f);
                float crack = n * Mathf.Exp(-t * 60f) * 0.6f;
                d[i] = (lp2 * 2.2f + thump * 0.9f) * env + crack;
            }
            return Make("cannon", d);
        }

        public static AudioClip Splash()
        {
            var d = Buf(1.1f);
            float lp = 0f, hp = 0f, prev = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float n = Noise();
                lp += (n - lp) * 0.35f;
                hp = 0.92f * (hp + lp - prev);
                prev = lp;
                float env = Mathf.Clamp01(t * 40f) * Mathf.Exp(-t * 4.5f);
                float bubble = Mathf.Sin(2f * Mathf.PI * (300f + 500f * t) * t) * Mathf.Exp(-t * 9f) * 0.15f;
                d[i] = hp * env + bubble;
            }
            return Make("splash", d);
        }

        public static AudioClip WoodHit()
        {
            var d = Buf(0.7f);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float n = Noise();
                lp += (n - lp) * 0.25f;
                float knock = Mathf.Sin(2f * Mathf.PI * 140f * t) * Mathf.Exp(-t * 14f);
                float crackle = (rng.NextDouble() < 0.02 * Math.Exp(-t * 6) ? Noise() * 2f : 0f);
                d[i] = (lp * 1.4f * Mathf.Exp(-t * 9f)) + knock * 0.8f + crackle;
            }
            return Make("woodhit", d);
        }

        public static AudioClip SwordClang()
        {
            var d = Buf(0.6f);
            float[] f = { 1870f, 2630f, 3420f, 4710f };
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float s = 0f;
                for (int k = 0; k < f.Length; k++) s += Mathf.Sin(2f * Mathf.PI * f[k] * t) * Mathf.Exp(-t * (8f + k * 5f)) / (k + 1);
                d[i] = s + Noise() * Mathf.Exp(-t * 90f) * 0.5f;
            }
            return Make("clang", d);
        }

        public static AudioClip Swoosh()
        {
            var d = Buf(0.32f);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.32f));
                lp += (Noise() - lp) * Mathf.Lerp(0.05f, 0.4f, env);
                d[i] = lp * env * env;
            }
            return Make("swoosh", d);
        }

        public static AudioClip Thud()
        {
            var d = Buf(0.35f);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                lp += (Noise() - lp) * 0.15f;
                d[i] = (Mathf.Sin(2f * Mathf.PI * (110f - 60f * t) * t) * 0.9f + lp) * Mathf.Exp(-t * 16f);
            }
            return Make("thud", d);
        }

        public static AudioClip Coin()
        {
            var d = Buf(0.45f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float f = t < 0.08f ? 1568f : 2093f;
                float env = t < 0.08f ? Mathf.Exp(-t * 20f) : Mathf.Exp(-(t - 0.08f) * 9f);
                d[i] = (Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * f * 2.01f * t)) * env;
            }
            return Make("coin", d);
        }

        public static AudioClip Dig()
        {
            var d = Buf(0.3f);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                lp += (Noise() - lp) * 0.3f;
                d[i] = lp * Mathf.Exp(-t * 14f) * Mathf.Clamp01(t * 80f) + Mathf.Sin(2f * Mathf.PI * 90f * t) * Mathf.Exp(-t * 25f) * 0.4f;
            }
            return Make("dig", d);
        }

        public static AudioClip ChestOpen()
        {
            var d = Buf(1.6f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float creak = 0f;
                if (t < 0.6f)
                {
                    float f = 180f + 120f * Mathf.Sin(t * 9f);
                    creak = (Mathf.Repeat(f * t, 1f) * 2f - 1f) * 0.25f * Mathf.Sin(Mathf.PI * t / 0.6f);
                }
                float sparkle = 0f;
                if (t > 0.5f)
                {
                    float u = t - 0.5f;
                    for (int k = 0; k < 4; k++)
                    {
                        float tk = u - k * 0.11f;
                        if (tk > 0) sparkle += Mathf.Sin(2f * Mathf.PI * (1318f + k * 330f) * tk) * Mathf.Exp(-tk * 7f) * 0.5f;
                    }
                }
                d[i] = creak + sparkle;
            }
            return Make("chest", d);
        }

        public static AudioClip Roar(float pitch = 1f)
        {
            var d = Buf(2.2f);
            float lp = 0f, ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Clamp01(t * 5f) * Mathf.Clamp01((2.2f - t) * 1.5f);
                float f = (70f + 25f * Mathf.Sin(t * 3f) + 8f * Mathf.Sin(t * 37f)) * pitch;
                ph += f / Rate;
                float saw = (ph - Mathf.Floor(ph)) * 2f - 1f;
                lp += (saw + Noise() * 0.7f - lp) * 0.08f;
                d[i] = lp * env;
            }
            return Make("roar", d);
        }

        public static AudioClip Bite()
        {
            var d = Buf(0.5f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                d[i] = (Noise() * Mathf.Exp(-t * 30f) + Mathf.Sin(2f * Mathf.PI * 80f * t) * Mathf.Exp(-t * 10f)) * 0.9f;
            }
            return Make("bite", d);
        }

        public static AudioClip UiClick()
        {
            var d = Buf(0.08f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                d[i] = Mathf.Sin(2f * Mathf.PI * 900f * t) * Mathf.Exp(-t * 60f);
            }
            return Make("click", d);
        }

        public static AudioClip Fanfare()
        {
            var d = Buf(3.2f);
            int[] notes = { 62, 66, 69, 74, 74, 76, 78 };
            float[] starts = { 0f, 0.18f, 0.36f, 0.54f, 1.1f, 1.3f, 1.5f };
            for (int n = 0; n < notes.Length; n++)
                AddLead(d, starts[n], n == notes.Length - 1 ? 1.5f : 0.5f, Midi(notes[n]), 0.6f, 0.0f);
            return Make("fanfare", d);
        }

        // ------------------------------------------------------------------ ambience
        public static AudioClip Waves()
        {
            var d = Buf(8f);
            float brown = 0f, lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                brown = brown * 0.995f + Noise() * 0.05f;
                lp += (brown - lp) * 0.08f;
                float swell = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * t / 4f) * Mathf.Sin(2f * Mathf.PI * t / 8f + 1f);
                d[i] = lp * swell;
            }
            return Make("waves", LoopFade(d, 1f));
        }

        public static AudioClip Wind()
        {
            var d = Buf(8f);
            float lp = 0f, bp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                lp += (Noise() - lp) * 0.06f;
                bp += (lp - bp) * 0.02f;
                float gust = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 8f) * Mathf.Sin(2f * Mathf.PI * t / 2f + 0.6f);
                d[i] = (lp - bp) * gust;
            }
            return Make("wind", LoopFade(d, 1f));
        }

        static float[] LoopFade(float[] d, float seconds)
        {
            int n = (int)(seconds * Rate);
            var o = new float[d.Length - n];
            Array.Copy(d, o, o.Length);
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                o[i] = d[i] * k + d[d.Length - n + i] * (1f - k);
            }
            return o;
        }

        // ------------------------------------------------------------------ music
        static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);

        static void AddPluck(float[] buf, float start, float dur, float freq, float vol, float decay = 0.996f)
        {
            int s0 = (int)(start * Rate);
            int len = Mathf.Min(buf.Length - s0, (int)((dur + 0.6f) * Rate));
            if (len <= 0) return;
            int period = Mathf.Max(2, Mathf.RoundToInt(Rate / freq));
            var ring = new float[period];
            for (int i = 0; i < period; i++) ring[i] = Noise();
            int idx = 0;
            for (int i = 0; i < len; i++)
            {
                int nx = (idx + 1) % period;
                float v = ring[idx];
                ring[idx] = (ring[idx] + ring[nx]) * 0.5f * decay;
                idx = nx;
                float rel = i > dur * Rate ? Mathf.Exp(-(i - dur * Rate) / (0.08f * Rate)) : 1f;
                buf[(s0 + i) % buf.Length] += v * vol * rel;
            }
        }

        static void AddLead(float[] buf, float start, float dur, float freq, float vol, float vibrato = 0.006f)
        {
            int s0 = (int)(start * Rate);
            int len = (int)((dur + 0.15f) * Rate);
            float ph = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)Rate;
                float vib = 1f + vibrato * Mathf.Sin(2f * Mathf.PI * 5.5f * t) * Mathf.Clamp01(t * 3f);
                ph += freq * vib / Rate;
                float tri = 1f - 4f * Mathf.Abs(Mathf.Repeat(ph, 1f) - 0.5f);
                float env = Mathf.Clamp01(t * 30f) * (t > dur ? Mathf.Exp(-(t - dur) * 25f) : 1f) * (0.85f + 0.15f * Mathf.Exp(-t * 4f));
                int k = (s0 + i) % buf.Length;
                buf[k] += tri * vol * env;
            }
        }

        static void AddKick(float[] buf, float start, float vol)
        {
            int s0 = (int)(start * Rate);
            for (int i = 0; i < (int)(0.35f * Rate); i++)
            {
                float t = i / (float)Rate;
                float f = 45f + 90f * Mathf.Exp(-t * 25f);
                buf[(s0 + i) % buf.Length] += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 9f) * vol;
            }
        }

        static void AddSnare(float[] buf, float start, float vol)
        {
            int s0 = (int)(start * Rate);
            float lp = 0f;
            for (int i = 0; i < (int)(0.25f * Rate); i++)
            {
                float t = i / (float)Rate;
                float n = Noise();
                lp += (n - lp) * 0.5f;
                buf[(s0 + i) % buf.Length] += ((n - lp) * 0.8f + Mathf.Sin(2f * Mathf.PI * 190f * t) * 0.3f) * Mathf.Exp(-t * 18f) * vol;
            }
        }

        // аккорды: корень (MIDI) на каждый такт
        static readonly int[] Chords = { 50, 50, 48, 48, 46, 46, 45, 45, 50, 50, 48, 48, 46, 45, 50, 50 };
        static readonly bool[] Minor = { true, true, false, false, false, false, false, false, true, true, false, false, false, false, true, true };

        // мелодия: (нота, начало в восьмых от начала такта, длина в восьмых), по тактам
        static readonly int[][] Melody =
        {
            new[] { 69, 0, 2, 74, 2, 1, 74, 3, 2, 76, 5, 1 },
            new[] { 77, 0, 3, 76, 3, 1, 74, 4, 2 },
            new[] { 76, 0, 2, 72, 2, 1, 67, 3, 2, 72, 5, 1 },
            new[] { 76, 0, 3, 74, 3, 1, 72, 4, 2 },
            new[] { 74, 0, 2, 70, 2, 1, 65, 3, 2, 70, 5, 1 },
            new[] { 74, 0, 2, 77, 2, 1, 76, 3, 2, 74, 5, 1 },
            new[] { 73, 0, 3, 69, 3, 3 },
            new[] { 76, 0, 2, 74, 2, 1, 73, 3, 3 },
            new[] { 74, 0, 2, 77, 2, 1, 81, 3, 2, 77, 5, 1 },
            new[] { 79, 0, 1, 77, 1, 1, 76, 2, 1, 74, 3, 3 },
            new[] { 76, 0, 2, 79, 2, 1, 72, 3, 2, 76, 5, 1 },
            new[] { 74, 0, 1, 72, 1, 1, 70, 2, 1, 69, 3, 3 },
            new[] { 70, 0, 2, 74, 2, 1, 77, 3, 3 },
            new[] { 76, 0, 2, 73, 2, 1, 69, 3, 3 },
            new[] { 74, 0, 3, 69, 3, 2, 65, 5, 1 },
            new[] { 62, 0, 6 },
        };

        /// <summary>Спокойная морская песня (16 тактов размера 6/8).</summary>
        public static AudioClip ShantyLoop()
        {
            const float eighth = 0.2f;
            const int bars = 16;
            var d = new float[(int)(bars * 6 * eighth * Rate)];
            for (int b = 0; b < bars; b++)
            {
                float t0 = b * 6 * eighth;
                int root = Chords[b];
                int third = root + (Minor[b] ? 3 : 4);
                if (root == 45) third = root + 4; // A-мажор (гармонический минор)
                int fifth = root + 7;
                AddPluck(d, t0, eighth * 3, Midi(root - 12), 0.55f, 0.997f);
                AddPluck(d, t0 + 3 * eighth, eighth * 3, Midi(root - 12 + (b % 2 == 0 ? 0 : 7)), 0.45f, 0.997f);
                int[] arp = { root, fifth, root + 12, third + 12, fifth, root + 12 };
                for (int k = 0; k < 6; k++)
                    AddPluck(d, t0 + k * eighth, eighth, Midi(arp[k]), 0.16f, 0.994f);
                var mel = Melody[b];
                for (int k = 0; k + 2 < mel.Length; k += 3)
                    AddLead(d, t0 + mel[k + 1] * eighth, mel[k + 2] * eighth * 0.92f, Midi(mel[k]), 0.2f);
            }
            Normalize(d, 0.8f);
            var clip = AudioClip.Create("shanty", d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        /// <summary>Напряжённая петля для боя: барабаны и бас.</summary>
        public static AudioClip BattleLoop()
        {
            const float eighth = 0.15f;
            const int bars = 8;
            var d = new float[(int)(bars * 6 * eighth * Rate)];
            int[] roots = { 50, 50, 46, 48, 50, 50, 46, 45 };
            for (int b = 0; b < bars; b++)
            {
                float t0 = b * 6 * eighth;
                int r = roots[b];
                AddKick(d, t0, 0.9f);
                AddKick(d, t0 + 3 * eighth, 0.7f);
                AddSnare(d, t0 + 2 * eighth, 0.35f);
                AddSnare(d, t0 + 5 * eighth, 0.5f);
                int[] bass = { r - 12, r - 12, r - 5, r - 12, r - 9, r - 7 };
                for (int k = 0; k < 6; k++) AddPluck(d, t0 + k * eighth, eighth * 0.9f, Midi(bass[k]), 0.45f, 0.993f);
                if (b % 2 == 1)
                {
                    AddLead(d, t0, eighth * 2.5f, Midi(r + 24), 0.12f, 0.01f);
                    AddLead(d, t0 + 3 * eighth, eighth * 2.5f, Midi(r + 22), 0.12f, 0.01f);
                }
            }
            Normalize(d, 0.8f);
            var clip = AudioClip.Create("battle", d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }
    }
}
