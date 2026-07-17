using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeathSound
{
    // Detonates a large "commander-style" explosion at a player's death position:
    //  - the game's ExplosionServer carves the block crater and applies physics,
    //  - a custom two-tier damage model hits entities (inner fireball / outer shockwave),
    //  - a composite of scaled-up explosion prefabs fakes a big nuclear-style fireball
    //    (flash + shockwave rings + mushroom), since 7DTD has no large-explosion asset.
    // Damage runs server-authoritatively; the visual is replicated to every client via
    // NetPackageDeathExplosion so multiplayer players all see it.
    internal static class DeathExplosion
    {
        private const string Tag = "[DeathSound]";

        internal static void Spawn(Vector3 worldPos)
        {
            // Explosions are authored on the server. If this handler also runs on a
            // connected client, bail so we don't detonate once per client.
            ConnectionManager cm = SingletonMonoBehaviour<ConnectionManager>.Instance;
            if (cm == null || !cm.IsServer)
                return;

            GameManager gm = GameManager.Instance;
            if (gm == null)
                return;

            // Sound plays NOW (trigger time: detonator click / death), decoupled from the
            // explosion, which follows after ExplosionDelaySeconds below.
            PlayDeathSoundEverywhere();

            // Native explosion carves the block crater + physics only. Its own particle
            // is disabled (ParticleIndex 0) because we render our own composite fireball,
            // and entity damage is zero (we apply tiered damage ourselves below).
            ExplosionData data = new ExplosionData
            {
                ParticleIndex = 0,
                BlastPower = DeathSoundSettings.ExplosionBlastPower,
                EntityRadius = 0,
                EntityDamage = 0f,
                BlockRadius = DeathSoundSettings.ExplosionBlockRadius,
                BlockDamage = DeathSoundSettings.ExplosionBlockDamage,
                DamageType = EnumDamageTypes.Heat,
                IgnoreHeatMap = false,
                // Must be non-null: Explosion.AttackBlocks reads BlockTags.Length.
                BlockTags = string.Empty,
            };

            float delay = DeathSoundSettings.ExplosionDelaySeconds;

            // _entityId = -1 -> environmental blast; _delay handled inside ExplosionServer.
            gm.ExplosionServer(
                worldPos,
                World.worldToBlockPos(worldPos),
                Quaternion.identity,
                data,
                -1,
                delay,
                false);

            gm.StartCoroutine(DetonateAfterDelay(worldPos, delay));

            Log.Out(Tag + " Death explosion queued at " + worldPos + " (delay=" + delay + "s)");
        }

        private static IEnumerator DetonateAfterDelay(Vector3 center, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            GameManager gm = GameManager.Instance;
            World world = gm != null ? gm.World : null;
            if (world == null)
                yield break;

            float scale = DeathSoundSettings.ExplosionVisualScale;
            int particleIndex = DeathSoundSettings.ExplosionParticleIndex;
            float footprint = DeathSoundSettings.ExplosionInnerRadius;

            // Render the composite fireball locally so the host / SP player sees it. Skip
            // on a headless dedicated server (no view); clients get it via the broadcast
            // below. The sound already played at trigger time (see Spawn). Do NOT gate on
            // GetPrimaryPlayer() - right after a death it is null.
            if (!GameManager.IsDedicatedServer)
                SpawnComposite(center, scale, particleIndex, footprint);

            // Replicate the full look (center/scale/particle/footprint) AND the sound to
            // every client so the blast looks and sounds identical regardless of local config.
            ConnectionManager cm = SingletonMonoBehaviour<ConnectionManager>.Instance;
            if (cm != null && cm.IsServer)
                cm.SendPackage(NetPackageManager.GetPackage<NetPackageDeathExplosion>()
                    .Setup(center, scale, particleIndex, footprint));

            ApplyTieredDamage(world, center);
        }

        // Plays the death/detonation sound immediately (trigger time): locally on the
        // host / single-player (skip a headless dedicated server) and broadcast to every
        // client, so all players hear it the moment it's triggered -- before the delayed blast.
        private static void PlayDeathSoundEverywhere()
        {
            if (!GameManager.IsDedicatedServer)
                DeathSoundPlayer.Play("Detonation");

            ConnectionManager cm = SingletonMonoBehaviour<ConnectionManager>.Instance;
            if (cm != null && cm.IsServer)
                cm.SendPackage(NetPackageManager.GetPackage<NetPackagePlayDeathSound>().Setup());
        }

        // Server-authoritative two-tier entity damage: a flat inner "fireball" tier and a
        // flat outer "shockwave" tier, each a percentage of the victim's max health.
        private static void ApplyTieredDamage(World world, Vector3 center)
        {
            float innerRadius = DeathSoundSettings.ExplosionInnerRadius;
            float outerRadius = Mathf.Max(innerRadius, DeathSoundSettings.ExplosionOuterRadius);
            float innerFraction = DeathSoundSettings.ExplosionInnerDamagePercent / 100f;
            float outerFraction = DeathSoundSettings.ExplosionOuterDamagePercent / 100f;

            DamageSource source = new DamageSource(EnumDamageSource.External, EnumDamageTypes.Heat);

            // Snapshot: DamageEntity can kill (and remove) entities mid-iteration.
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

        // Renders the composite fireball. Called on the host locally and on each client
        // via NetPackageDeathExplosion. Safe wherever a GameManager exists.
        internal static void SpawnComposite(Vector3 center, float scale, int particleIndex, float footprint)
        {
            GameManager gm = GameManager.Instance;
            if (gm == null)
                return;

            Log.Out(Tag + " Spawning composite fireball at " + center + " (scale=" + scale + ").");
            gm.StartCoroutine(CompositeRoutine(center, scale, particleIndex, footprint));
        }

        // A timed sequence of scaled explosion bursts arranged to read as one large
        // nuclear-style blast: central flash -> expanding ground shockwave -> rising
        // mushroom stem -> cap. "footprint" is how wide (metres) the effect should span.
        private static IEnumerator CompositeRoutine(Vector3 center, float scale, int idx, float footprint)
        {
            Vector3 up = Vector3.up;

            // 1) Central flash.
            SpawnBurst(center, idx, scale * 1.2f);

            // 2) Ground shockwave: two expanding rings.
            yield return new WaitForSeconds(0.05f);
            SpawnRing(center, idx, footprint * 0.35f, 6, scale * 0.45f);
            yield return new WaitForSeconds(0.15f);
            SpawnRing(center, idx, footprint * 0.7f, 8, scale * 0.45f);

            // 3) Rising mushroom stem.
            yield return new WaitForSeconds(0.1f);
            for (int i = 1; i <= 3; i++)
            {
                SpawnBurst(center + up * (footprint * 0.18f * i), idx, scale * 0.5f);
                yield return new WaitForSeconds(0.08f);
            }

            // 4) Mushroom cap.
            SpawnBurst(center + up * (footprint * 0.65f), idx, scale * 1.1f);
        }

        private static void SpawnRing(Vector3 center, int index, float radius, int count, float scale)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.PI * 2f * i / count;
                Vector3 pos = center + new Vector3(Mathf.Cos(angle) * radius, 1f, Mathf.Sin(angle) * radius);
                SpawnBurst(pos, index, scale);
            }
        }

        // Instantiates one scaled-up copy of an explosion prefab at a world position.
        // Particle audio is muted so 20-odd bursts don't stack into a wall of noise; the
        // mod's own death sound is the intended audio.
        private static void SpawnBurst(Vector3 worldPos, int index, float scale)
        {
            Transform[] prefabs = WorldStaticData.prefabExplosions;
            if (index <= 0 || prefabs == null || index >= prefabs.Length || prefabs[index] == null)
                return;

            // Prefabs live in render space (world minus the floating Origin).
            GameObject go = GameObject.Instantiate(prefabs[index].gameObject, worldPos - Origin.position, Quaternion.identity);

            if (scale > 1f)
            {
                // Transform scale only enlarges particles under Hierarchy scaling.
                ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < systems.Length; i++)
                {
                    ParticleSystem.MainModule main = systems[i].main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }

                go.transform.localScale = Vector3.one * scale;
            }

            AudioSource[] audio = go.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audio.Length; i++)
                audio[i].enabled = false;

            // Safety net in case the scaled copy outlives the prefab's own TemporaryObject.
            GameObject.Destroy(go, 8f);
        }
    }
}
