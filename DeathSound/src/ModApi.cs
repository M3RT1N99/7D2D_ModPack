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

                // Optional: also explode on death (off by default -- the detonator item
                // is the intended trigger). DeathExplosion.Spawn self-guards to run once,
                // on the server, and is independent of the client-side audio gate below.
                // NOTE: use entity.position (authoritative WORLD coords), not
                // transform.position, which is render-space offset by the floating
                // Origin and would place the blast hundreds of metres off.
                if (DeathSoundSettings.ExplosionOnDeath)
                    DeathExplosion.Spawn(killedEntity.position);

                // Audio: only for the local player's death unless configured otherwise.
                if (!DeathSoundSettings.PlayForRemotePlayers
                    && !(killedEntity is EntityPlayerLocal))
                    return;

                DeathSoundPlayer.Play(killedEntity.GetDebugName());
            }
            catch (Exception ex)
            {
                Log.Error("[DeathSound] EntityKilled handler failed: " + ex);
            }
        }
    }
}
