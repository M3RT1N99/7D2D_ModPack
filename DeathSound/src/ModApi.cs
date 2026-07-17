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
                + ", Explosion=" + DeathSoundSettings.ExplosionEnabled);
        }

        private static void OnEntityKilled(ref ModEvents.SEntityKilledData data)
        {
            try
            {
                Entity killedEntity = data.KilledEntitiy;

                if (killedEntity == null || killedEntity.entityType != EntityType.Player)
                    return;

                // Explosion is a server-authoritative world event: it fires for any
                // player death (DeathExplosion.Spawn self-guards to run once, on the
                // server) and is independent of the client-side audio gate below.
                if (DeathSoundSettings.ExplosionEnabled)
                    DeathExplosion.Spawn(killedEntity.transform.position);

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
