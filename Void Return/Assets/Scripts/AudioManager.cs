using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Central audio manager with AudioMixer support.
///
/// CHANGES IN THIS VERSION:
///  — Added AudioMixer reference for professional volume control.
///  — Music and SFX volumes now route through AudioMixer exposed parameters
///    instead of directly setting AudioSource.volume. This means the master
///    volume slider in the options menu can duck everything at once.
///  — SetMusicVolume / SetSFXVolume / SetMasterVolume now write to the mixer
///    using logarithmic conversion (dB) for natural-feeling sliders.
///  — All settings still persist via PlayerPrefs.
///
/// SETUP:
///  1. In the Project window → Assets/Audio/ → right-click → Create → Audio Mixer.
///     Name it "GameAudioMixer".
///  2. Double-click GameAudioMixer to open the Audio Mixer window.
///  3. You will see a "Master" group. Click on it.
///  4. Right-click Master → Add child group → name it "Music".
///  5. Right-click Master → Add child group → name it "SFX".
///  6. Select the Music group → in the Inspector on the right, right-click the
///     Volume parameter → "Expose 'Volume' to script" → name it "MusicVolume".
///  7. Select the SFX group → same → expose as "SFXVolume".
///  8. Select the Master group → expose as "MasterVolume".
///  9. Drag this AudioMixer asset into the 'gameMixer' field below.
/// 10. On your MusicSource AudioSource: Output → select "Music" group.
/// 11. On your SFXSource AudioSource: Output → select "SFX" group.
/// </summary>
public class AudioManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Singleton
    // ─────────────────────────────────────────────────────────────────────────

    public static AudioManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Audio Mixer")]
    [Tooltip("Drag your GameAudioMixer asset here (Assets/Audio/GameAudioMixer). " +
             "See script header for full setup instructions.")]
    public AudioMixer gameMixer;

    [Tooltip("Exact name of the exposed parameter for Music volume in the mixer. " +
             "Default: 'MusicVolume'. Must match what you exposed in the Audio Mixer window.")]
    public string mixerParamMusic  = "MusicVolume";

    [Tooltip("Exact name of the exposed parameter for SFX volume.")]
    public string mixerParamSFX    = "SFXVolume";

    [Tooltip("Exact name of the exposed parameter for Master volume.")]
    public string mixerParamMaster = "MasterVolume";

    [Header("Audio Sources — Drag child AudioSource components here")]
    [Tooltip("AudioSource for background music. Set its Output to the 'Music' mixer group.")]
    public AudioSource musicSource;

    [Tooltip("AudioSource for sound effects. Set its Output to the 'SFX' mixer group.")]
    public AudioSource sfxSource;

    [Header("Ambient Sound")]
    [Tooltip("Audio clip looped as ambient background the moment the game scene loads.")]
    public AudioClip ambientClip;

    [Tooltip("Volume of the ambient loop (0–1).")]
    [Range(0f, 1f)]
    public float ambientVolume = 0.35f;

    [Header("Music Tracks Library")]
    [Tooltip("All music tracks. Each entry needs a unique Id string and an AudioClip. " +
             "Call AudioManager.Instance.PlayMusic('your_id') to switch tracks.\n\n" +
             "Suggested IDs: ambient_space, tension_zone2, danger_zone3, main_menu, victory")]
    public SoundEntry[] musicTracks;

    [Header("Sound Effects Library")]
    [Tooltip("All sound effects. Each entry needs a unique Id string and an AudioClip. " +
             "Call AudioManager.Instance.PlaySFX('your_id') to play.\n\n" +
             "Suggested IDs:\n" +
             "pickup, canister_pickup,\n" +
             "boots_on, boots_off, boots_step,\n" +
             "tether_fire, tether_hook, tether_release,\n" +
             "grenade_launch, grenade_detonate,\n" +
             "thruster_active, thruster_empty,\n" +
             "meteorite_impact, meteorite_incoming,\n" +
             "rift_start, repair_stage, repair_complete,\n" +
             "oxygen_warning, oxygen_critical,\n" +
             "jump, ui_click, ui_open, ui_notification")]
    public SoundEntry[] soundEffects;

    [Header("Default Volumes (0–1)")]
    [Tooltip("Default master volume loaded at first launch.")]
    [Range(0f, 1f)]
    public float defaultMasterVolume = 1f;

    [Tooltip("Default music volume.")]
    [Range(0f, 1f)]
    public float defaultMusicVolume = 0.8f;

    [Tooltip("Default SFX volume.")]
    [Range(0f, 1f)]
    public float defaultSFXVolume = 1f;

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private float _masterVolume;
    private float _musicVolume;
    private float _sfxVolume;
    private bool  _musicEnabled = true;
    private bool  _sfxEnabled   = true;

    // ─────────────────────────────────────────────────────────────────────────
    // Public Properties
    // ─────────────────────────────────────────────────────────────────────────

    public float MasterVolume => _masterVolume;
    public float MusicVolume  => _musicVolume;
    public float SFXVolume    => _sfxVolume;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadSettings();
    }

    private void Start()
    {
        // Apply persisted volumes to the mixer on startup
        ApplyAllVolumesToMixer();

        // Start ambient loop
        if (ambientClip != null && musicSource != null)
        {
            musicSource.clip   = ambientClip;
            musicSource.loop   = true;
            musicSource.volume = 1f; // Volume is controlled by the mixer group
            musicSource.Play();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — Playback
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Play a sound effect by its string Id.
    /// Example: AudioManager.Instance.PlaySFX("pickup");
    /// </summary>
    public void PlaySFX(string id)
    {
        if (!_sfxEnabled || sfxSource == null) return;

        foreach (var entry in soundEffects)
        {
            if (entry.id != id || entry.clip == null) continue;
            sfxSource.PlayOneShot(entry.clip, entry.volume);
            return;
        }
        Debug.LogWarning($"[AudioManager] SFX '{id}' not found. Check Sound Effects library.");
    }

    /// <summary>
    /// Switch background music to the track with the given Id.
    /// Example: AudioManager.Instance.PlayMusic("tension_zone2");
    /// </summary>
    public void PlayMusic(string id)
    {
        if (!_musicEnabled || musicSource == null) return;

        foreach (var entry in musicTracks)
        {
            if (entry.id != id || entry.clip == null) continue;
            musicSource.clip   = entry.clip;
            musicSource.loop   = entry.loop;
            musicSource.volume = 1f; // Mixer controls actual volume
            musicSource.Play();
            return;
        }
        Debug.LogWarning($"[AudioManager] Music '{id}' not found. Check Music Tracks library.");
    }

    public void StopMusic()  => musicSource?.Stop();
    public void PauseMusic() => musicSource?.Pause();
    public void ResumeMusic(){ if (_musicEnabled) musicSource?.UnPause(); }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — Volume (wire to UI sliders in the Options panel)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Set master volume (0–1). Wired to Master volume slider.
    /// </summary>
    public void SetMasterVolume(float value)
    {
        _masterVolume = Mathf.Clamp01(value);
        SetMixerVolume(mixerParamMaster, _masterVolume);
        SaveSettings();
    }

    /// <summary>
    /// Set music volume (0–1). Wired to Music volume slider.
    /// </summary>
    public void SetMusicVolume(float value)
    {
        _musicVolume = Mathf.Clamp01(value);
        SetMixerVolume(mixerParamMusic, _musicVolume);
        SaveSettings();
    }

    /// <summary>
    /// Set SFX volume (0–1). Wired to SFX volume slider.
    /// </summary>
    public void SetSFXVolume(float value)
    {
        _sfxVolume = Mathf.Clamp01(value);
        SetMixerVolume(mixerParamSFX, _sfxVolume);
        SaveSettings();
    }

    /// <summary>
    /// Mute or unmute music. Wired to Music toggle.
    /// </summary>
    public void ToggleMusic(bool enabled)
    {
        _musicEnabled = enabled;
        SetMixerVolume(mixerParamMusic, enabled ? _musicVolume : 0f);
        if (!enabled) musicSource?.Pause();
        else          musicSource?.UnPause();
        SaveSettings();
    }

    /// <summary>
    /// Mute or unmute SFX. Wired to SFX toggle.
    /// </summary>
    public void ToggleSFX(bool enabled)
    {
        _sfxEnabled = enabled;
        SetMixerVolume(mixerParamSFX, enabled ? _sfxVolume : 0f);
        SaveSettings();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Mixer Helper
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets an AudioMixer exposed parameter using logarithmic (dB) conversion.
    /// Linear sliders feel unnatural for volume — log conversion gives a
    /// perceptually even volume curve.
    /// volume = 0.001 is used as the floor (-60dB) instead of 0 to avoid log(0).
    /// </summary>
    private void SetMixerVolume(string paramName, float linearVolume)
    {
        if (gameMixer == null) return;
        float dB = Mathf.Log10(Mathf.Max(linearVolume, 0.001f)) * 20f;
        gameMixer.SetFloat(paramName, dB);
    }

    private void ApplyAllVolumesToMixer()
    {
        SetMixerVolume(mixerParamMaster, _masterVolume);
        SetMixerVolume(mixerParamMusic,  _musicEnabled ? _musicVolume : 0f);
        SetMixerVolume(mixerParamSFX,    _sfxEnabled   ? _sfxVolume   : 0f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Settings Persistence
    // ─────────────────────────────────────────────────────────────────────────

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat("MasterVolume",  _masterVolume);
        PlayerPrefs.SetFloat("MusicVolume",   _musicVolume);
        PlayerPrefs.SetFloat("SFXVolume",     _sfxVolume);
        PlayerPrefs.SetInt("MusicEnabled",    _musicEnabled ? 1 : 0);
        PlayerPrefs.SetInt("SFXEnabled",      _sfxEnabled   ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void LoadSettings()
    {
        _masterVolume = PlayerPrefs.GetFloat("MasterVolume", defaultMasterVolume);
        _musicVolume  = PlayerPrefs.GetFloat("MusicVolume",  defaultMusicVolume);
        _sfxVolume    = PlayerPrefs.GetFloat("SFXVolume",    defaultSFXVolume);
        _musicEnabled = PlayerPrefs.GetInt("MusicEnabled",   1) == 1;
        _sfxEnabled   = PlayerPrefs.GetInt("SFXEnabled",     1) == 1;
    }
}
