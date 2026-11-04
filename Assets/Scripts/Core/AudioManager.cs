using System.Collections.Generic;
using UnityEngine;

namespace Caribbean
{
    public enum Sfx { Cannon, Splash, WoodHit, Clang, Swoosh, Thud, Coin, Dig, Chest, Roar, RoarHigh, Bite, Click, Fanfare }

    /// <summary>Воспроизведение звуков: 3D-эффекты, музыка (мирная/боевая) и фон моря.</summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }

        public float masterVolume = 0.9f;
        public float musicVolume = 0.45f;

        readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        AudioSource ui, musicA, musicB, waves, wind;
        AudioClip shanty, battle;
        bool battleMode;
        float musicBlend; // 0 — мирная, 1 — боевая

        public void Build()
        {
            I = this;
            clips[Sfx.Cannon] = AudioSynth.Cannon();
            clips[Sfx.Splash] = AudioSynth.Splash();
            clips[Sfx.WoodHit] = AudioSynth.WoodHit();
            clips[Sfx.Clang] = AudioSynth.SwordClang();
            clips[Sfx.Swoosh] = AudioSynth.Swoosh();
            clips[Sfx.Thud] = AudioSynth.Thud();
            clips[Sfx.Coin] = AudioSynth.Coin();
            clips[Sfx.Dig] = AudioSynth.Dig();
            clips[Sfx.Chest] = AudioSynth.ChestOpen();
            clips[Sfx.Roar] = AudioSynth.Roar(1f);
            clips[Sfx.RoarHigh] = AudioSynth.Roar(1.6f);
            clips[Sfx.Bite] = AudioSynth.Bite();
            clips[Sfx.Click] = AudioSynth.UiClick();
            clips[Sfx.Fanfare] = AudioSynth.Fanfare();
            shanty = AudioSynth.ShantyLoop();
            battle = AudioSynth.BattleLoop();

            // пул 3D-источников (каждый на своём объекте, чтобы ставить его в точку звука)
            for (int i = 0; i < 20; i++)
            {
                var go = new GameObject("Sfx" + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 6f;
                s.maxDistance = 260f;
                s.dopplerLevel = 0f;
                pool.Add(s);
            }
            ui = Make2D("UI", 1f);
            musicA = Make2D("Music", 0f, true, shanty);
            musicB = Make2D("Battle", 0f, true, battle);
            waves = Make2D("Waves", 0.35f, true, AudioSynth.Waves());
            wind = Make2D("Wind", 0.12f, true, AudioSynth.Wind());
            musicA.Play();
            musicB.Play();
            waves.Play();
            wind.Play();
        }

        AudioSource Make2D(string name, float vol, bool loop = false, AudioClip clip = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.spatialBlend = 0f;
            s.loop = loop;
            s.clip = clip;
            s.volume = vol;
            s.playOnAwake = false;
            return s;
        }

        /// <summary>Звук в точке мира.</summary>
        public static void Play(Sfx sfx, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            if (I == null || !I.clips.TryGetValue(sfx, out var clip)) return;
            AudioSource best = null;
            foreach (var s in I.pool)
                if (!s.isPlaying) { best = s; break; }
            if (best == null) best = I.pool[Random.Range(0, I.pool.Count)];
            best.transform.position = pos;
            best.clip = clip;
            best.volume = volume * I.masterVolume;
            best.pitch = pitch * Random.Range(0.93f, 1.07f);
            best.Play();
        }

        /// <summary>Звук интерфейса (без позиции).</summary>
        public static void PlayUI(Sfx sfx, float volume = 1f, float pitch = 1f)
        {
            if (I == null || !I.clips.TryGetValue(sfx, out var clip)) return;
            I.ui.pitch = pitch;
            I.ui.PlayOneShot(clip, volume * I.masterVolume);
        }

        public static void SetBattle(bool on) { if (I != null) I.battleMode = on; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            musicBlend = Mathf.MoveTowards(musicBlend, battleMode ? 1f : 0f, dt * 0.5f);
            float mv = musicVolume * masterVolume;
            musicA.volume = mv * (1f - musicBlend) * 0.9f;
            musicB.volume = mv * musicBlend;
            bool paused = Time.timeScale < 0.01f;
            waves.volume = (paused ? 0.15f : 0.35f) * masterVolume;
            wind.volume = 0.12f * masterVolume;
            AudioListener.volume = 1f;
        }
    }
}
