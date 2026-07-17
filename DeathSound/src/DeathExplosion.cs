using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeathSound
{
    // Spawns a fireball explosion at a player's death position using 7 Days To Die's
    // own explosion system for the VISUAL / physics / block crater, and applies a
    // custom two-tier damage model to entities (an inner "fireball" and an outer
    // "shockwave" ring). The engine's native explosion only supports a single radius
    // with linear falloff, so the two flat tiers are applied here by hand.
    internal static class DeathExplosion
    {
        private const string Tag = "[DeathSound]";

        internal static void Spawn(Vector3 worldPos)
        {
            // Explosions are authored on the server, which replicates them to clients.
            // If this handler also runs on a connected client, bail out so we don't
            // detonate once per client.
            ConnectionManager cm = SingletonMonoBehaviour<ConnectionManager>.Instance;
            if (cm == null || !cm.IsServer)
                return;

            GameManager gm = GameManager.Instance;
            if (gm == null)
                return;

            // Native explosion: fireball visual + physics + optional block crater only.
            // EntityRadius/EntityDamage are zeroed because we apply our own tiered entity
            // damage below (the engine can't express two flat damage tiers).
            ExplosionData data = new ExplosionData
            {
                ParticleIndex = DeathSoundSettings.ExplosionParticleIndex,
                BlastPower = DeathSoundSettings.ExplosionBlastPower,
                EntityRadius = 0,
                EntityDamage = 0f,
                BlockRadius = DeathSoundSettings.ExplosionBlockRadius,
                BlockDamage = DeathSoundSettings.ExplosionBlockDamage,
                DamageType = EnumDamageTypes.Heat,
                IgnoreHeatMap = false,
                // Must be non-null: Explosion.AttackBlocks reads BlockTags.Length, and a
                // zero-initialised ExplosionData leaves it null (which would throw).
                BlockTags = string.Empty,
            };

            float delay = DeathSoundSettings.ExplosionDelaySeconds;

            // _entityId = -1 -> environmental blast (no attacker attribution).
            // _delay is handled inside ExplosionServer, so no manual wait for the visual.
            gm.ExplosionServer(
                worldPos,
                World.worldToBlockPos(worldPos),
                Quaternion.identity,
                data,
                -1,
                delay,
                false);

            // Apply the tiered entity damage in sync with the visual (same delay).
            gm.StartCoroutine(ApplyTieredDamageAfterDelay(worldPos, delay));

            Log.Out(Tag + " Death explosion queued at " + worldPos + " (delay=" + delay + "s)");
        }

        private static IEnumerator ApplyTieredDamageAfterDelay(Vector3 center, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (world == null)
                yield break;

            float innerRadius = DeathSoundSettings.ExplosionInnerRadius;
            float outerRadius = Mathf.Max(innerRadius, DeathSoundSettings.ExplosionOuterRadius);
            float innerFraction = DeathSoundSettings.ExplosionInnerDamagePercent / 100f;
            float outerFraction = DeathSoundSettings.ExplosionOuterDamagePercent / 100f;

            DamageSource source = new DamageSource(EnumDamageSource.External, EnumDamageTypes.Heat);

            // Snapshot the entity list: DamageEntity can kill (and remove) entities,
            // which would mutate world.Entities.list while we iterate it. Deaths are
            // infrequent, so a linear scan over a copy is cheap and robust, and avoids
            // any chunk/type-matching subtleties.
            List<Entity> entities = new List<Entity>(world.Entities.list);
            int hitCount = 0;

            for (int i = 0; i < entities.Count; i++)
            {
                EntityAlive alive = entities[i] as EntityAlive;
                if (alive == null || alive.IsDead())
                    continue;

                float distance = Vector3.Distance(alive.position, center);

                float fraction;
                if (distance <= innerRadius)
                    fraction = innerFraction;
                else if (distance <= outerRadius)
                    fraction = outerFraction;
                else
                    continue;

                if (fraction <= 0f)
                    continue;

                int damage = Mathf.CeilToInt(alive.GetMaxHealth() * fraction);
                if (damage <= 0)
                    continue;

                alive.DamageEntity(source, damage, false, 1f);
                hitCount++;
            }

            Log.Out(Tag + " Explosion hit " + hitCount + " entities within " + outerRadius + "m.");
        }
    }
}
