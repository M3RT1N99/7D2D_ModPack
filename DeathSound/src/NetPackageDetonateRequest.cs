using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace DeathSound
{
    // Client -> server: a remote client holding the detonator asks the server to detonate.
    // SECURITY: the server derives the blast from the SENDER's own server-side entity
    // position (never a client-supplied coordinate) and rate-limits per sender, so a
    // modified client cannot blast arbitrary map locations or spam. The explosion
    // (damage + replicated visual/sound) is produced server-side by DeathExplosion.Spawn.
    [Preserve]
    public class NetPackageDetonateRequest : NetPackage
    {
        private const float CooldownSeconds = 3f;
        private static readonly Dictionary<int, float> lastDetonate = new Dictionary<int, float>();

        public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

        public NetPackageDetonateRequest Setup()
        {
            return this;
        }

        public override void read(PooledBinaryReader _br)
        {
            // No payload: the server uses the sender's own entity, not client data.
        }

        public override void write(PooledBinaryWriter _bw)
        {
            base.write(_bw);
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (_world == null || Sender == null)
                return;

            EntityAlive attacker = _world.GetEntity(Sender.entityId) as EntityAlive;
            if (attacker == null || attacker.IsDead())
                return;

            // Server-authoritative rate limit (the client-side cooldown is only UX).
            float now = Time.unscaledTime;
            if (lastDetonate.TryGetValue(Sender.entityId, out float last) && now - last < CooldownSeconds)
                return;
            lastDetonate[Sender.entityId] = now;

            // Blast at the sender's real position; ignore any client-supplied coordinate.
            DeathExplosion.Spawn(attacker.position);
        }

        public override int GetLength()
        {
            return 8;
        }
    }
}
