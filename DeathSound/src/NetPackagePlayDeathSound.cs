using UnityEngine.Scripting;

namespace DeathSound
{
    // ToClient: play the custom death/detonation sound on the receiving client.
    // Sent at trigger time (detonator click / death) so the sound is heard immediately,
    // decoupled from the delayed explosion. Auto-registers like the other NetPackages.
    [Preserve]
    public class NetPackagePlayDeathSound : NetPackage
    {
        public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

        public NetPackagePlayDeathSound Setup()
        {
            return this;
        }

        public override void read(PooledBinaryReader _br)
        {
        }

        public override void write(PooledBinaryWriter _bw)
        {
            base.write(_bw);
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (_world == null)
                return;

            DeathSoundPlayer.Play("Detonation");
        }

        public override int GetLength()
        {
            return 8;
        }
    }
}
