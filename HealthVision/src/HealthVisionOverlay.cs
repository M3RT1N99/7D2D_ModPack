using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace HealthVision
{
    // Persistent, client-side, read-only HUD. While the local player wears the
    // HealthVisionGoggles, it draws a health bar (+ optional number) above every nearby
    // living entity. Purely visual -> no server/networking; each client only sees bars
    // while it wears its own goggles.
    //
    // Floating-origin note: RANGE tests use world coords (entity.position vs player), but
    // SCREEN projection must use render-space (world - Origin.position), because the camera
    // lives in render space. Mixing the two is the classic bug.
    [Preserve]
    public sealed class HealthVisionOverlay : MonoBehaviour
    {
        private const string GogglesItemName = "HealthVisionGoggles";

        private Texture2D white;
        private Texture2D black;
        private GUIStyle labelStyle;

        // Cheap reusable buffer for gathering entities each frame.
        private readonly List<EntityAlive> targets = new List<EntityAlive>();

        internal static void Ensure()
        {
            if (FindObjectOfType<HealthVisionOverlay>() != null)
                return;

            GameObject go = new GameObject("HealthVisionOverlay");
            DontDestroyOnLoad(go);
            go.AddComponent<HealthVisionOverlay>();
        }

        private void Awake()
        {
            white = new Texture2D(1, 1);
            white.SetPixel(0, 0, Color.white);
            white.Apply();

            black = new Texture2D(1, 1);
            black.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
            black.Apply();

            labelStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
            labelStyle.normal.textColor = Color.white;
        }

        private static bool WearingGoggles(EntityAlive player)
        {
            Equipment eq = player != null ? player.equipment : null;
            if (eq == null)
                return false;

            for (int i = 0; i < eq.GetSlotCount(); i++)
            {
                ItemValue iv = eq.GetSlotItem(i);
                if (iv != null && !iv.IsEmpty() && iv.ItemClass != null
                    && iv.ItemClass.GetItemName() == GogglesItemName)
                    return true;
            }

            return false;
        }

        private void OnGUI()
        {
            // Immediate-mode GUI runs on Layout AND Repaint; only draw on Repaint.
            if (Event.current == null || Event.current.type != EventType.Repaint)
                return;

            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (world == null)
                return;

            EntityPlayerLocal player = world.GetPrimaryPlayer();
            if (player == null || !WearingGoggles(player))
                return;

            Camera cam = player.playerCamera != null ? player.playerCamera : Camera.main;
            if (cam == null)
                return;

            float range = HealthVisionSettings.Range;
            float rangeSq = range * range;
            bool showPlayers = HealthVisionSettings.ShowPlayers;
            Vector3 playerWorld = player.position;

            // Gather living, in-range entities.
            targets.Clear();
            List<Entity> all = world.Entities.list;
            for (int i = 0; i < all.Count; i++)
            {
                EntityAlive a = all[i] as EntityAlive;
                if (a == null || !a.IsAlive())
                    continue;
                if (a.entityId == player.entityId)
                    continue;
                if (!showPlayers && a is EntityPlayer)
                    continue;
                if ((a.position - playerWorld).sqrMagnitude > rangeSq)
                    continue;

                targets.Add(a);
            }

            float barWidth = HealthVisionSettings.BarWidth;
            float barHeight = HealthVisionSettings.BarHeight;
            bool showNumbers = HealthVisionSettings.ShowNumbers;
            labelStyle.fontSize = HealthVisionSettings.FontSize; // config-driven each frame
            float labelHeight = HealthVisionSettings.FontSize + 8f;

            for (int i = 0; i < targets.Count; i++)
            {
                EntityAlive a = targets[i];

                // Render-space head anchor (camera is in render space).
                Vector3 head = a.getHeadPosition() - Origin.position + Vector3.up * 0.3f;
                Vector3 sp = cam.WorldToScreenPoint(head);
                if (sp.z <= 0f)
                    continue; // behind the camera

                float x = sp.x;
                float y = Screen.height - sp.y; // flip to GUI (top-left) origin
                if (x < 0f || x > Screen.width || y < 0f || y > Screen.height)
                    continue;

                int max = Mathf.Max(1, a.GetMaxHealth());
                int cur = Mathf.Clamp(a.Health, 0, max);
                float frac = (float)cur / max;

                // Dark background (acts as a border) + coloured fill inset by 2px.
                Rect bg = new Rect(x - barWidth * 0.5f, y - barHeight - 10f, barWidth, barHeight);
                GUI.DrawTexture(bg, black);

                GUI.color = Color.Lerp(Color.red, Color.green, frac);
                GUI.DrawTexture(new Rect(bg.x + 2f, bg.y + 2f, (barWidth - 4f) * frac, barHeight - 4f), white);
                GUI.color = Color.white;

                if (showNumbers)
                    GUI.Label(new Rect(x - barWidth * 0.5f, bg.y - labelHeight, barWidth, labelHeight), cur + "/" + max, labelStyle);
            }
        }
    }
}
