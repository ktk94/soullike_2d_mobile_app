using UnityEngine;
using System;

namespace SoulCraft.Audio
{
    /// <summary>
    /// 수학적 파형 합성을 통해 AudioClip을 코드로 생성하는 정적 유틸리티.
    /// 외부 오디오 파일 없이 모든 사운드를 프로시저럴하게 만든다.
    /// </summary>
    public static class SoundSynthesizer
    {
        // ════════════════════════════════════════════════════════
        //  Constants
        // ════════════════════════════════════════════════════════

        private const int DefaultSampleRate = 44100;
        private static System.Random _rng = new System.Random(42);

        // ════════════════════════════════════════════════════════
        //  Waveform Generators  (0‥1 normalised time → -1‥1)
        // ════════════════════════════════════════════════════════

        public static float Sine(float phase)
            => Mathf.Sin(phase * 2f * Mathf.PI);

        public static float Square(float phase)
            => (phase % 1f) < 0.5f ? 1f : -1f;

        public static float Sawtooth(float phase)
            => 2f * (phase % 1f) - 1f;

        public static float Triangle(float phase)
        {
            float p = phase % 1f;
            return p < 0.5f ? (4f * p - 1f) : (3f - 4f * p);
        }

        public static float WhiteNoise()
            => (float)(_rng.NextDouble() * 2.0 - 1.0);

        // ════════════════════════════════════════════════════════
        //  ADSR Envelope
        // ════════════════════════════════════════════════════════

        public struct ADSREnvelope
        {
            public float Attack;   // seconds
            public float Decay;    // seconds
            public float Sustain;  // level 0‥1
            public float Release;  // seconds

            public ADSREnvelope(float a, float d, float s, float r)
            {
                Attack = a; Decay = d; Sustain = s; Release = r;
            }

            /// <summary>
            /// t: 0‥duration, duration: 총 길이. 반환: 0‥1 엔벨로프 진폭.
            /// </summary>
            public float Evaluate(float t, float duration)
            {
                float releaseStart = duration - Release;
                if (releaseStart < 0f) releaseStart = 0f;

                if (t < 0f) return 0f;

                // Attack
                if (t < Attack)
                    return Attack > 0f ? t / Attack : 1f;

                // Decay
                float decayEnd = Attack + Decay;
                if (t < decayEnd)
                {
                    float ratio = Decay > 0f ? (t - Attack) / Decay : 1f;
                    return Mathf.Lerp(1f, Sustain, ratio);
                }

                // Sustain → Release
                if (t < releaseStart)
                    return Sustain;

                // Release
                if (t < duration)
                {
                    float ratio = Release > 0f ? (t - releaseStart) / Release : 1f;
                    return Mathf.Lerp(Sustain, 0f, ratio);
                }

                return 0f;
            }
        }

        // ════════════════════════════════════════════════════════
        //  Frequency Sweep
        // ════════════════════════════════════════════════════════

        public enum SweepMode { Linear, Exponential }

        /// <summary>
        /// 시간 t (0‥duration)에서의 주파수를 반환.
        /// </summary>
        public static float FrequencySweep(float t, float duration, float freqStart, float freqEnd, SweepMode mode)
        {
            float ratio = Mathf.Clamp01(t / Mathf.Max(duration, 0.0001f));
            return mode switch
            {
                SweepMode.Linear => Mathf.Lerp(freqStart, freqEnd, ratio),
                SweepMode.Exponential => freqStart * Mathf.Pow(freqEnd / Mathf.Max(freqStart, 0.01f), ratio),
                _ => freqStart
            };
        }

        /// <summary>
        /// 주파수 스윕 중의 누적 위상을 계산하여 반환.
        /// </summary>
        public static float AccumulatedPhase(float t, float dt, float freq, ref float phaseAccum)
        {
            phaseAccum += freq * dt;
            return phaseAccum;
        }

        // ════════════════════════════════════════════════════════
        //  Simple Filters (1-pole IIR)
        // ════════════════════════════════════════════════════════

        /// <summary>1-pole 로우패스 필터를 배열에 적용한다.</summary>
        public static void LowPass(float[] samples, float cutoffNormalized)
        {
            float rc = 1f / (cutoffNormalized * 2f * Mathf.PI);
            float dt = 1f;
            float alpha = dt / (rc + dt);
            float prev = samples[0];
            for (int i = 1; i < samples.Length; i++)
            {
                prev += alpha * (samples[i] - prev);
                samples[i] = prev;
            }
        }

