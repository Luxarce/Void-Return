using UnityEngine;

/// <summary>
/// A single entry in the AudioManager's sound library.
///
/// FIX: Moved out of AudioManager.cs into its own file so the class
/// is globally visible to all other scripts in the project.
/// Previously it was declared inside AudioManager.cs ABOVE the class
/// definition, which caused "type not found" errors in some Unity versions
/// depending on script compilation order.
///
/// HOW TO USE IN INSPECTOR:
///   - In the AudioManager Inspector, expand the Music Tracks or Sound Effects array.
///   - Click + to add an entry.
///   - Set the 'Id' string (e.g., "pickup", "boots_on", "meteorite_impact").
///   - Drag an AudioClip into the 'Clip' field.
///   - Set Volume (0–1) and Loop as needed.
///   - Call AudioManager.Instance.PlaySFX("your_id") from any script.
/// </summary>
[System.Serializable]
public class SoundEntry
{
    [Tooltip("Unique string used to reference this sound in code.\n" +
             "Examples: pickup, boots_on, boots_off, boots_step,\n" +
             "tether_fire, tether_hook, grenade_launch, grenade_detonate,\n" +
             "thruster_active, thruster_empty, meteorite_impact,\n" +
             "meteorite_incoming, rift_start, repair_stage, repair_complete,\n" +
             "oxygen_warning, oxygen_critical, jump,\n" +
             "ui_click, ui_open, ui_notification")]
    public string id;

    [Tooltip("The audio clip to play for this sound.")]
    public AudioClip clip;

    [Tooltip("Base volume for this sound (0–1). " +
             "Multiplied by the global SFX or music volume from the AudioMixer.")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Tooltip("If true, this sound loops until explicitly stopped. " +
             "Only applies when played via a dedicated AudioSource, not PlayOneShot.")]
    public bool loop = false;
}
