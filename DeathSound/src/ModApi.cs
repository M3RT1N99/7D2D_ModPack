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

                // Setzt einen Timer, um nach 2 Sekunden eine Explosion mit Feuerball-Effekt zu spawnen
                StartCoroutine(ExplodeAfterDelay(killedEntity.transform.position));
            }
            catch (Exception ex)
            {
                Log.Error("[DeathSound] EntityKilled handler failed: " + ex);
            }
        }

        private static IEnumerator ExplodeAfterDelay(Vector3 position)
        {
            yield return new WaitForSeconds(2f);

            // Spawnt die Explosion mit einem Feuerball-Effekt, Hauptradius 50m, Außerradius 150m, Hauptschaden 95%, Außerschaden 50%
            FireBallExplosion.SpawnFireBallExplosion(position, 50f, 150f, 95f, 50f);
        }
    }

    public class FireBallExplosion : MonoBehaviour
    {
        public static void SpawnFireBallExplosion(Vector3 position, float mainRadius, float outerRadius, float mainDamage, float outerDamage)
        {
            // Code zur Auslösung des Feuerball-Effekts und des Schadens
            // Beispiel:
            GameObject fireBall = Instantiate(fireBallPrefab, position, Quaternion.identity);
            fireBall.GetComponent<FireBall>().MainRadius = mainRadius;
            fireBall.GetComponent<FireBall>().OuterRadius = outerRadius;
            fireBall.GetComponent<FireBall>().MainDamage = mainDamage;
            fireBall.GetComponent<FireBall>().OuterDamage = outerDamage;
        }
    }
}