        /// <summary>1-pole 하이패스 필터를 배열에 적용한다.</summary>
        public static void HighPass(float[] samples, float cutoffNormalized)
        {
            float rc = 1f / (cutoffNormalized * 2f * Mathf.PI);
            float dt = 1f;
            float alpha = rc / (rc + dt);
            float prevIn = samples[0];
            float prevOut = samples[0];
            for (int i = 1; i < samples.Length; i++)
            {
                float input = samples[i];
                prevOut = alpha * (prevOut + input - prevIn);
                prevIn = input;
                samples[i] = prevOut;
            }
        }

        // ════════════════════════════════════════════════════════
        //  Mixing Utilities
        // ════════════════════════════════════════════════════════

        /// <summary>여러 샘플 배열을 합산하여 하나로 만든다.</summary>
        public static float[] Mix(params (float[] data, float volume)[] layers)
        {
            if (layers.Length == 0) return Array.Empty<float>();

            int maxLen = 0;
            foreach (var (data, _) in layers)
                if (data.Length > maxLen) maxLen = data.Length;

            float[] result = new float[maxLen];
            foreach (var (data, vol) in layers)
            {
                for (int i = 0; i < data.Length; i++)
                    result[i] += data[i] * vol;
            }
            return result;
        }

        /// <summary>배열을 -1‥1 사이로 노멀라이즈한다.</summary>
        public static void Normalize(float[] samples)
        {
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float abs = Mathf.Abs(samples[i]);
                if (abs > peak) peak = abs;
            }
            if (peak > 0.0001f)
            {
                float inv = 1f / peak;
                for (int i = 0; i < samples.Length; i++)
                    samples[i] *= inv;
            }
        }

        /// <summary>소프트 클리핑 (tanh 기반).</summary>
        public static void SoftClip(float[] samples, float drive = 1f)
        {
            for (int i = 0; i < samples.Length; i++)
                samples[i] = (float)Math.Tanh(samples[i] * drive);
        }

        /// <summary>간단한 딜레이/에코 효과.</summary>
        public static void Delay(float[] samples, int delaySamples, float feedback, float wet)
        {
            float[] buffer = new float[samples.Length];
            Array.Copy(samples, buffer, samples.Length);

            for (int i = delaySamples; i < samples.Length; i++)
            {
                buffer[i] += buffer[i - delaySamples] * feedback;
                samples[i] = samples[i] * (1f - wet) + buffer[i] * wet;
            }
        }

        /// <summary>간단한 리버브 (다중 딜레이 합산).</summary>
        public static void Reverb(float[] samples, int sampleRate, float roomSize = 0.5f, float wet = 0.3f)
        {
            int[] delays = {
                (int)(0.0297f * sampleRate * roomSize),
                (int)(0.0371f * sampleRate * roomSize),
                (int)(0.0411f * sampleRate * roomSize),
                (int)(0.0437f * sampleRate * roomSize)
            };
            float fb = 0.7f * roomSize;

            float[] orig = new float[samples.Length];
            Array.Copy(samples, orig, samples.Length);

            foreach (int d in delays)
            {
                if (d <= 0 || d >= samples.Length) continue;
                float[] buf = new float[samples.Length];
                Array.Copy(orig, buf, samples.Length);
                for (int i = d; i < samples.Length; i++)
                    buf[i] += buf[i - d] * fb;
                for (int i = 0; i < samples.Length; i++)
                    samples[i] += buf[i] * (wet / delays.Length);
            }
        }

        /// <summary>페이드인/페이드아웃을 적용한다.</summary>
        public static void FadeInOut(float[] samples, int fadeInSamples, int fadeOutSamples)
        {
            for (int i = 0; i < fadeInSamples && i < samples.Length; i++)
                samples[i] *= (float)i / fadeInSamples;
            for (int i = 0; i < fadeOutSamples && i < samples.Length; i++)
            {
                int idx = samples.Length - 1 - i;
                samples[idx] *= (float)i / fadeOutSamples;
            }
        }

