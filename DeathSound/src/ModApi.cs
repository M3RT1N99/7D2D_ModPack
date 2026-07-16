using System;

namespace DeathSound
{
    public sealed class ModApi : IModApi
    {
        public void InitMod(Mod modInstance)
        {
            DeathSoundSettings.Load(modInstance?.Path);
            DeathSoundPlayer.Ensure();
            ModEvents.EntityKilled.RegisterHandler(OnEntityKilled);

            Log.Out("[DeathSound] Loaded. AudioFile=" + DeathSoundSettings.AudioPath);
        }

        private static void OnEntityKilled(ref ModEvents.SEntityKilledData data)
        {
            try
            {
                Entity killedEntity = data.KilledEntitiy;

                if (killedEntity == null || killedEntity.entityType != EntityType.Player)
                    return;

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
