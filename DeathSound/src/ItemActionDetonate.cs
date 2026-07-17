using UnityEngine;
using UnityEngine.Scripting;

namespace DeathSound
{
    // Custom item action: pressing primary (left-click) with the detonator equipped
    // triggers the DeathSound explosion at the wielder's position.
    //
    // Pairs with the "C4 bomb vest": by default the detonator only fires while the vest
    // (BombVest) is worn, and the vest is consumed on detonation. Both are
    // configurable (DetonatorRequiresVest / DetonatorConsumesVest).
    //
    // 7DTD instantiates this from item XML via
    //   <property name="Class" value="DeathSound.ItemActionDetonate, DeathSound"/>
    // resolved through ReflectionHelpers.GetTypeWithPrefix, which scans mod assemblies.
    // Only ExecuteAction is abstract on ItemAction; the base handles the rest.
    [Preserve]
    public class ItemActionDetonate : ItemAction
    {
        private const string VestItemName = "BombVest";

        // Shared cooldown so holding the button doesn't detonate every frame.
        private static float nextAllowedTime;

        public override void ExecuteAction(ItemActionData _actionData, bool _bReleased)
        {
            // Fire on press, not on release.
            if (_bReleased)
                return;

            if (_actionData == null || _actionData.invData == null)
                return;

            EntityAlive holder = _actionData.invData.holdingEntity;
            if (holder == null)
                return;

            // The vest IS the bomb: without it there is nothing to set off.
            bool wearingVest = IsWearingBombVest(holder);
            if (DeathSoundSettings.DetonatorRequiresVest && !wearingVest)
                return;

            if (Time.unscaledTime < nextAllowedTime)
                return;
            nextAllowedTime = Time.unscaledTime + 3f;

            if (wearingVest && DeathSoundSettings.DetonatorConsumesVest)
                ConsumeBombVest(holder);

            Vector3 pos = holder.position;

            // The explosion is server-authoritative: on the host/single-player detonate
            // directly; a remote client asks the server to do it.
            ConnectionManager cm = SingletonMonoBehaviour<ConnectionManager>.Instance;
            if (cm == null)
                return;

            if (cm.IsServer)
            {
                DeathExplosion.Spawn(pos);
            }
            else
            {
                // Remote client: just request it. The server ignores our coords and uses
                // our own entity position; the vest/cooldown checks above are local UX only.
                cm.SendToServer(NetPackageManager.GetPackage<NetPackageDetonateRequest>().Setup());
            }
        }

        // True if the entity has the bomb vest equipped in any armor slot.
        private static bool IsWearingBombVest(EntityAlive holder)
        {
            Equipment eq = holder != null ? holder.equipment : null;
            if (eq == null)
                return false;

            for (int i = 0; i < eq.GetSlotCount(); i++)
            {
                ItemValue iv = eq.GetSlotItem(i);
                if (iv != null && !iv.IsEmpty() && iv.ItemClass != null
                    && iv.ItemClass.GetItemName() == VestItemName)
                    return true;
            }

            return false;
        }

        // Unequip + destroy the worn bomb vest (SetSlotItem also flags the change for MP sync).
        private static void ConsumeBombVest(EntityAlive holder)
        {
            Equipment eq = holder != null ? holder.equipment : null;
            if (eq == null)
                return;

            for (int i = eq.GetSlotCount() - 1; i >= 0; i--)
            {
                ItemValue iv = eq.GetSlotItem(i);
                if (iv != null && iv.ItemClass != null
                    && iv.ItemClass.GetItemName() == VestItemName)
                    eq.SetSlotItem(i, null);
            }
        }
    }
}