        // ════════════════════════════════════════════════════════
        //  Tone Generation Helpers
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// 단일 파형 톤을 생성한다.
        /// </summary>
        public static float[] GenerateTone(
            Func<float, float> waveFunc,
            float frequency,
            float duration,
            int sampleRate = DefaultSampleRate,
            ADSREnvelope? envelope = null)
        {
            int totalSamples = (int)(duration * sampleRate);
            float[] samples = new float[totalSamples];
            float phase = 0f;
            float dt = 1f / sampleRate;
            var env = envelope ?? new ADSREnvelope(0.01f, 0.05f, 0.7f, 0.05f);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i * dt;
                phase += frequency * dt;
                samples[i] = waveFunc(phase) * env.Evaluate(t, duration);
            }
            return samples;
        }

        /// <summary>
        /// 주파수 스윕을 가진 톤을 생성한다.
        /// </summary>
        public static float[] GenerateSweep(
            Func<float, float> waveFunc,
            float freqStart,
            float freqEnd,
            float duration,
            SweepMode sweepMode = SweepMode.Linear,
            int sampleRate = DefaultSampleRate,
            ADSREnvelope? envelope = null)
        {
            int totalSamples = (int)(duration * sampleRate);
            float[] samples = new float[totalSamples];
            float phase = 0f;
            float dt = 1f / sampleRate;
            var env = envelope ?? new ADSREnvelope(0.01f, 0.05f, 0.7f, 0.05f);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i * dt;
                float freq = FrequencySweep(t, duration, freqStart, freqEnd, sweepMode);
                phase += freq * dt;
                samples[i] = waveFunc(phase) * env.Evaluate(t, duration);
            }
            return samples;
        }

        /// <summary>
        /// 화이트노이즈를 생성한다.
        /// </summary>
        public static float[] GenerateNoise(
            float duration,
            int sampleRate = DefaultSampleRate,
            ADSREnvelope? envelope = null)
        {
            int totalSamples = (int)(duration * sampleRate);
            float[] samples = new float[totalSamples];
            float dt = 1f / sampleRate;
            var env = envelope ?? new ADSREnvelope(0.01f, 0.05f, 0.7f, 0.05f);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i * dt;
                samples[i] = WhiteNoise() * env.Evaluate(t, duration);
            }
            return samples;
        }

        // ════════════════════════════════════════════════════════
        //  AudioClip Creation
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// float 배열로부터 AudioClip을 생성한다.
        /// </summary>
        public static AudioClip CreateClip(string name, float[] samples, int sampleRate = DefaultSampleRate)
        {
            if (samples == null || samples.Length == 0) return null;

            AudioClip clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// 빈 AudioClip을 생성한다 (콜백 방식이 아닌 빈 버퍼).
        /// </summary>
        public static AudioClip CreateClip(string name, float duration, int sampleRate = DefaultSampleRate)
        {
            int totalSamples = (int)(duration * sampleRate);
            float[] samples = new float[totalSamples];
            AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // ════════════════════════════════════════════════════════
        //  Musical Helpers
        // ════════════════════════════════════════════════════════

        /// <summary>MIDI 노트 번호로부터 주파수를 구한다 (A4=440Hz=MIDI 69).</summary>
        public static float NoteToFreq(int midiNote)
            => 440f * Mathf.Pow(2f, (midiNote - 69) / 12f);

        /// <summary>노트 이름으로부터 주파수를 구한다. 예: "C4", "A#3".</summary>
        public static float NoteNameToFreq(string noteName)
        {
            // 간단한 파서: C=0, C#=1, D=2, ...
            int idx = 0;
            int semitone = noteName[idx] switch
            {
                'C' => 0, 'D' => 2, 'E' => 4, 'F' => 5,
                'G' => 7, 'A' => 9, 'B' => 11,
                _ => 0
            };
            idx++;

            if (idx < noteName.Length && noteName[idx] == '#')
            {
                semitone++;
                idx++;
            }
            else if (idx < noteName.Length && noteName[idx] == 'b')
            {
                semitone--;
                idx++;
            }

            int octave = 4;
            if (idx < noteName.Length && char.IsDigit(noteName[idx]))
                octave = noteName[idx] - '0';

            int midi = (octave + 1) * 12 + semitone;
            return NoteToFreq(midi);
        }

        /// <summary>간단한 피아노 톤을 생성한다 (사인파 + 하모닉스).</summary>
        public static float[] GeneratePianoNote(float freq, float duration, float velocity = 0.8f,
            int sampleRate = DefaultSampleRate)
        {
            var env = new ADSREnvelope(0.005f, 0.15f, 0.3f, duration * 0.4f);

            var fundamental = GenerateTone(Sine, freq, duration, sampleRate, env);
            var harm2 = GenerateTone(Sine, freq * 2f, duration, sampleRate,
                new ADSREnvelope(0.005f, 0.1f, 0.15f, duration * 0.3f));
            var harm3 = GenerateTone(Sine, freq * 3f, duration, sampleRate,
                new ADSREnvelope(0.005f, 0.08f, 0.08f, duration * 0.2f));
            var harm4 = GenerateTone(Sine, freq * 4f, duration, sampleRate,
                new ADSREnvelope(0.003f, 0.06f, 0.04f, duration * 0.15f));

            float[] mixed = Mix(
                (fundamental, 0.6f * velocity),
                (harm2, 0.25f * velocity),
                (harm3, 0.1f * velocity),
                (harm4, 0.05f * velocity)
            );

            return mixed;
        }

        /// <summary>코드(화음)를 생성한다. frequencies 배열로 여러 음을 동시에 울린다.</summary>
        public static float[] GenerateChord(float[] frequencies, float duration, float velocity = 0.7f,
            int sampleRate = DefaultSampleRate)
        {
            var layers = new (float[] data, float volume)[frequencies.Length];
            float vol = velocity / frequencies.Length;

            for (int i = 0; i < frequencies.Length; i++)
            {
                layers[i] = (GeneratePianoNote(frequencies[i], duration, 1f, sampleRate), vol);
            }

            return Mix(layers);
        }

        /// <summary>아르페지오를 생성한다. 각 노트를 순차적으로 재생한다.</summary>
        public static float[] GenerateArpeggio(float[] frequencies, float noteLength, float totalDuration,
            float velocity = 0.7f, int sampleRate = DefaultSampleRate)
        {
            int totalSamples = (int)(totalDuration * sampleRate);
            float[] result = new float[totalSamples];
            int noteSamples = (int)(noteLength * sampleRate);

            for (int n = 0; n < frequencies.Length; n++)
            {
                int startSample = n * noteSamples;
                if (startSample >= totalSamples) break;

                float[] note = GeneratePianoNote(frequencies[n], noteLength, velocity, sampleRate);
                for (int i = 0; i < note.Length && (startSample + i) < totalSamples; i++)
                {
                    result[startSample + i] += note[i];
                }
            }

            return result;
        }

        /// <summary>드럼 킥 합성: 저주파 사인 스윕 + 노이즈 어택.</summary>
        public static float[] GenerateKick(float duration = 0.3f, int sampleRate = DefaultSampleRate)
        {
            var body = GenerateSweep(Sine, 150f, 40f, duration, SweepMode.Exponential,
                sampleRate, new ADSREnvelope(0.005f, 0.1f, 0.2f, duration * 0.5f));
            var click = GenerateNoise(0.015f, sampleRate,
                new ADSREnvelope(0.001f, 0.005f, 0.0f, 0.009f));

            int totalSamples = (int)(duration * sampleRate);
            float[] result = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                result[i] = body[i];
                if (i < click.Length) result[i] += click[i] * 0.5f;
            }
            return result;
        }

        /// <summary>하이햇 합성: 필터드 노이즈.</summary>
        public static float[] GenerateHiHat(float duration = 0.08f, bool open = false, int sampleRate = DefaultSampleRate)
        {
            float dur = open ? duration * 3f : duration;
            var env = new ADSREnvelope(0.001f, dur * 0.3f, open ? 0.3f : 0.0f, dur * 0.5f);
            float[] samples = GenerateNoise(dur, sampleRate, env);
            HighPass(samples, 0.3f);
            return samples;
        }

        /// <summary>스네어 합성: 노이즈 + 톤.</summary>
        public static float[] GenerateSnare(float duration = 0.2f, int sampleRate = DefaultSampleRate)
        {
            var tone = GenerateSweep(Sine, 200f, 120f, duration, SweepMode.Exponential,
                sampleRate, new ADSREnvelope(0.002f, 0.05f, 0.0f, 0.1f));
            var noise = GenerateNoise(duration, sampleRate,
                new ADSREnvelope(0.002f, 0.05f, 0.1f, duration * 0.6f));
            HighPass(noise, 0.15f);
            return Mix((tone, 0.5f), (noise, 0.7f));
        }

        /// <summary>RNG 시드 리셋 (결정론적 결과 보장).</summary>
        public static void ResetRng(int seed = 42)
        {
            _rng = new System.Random(seed);
        }
    }
}
