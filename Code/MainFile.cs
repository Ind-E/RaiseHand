using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace RaiseHand;

[ModInitializer(nameof(Initialize))]
partial class MainFile : Node
{
    const string ModId = "RaiseHand";
    static Logger Logger { get; } = new(ModId, LogType.Generic);

    static Vector2 BaseScale;
    static float OffsetY;

    static readonly Vector2 HandOffset = new Vector2(0, 93);

    static SceneTree? tree;

    static void Initialize()
    {
        BaseScale = (Vector2)AccessTools.Field(typeof(HandPosHelper), "_baseScale").GetValue(null);
        OffsetY = (float)AccessTools.Field(typeof(HandPosHelper), "_offsetY").GetValue(null);

        Harmony harmony = new(ModId);

        harmony.PatchAll();

        tree = Engine.GetMainLoop() as SceneTree;
        if (tree is not null)
        {
            tree.NodeAdded += OnNodeAdded;
        }
    }

    static async void OnNodeAdded(Node node)
    {
        if (tree is null || node is not Control control || control.Name != "CardHolderContainer")
            return;

        if (!control.IsNodeReady())
            await tree.ToSignal(node, Node.SignalName.Ready);

        control.Position -= HandOffset;
    }

    [HarmonyPatch(typeof(NHandCardHolder), nameof(NHandCardHolder.SetTargetAngle))]
    class NoRotationPatch
    {
        static void Prefix(ref float angle) => angle = 0f;
    }

    [HarmonyPatch(typeof(NHandCardHolder), nameof(NHandCardHolder.SetTargetPosition))]
    class OffsetHandPatch
    {
        static void Prefix(NHandCardHolder __instance, ref Vector2 position)
        {
            var hand = NPlayerHand.Instance;
            if (hand == null || hand.FocusedHolder == __instance || hand.IsAwaitingPlay(__instance))
                return;

            float visualHeight = __instance.Hitbox.Size.Y;
            int handSize = hand.ActiveHolders.Count;
            Vector2 currentScale = HandPosHelper.GetScale(handSize);

            float halfHeightBase = (visualHeight * BaseScale.X) / 2f;
            float halfHeightCurrent = (visualHeight * currentScale.Y) / 2f;
            float pushDownAmount = halfHeightBase - halfHeightCurrent;

            position.Y = OffsetY + pushDownAmount;
        }
    }

    [HarmonyPatch(typeof(HandPosHelper), nameof(HandPosHelper.GetScale))]
    class GetScaleOverridePatch
    {
        static bool Prefix(int handSize, ref Vector2 __result)
        {
            var multiplier = handSize switch
            {
                6 => 0.6f,
                7 => 0.55f,
                8 => 0.5f,
                9 => 0.45f,
                10 => 0.4f,
                _ => 0.65f,
            };
            __result = BaseScale * multiplier;

            return true;
        }
    }

    [HarmonyPatch(typeof(NPlayerHand), "RefreshLayout")]
    class UnOffsetHoveredCardsPatch
    {
        static void Postfix(NPlayerHand __instance)
        {
            if (__instance.FocusedHolder is not NHandCardHolder focusedHolder)
                return;

            Vector2 hoveredPos = focusedHolder.Position + HandOffset;

            focusedHolder.Position = hoveredPos;
            focusedHolder.SetTargetPosition(hoveredPos);
        }
    }
}
