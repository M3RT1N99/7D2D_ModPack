using System;
using UnityEngine;

namespace DeathSound
{
    public sealed class ModApi : IModApi
    {
        public void InitMod(Mod modInstance)
        {
            DeathSoundSettings.Load(modInstance?.Path);
            DeathSoundPlayer.Ensure();
            ModEvents.EntityKilled.RegisterHandler(OnEntityKilled);

            Log.Out("[DeathSound] Loaded. AudioFile=" + DeathSoundSettings.AudioPath
                + ", ExplosionOnDeath=" + DeathSoundSettings.ExplosionOnDeath);
        }

        private static void OnEntityKilled(ref ModEvents.SEntityKilledData data)
        {
            try
            {
                Entity killedEntity = data.KilledEntitiy;

                if (killedEntity == null || killedEntity.entityType != EntityType.Player)
                    return;

                // The sound is tied to the EXPLOSION (played at trigger time inside
                // DeathExplosion.Spawn), NOT to death itself -- so there is no separate
                // death sound here. On death we only act when ExplosionOnDeath is enabled
                // (off by default; the detonator is the intended trigger). When off, a
                // plain death is silent and does not explode.
                // NOTE: entity.position = authoritative WORLD coords (not transform.position,
                // which is render-space offset by the floating Origin).
                if (DeathSoundSettings.ExplosionOnDeath)
                    DeathExplosion.Spawn(killedEntity.position);
            }
            catch (Exception ex)
            {
                Log.Error("[DeathSound] EntityKilled handler failed: " + ex);
            }
        }
    }
}
