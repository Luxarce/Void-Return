using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// Sound Entry Data Class
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A single entry in the AudioManager's sound library.
/// Set the Id string and drag in the AudioClip in the Inspector.
/// Call AudioManager.Instance.PlaySFX("your_id") from any script to play it.
/// </summary>
[System.Serializable]
public class SoundEntry
{
    [Tooltip("Unique string identifier used to reference this sound in code. " +
             "Example: 'pickup', 'boots_activate', 'meteorite_impact'.")]
    public string id;

    [Tooltip("The audio clip to play for this sound.")]
    public AudioClip clip;

    [Tooltip("Base volume for this specific sound (0 to 1). " +
             "This is multiplied by the global SFX/music volume.")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Tooltip("If true, this sound loops until explicitly stopped.")]
    public bool loop = false;
}

// ─────────────────────────────────────────────────────────────────────────────
// AudioManager Singleton
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Central audio manager. Handles music playback, SFX playback by ID,
/// ambient background sound, and volume/toggle settings.
///
/// Persists across scenes via DontDestroyOnLoad.
/// Create one instance in the MainMenu scene. Do not duplicate in GameScene.
///
/// SETUP:
/// 1. Create an empty GameObject named 'AudioManager'.
/// 2. Attach this script.
/// 3. Create two child GameObjects: 'MusicSource' and 'SFXSource'.
/// 4. Add an AudioSource component to each child.
/// 5. Drag each AudioSource into the musicSource and sfxSource fields below.
/// 6. Populate the musicTracks and soundEffects arrays with your clips.
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

    [Header("Audio Sources — Drag child AudioSource components here")]
    [Tooltip("AudioSource used for background music and ambient loops. " +
             "Enable 'Loop' on the component itself.")]
    public AudioSource musicSource;

    [Tooltip("AudioSource used for sound effects (PlayOneShot calls). " +
             "Do NOT enable Loop on this component.")]
    public AudioSource sfxSource;

    [Header("Ambient Sound")]
    [Tooltip("Clip that plays immediately on Start as the ambient space background loop. " +
             "Leave empty if you want to trigger ambient music manually with PlayMusic().")]
    public AudioClip ambientClip;

    [Tooltip("Volume of the ambient background loop (0 to 1).")]
    [Range(0f, 1f)]
    public float ambientVolume = 0.35f;

    [Header("Music Tracks Library")]
    [Tooltip("All music tracks. Give each a unique Id and assign the AudioClip. " +
             "Call PlayMusic('your_id') to switch tracks at runtime.")]
    public SoundEntry[] musicTracks;

    [Header("Sound Effects Library")]
    [Tooltip("All sound effects. Give each a unique Id and assign the AudioClip. " +
             "Call PlaySFX('your_id') to play a sound at runtime.\n\n" +
             "Suggested IDs:\n" +
             "pickup, canister_pickup,\n" +
             "boots_on, boots_off, boots_step,\n" +
             "tether_fire, tether_hook, tether_release,\n" +
             "grenade_launch, grenade_detonate,\n" +
             "thruster_active, thruster_empty,\n" +
             "meteorite_impact, meteorite_incoming,\n" +
             "rift_start, rift_disorient,\n" +
             "repair_stage, repair_complete,\n" +
             "oxygen_warning, oxygen_critical,\n" +
             "jump, ui_click, ui_open, ui_notification")]
    public SoundEntry[] soundEffects;

    [Header("Default Volumes")]
    [Tooltip("Starting music volume (0 to 1). Loaded from PlayerPrefs if a saved value exists.")]
    [Range(0f, 1f)]
    public float defaultMusicVolume = 0.8f;

    [Tooltip("Starting SFX volume (0 to 1). Loaded from PlayerPrefs if a saved value exists.")]
    [Range(0f, 1f)]
    public float defaultSFXVolume = 1f;

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private float _musicVolume;
    private float _sfxVolume;
    private bool  _musicEnabled = true;
    private bool  _sfxEnabled   = true;

    // ─────────────────────────────────────────────────────────────────────────
    // Public Properties (read-only, used by UI sliders)
    // ─────────────────────────────────────────────────────────────────────────

    public float MusicVolume => _musicVolume;
    public float SFXVolume   => _sfxVolume;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSettings();
    }

    private void Start()
    {
        // Apply loaded volumes to sources
        if (musicSource != null) musicSource.volume = _musicVolume;

        // Start ambient background loop immediately if assigned
        if (ambientClip != null && musicSource != null)
        {
            musicSource.clip   = ambientClip;
            musicSource.loop   = true;
            musicSource.volume = ambientVolume * _musicVolume;
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

            sfxSource.PlayOneShot(entry.clip, entry.volume * _sfxVolume);
            return;
        }

        Debug.LogWarning($"[AudioManager] SFX with id '{id}' not found. Check your Sound Effects library.");
    }

    /// <summary>
    /// Switch the background music to a track by its string Id.
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
            musicSource.volume = entry.volume * _musicVolume;
            musicSource.Play();
            return;
        }

        Debug.LogWarning($"[AudioManager] Music track with id '{id}' not found. Check your Music Tracks library.");
    }

    /// <summary>Stop the current music track.</summary>
    public void StopMusic()
    {
        musicSource?.Stop();
    }

    /// <summary>Pause the current music track (can be resumed).</summary>
    public void PauseMusic()
    {
        musicSource?.Pause();
    }

    /// <summary>Resume a paused music track.</summary>
    public void ResumeMusic()
    {
        if (_musicEnabled) musicSource?.UnPause();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — Volume & Toggle (wired to UI sliders and toggles)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Set the global music volume. Wired to the music volume slider in the Inspector.
    /// </summary>
    public void SetMusicVolume(float value)
    {
        _musicVolume = Mathf.Clamp01(value);
        if (musicSource != null) musicSource.volume = _musicVolume;
        SaveSettings();
    }

    /// <summary>
    /// Set the global SFX volume. Wired to the SFX volume slider in the Inspector.
    /// </summary>
    public void SetSFXVolume(float value)
    {
        _sfxVolume = Mathf.Clamp01(value);
        SaveSettings();
    }

    /// <summary>
    /// Enable or disable all music. Wired to the music Toggle in the Inspector.
    /// </summary>
    public void ToggleMusic(bool enabled)
    {
        _musicEnabled = enabled;
        if (enabled)
            musicSource?.UnPause();
        else
            musicSource?.Pause();

        SaveSettings();
    }

    /// <summary>
    /// Enable or disable all SFX. Wired to the SFX Toggle in the Inspector.
    /// </summary>
    public void ToggleSFX(bool enabled)
    {
        _sfxEnabled = enabled;
        SaveSettings();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Settings Persistence (PlayerPrefs)
    // ─────────────────────────────────────────────────────────────────────────

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat("MusicVolume",  _musicVolume);
        PlayerPrefs.SetFloat("SFXVolume",    _sfxVolume);
        PlayerPrefs.SetInt("MusicEnabled",   _musicEnabled ? 1 : 0);
        PlayerPrefs.SetInt("SFXEnabled",     _sfxEnabled   ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void LoadSettings()
    {
        _musicVolume  = PlayerPrefs.GetFloat("MusicVolume", defaultMusicVolume);
        _sfxVolume    = PlayerPrefs.GetFloat("SFXVolume",   defaultSFXVolume);
        _musicEnabled = PlayerPrefs.GetInt("MusicEnabled", 1) == 1;
        _sfxEnabled   = PlayerPrefs.GetInt("SFXEnabled",   1) == 1;
    }
}
