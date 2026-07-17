using UnityEngine.Scripting;

namespace HealthVision
{
    // Entry point. Loads settings and spawns the persistent client-side overlay that
    // draws health bars over nearby entities while the goggles are worn.
    [Preserve]
    public sealed class ModApi : IModApi
    {
        public void InitMod(Mod modInstance)
        {
            HealthVisionSettings.Load(modInstance?.Path);
            HealthVisionOverlay.Ensure();
            Log.Out("[HealthVision] Loaded. Range=" + HealthVisionSettings.Range);
        }
    }
}
