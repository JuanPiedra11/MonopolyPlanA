using System.Collections.Generic;
using UnityEngine;

namespace MonopolyPlanA
{
    /// <summary>
    /// Música y SFX del juego (clips en Resources/Audio). Se crea solo al primer
    /// uso y sobrevive entre escenas. Volúmenes editables en el Inspector
    /// (GameObject "AudioManager" en runtime).
    /// Clips esperados: music_medieval, sfx_coins_gain, sfx_coins_lose, sfx_dice,
    /// sfx_button, sfx_jail, sfx_build_house, sfx_token_step, sfx_trade_offer,
    /// sfx_trade_reject, sfx_victory, sfx_crowd.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        static AudioManager _inst;
        AudioSource _music;
        AudioSource _sfx;
        static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        [Range(0f, 1f)] public float musicVolume = 0.4f;
        [Range(0f, 1f)] public float sfxVolume = 0.9f;

        public static AudioManager Instance
        {
            get
            {
                if (_inst == null)
                {
                    var go = new GameObject("AudioManager");
                    DontDestroyOnLoad(go);
                    _inst = go.AddComponent<AudioManager>();
                    _inst._music = go.AddComponent<AudioSource>();
                    _inst._music.loop = true;
                    _inst._music.playOnAwake = false;
                    _inst._sfx = go.AddComponent<AudioSource>();
                    _inst._sfx.playOnAwake = false;
                }
                return _inst;
            }
        }

        void Update()
        {
            if (_music != null && !Mathf.Approximately(_music.volume, musicVolume))
                _music.volume = musicVolume;
        }

        static AudioClip Load(string name)
        {
            if (!Cache.TryGetValue(name, out var c) || c == null)
            {
                c = Resources.Load<AudioClip>("Audio/" + name);
                Cache[name] = c;
            }
            return c;
        }

        /// <summary>Música en bucle; si ya suena ese clip, no la reinicia.</summary>
        public static void PlayMusic(string name)
        {
            var am = Instance;
            var clip = Load(name);
            if (clip == null || (am._music.clip == clip && am._music.isPlaying)) return;
            am._music.clip = clip;
            am._music.volume = am.musicVolume;
            am._music.Play();
        }

        public static void StopMusic()
        {
            if (_inst != null) _inst._music.Stop();
        }

        /// <summary>Efecto de sonido one-shot.</summary>
        public static void Play(string name, float volume = 1f)
        {
            var am = Instance;
            var clip = Load(name);
            if (clip != null) am._sfx.PlayOneShot(clip, volume * am.sfxVolume);
        }

        static readonly Dictionary<string, List<AudioClip>> Variants = new Dictionary<string, List<AudioClip>>();

        /// <summary>
        /// Variante aleatoria: busca clips "base_0", "base_1", ... y elige uno
        /// distinto cada vez (p. ej. las tiradas de dados).
        /// </summary>
        public static void PlayRandom(string baseName, float volume = 1f)
        {
            if (!Variants.TryGetValue(baseName, out var list))
            {
                list = new List<AudioClip>();
                for (int i = 0; i < 16; i++)
                {
                    var c = Resources.Load<AudioClip>($"Audio/{baseName}_{i}");
                    if (c == null) break;
                    list.Add(c);
                }
                Variants[baseName] = list;
            }
            if (list.Count == 0) { Play(baseName, volume); return; }
            var am = Instance;
            var clip = list[Random.Range(0, list.Count)];
            am._sfx.PlayOneShot(clip, volume * am.sfxVolume);
        }
    }
}
