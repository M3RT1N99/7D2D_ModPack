using UnityEngine;
using UnityEngine.Scripting;

namespace DeathSound
{
    // Replicates the big composite fireball (and its sound) from the server to every
    // client so all players see AND hear it, not just the host. Damage stays
    // server-authoritative and is NOT carried here. 7 Days To Die auto-discovers
    // NetPackage subclasses in loaded mod assemblies, so this registers itself.
    //
    // The full look is serialized (center, scale, particleIndex, footprint) so every
    // client renders exactly what the server authored, even if their DeathSound.xml differs.
    [Preserve]
    public class NetPackageDeathExplosion : NetPackage
    {
        private Vector3 center;
        private float scale;
        private int particleIndex;
        private float footprint;

        public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

        public NetPackageDeathExplosion Setup(Vector3 _center, float _scale, int _particleIndex, float _footprint)
        {
            center = _center;
            scale = _scale;
            particleIndex = _particleIndex;
            footprint = _footprint;
            return this;
        }

        public override void read(PooledBinaryReader _br)
        {
            center = StreamUtils.ReadVector3(_br);
            scale = _br.ReadSingle();
            particleIndex = _br.ReadInt32();
            footprint = _br.ReadSingle();
        }

        public override void write(PooledBinaryWriter _bw)
        {
            base.write(_bw);
            StreamUtils.Write(_bw, center);
            _bw.Write(scale);
            _bw.Write(particleIndex);
            _bw.Write(footprint);
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (_world == null)
                return;

            // Visual only on the receiving client; the server applied damage, and the
            // sound is sent separately at trigger time (NetPackagePlayDeathSound).
            DeathExplosion.SpawnComposite(center, scale, particleIndex, footprint);
        }

        public override int GetLength()
        {
            // Vector3 (12) + float (4) + int (4) + float (4), plus header slack.
            return 40;
        }
    }
}
