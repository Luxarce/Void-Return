using UnityEngine;

/// <summary>
/// Relay script that forwards ShipModule.onProgressChanged (float)
/// to GameHUD.UpdateModuleProgress(int, float) with the correct module index.
///
/// SETUP:
/// 1. Attach this to each ShipModule GameObject.
/// 2. Set moduleIndex: 0=LifeSupport, 1=HullPlating, 2=Navigation, 3=EngineCore.
/// 3. Wire: ShipModule.onProgressChanged (UnityEvent<float>) → ModuleProgressRelay.ForwardProgress
/// </summary>
public class ModuleProgressRelay : MonoBehaviour
{
    [Tooltip("Index of this module in the HUD progress bar array.\n" +
             "0 = Life Support\n1 = Hull Plating\n2 = Navigation\n3 = Engine Core")]
    [Range(0, 3)]
    public int moduleIndex;

    /// <summary>
    /// Wire ShipModule.onProgressChanged → this method in the Inspector.
    /// Forwards the 0–1 progress value to the GameHUD with the correct module index.
    /// </summary>
    public void ForwardProgress(float progress)
    {
        FindFirstObjectByType<GameHUD>()?.UpdateModuleProgress(moduleIndex, progress);
    }
}
