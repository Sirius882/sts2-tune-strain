#nullable enable
using System.Threading.Tasks;
using System.Threading;
using AemeathWw.Scripts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace TuneStrain.Patches;

/// <summary>
/// 在 Aemeath 原版谐度破坏结算后接管集谐处决。
/// 设计文档：带有集谐·偏移的怪物被谐度破坏（集谐处决）时，除原本的处决效果外，
/// 清空集谐·偏移并转化为等量的集谐·干涉 + 力量奖励 + 响应 power 覆盖 + 清临时响应。
///
/// 多人同步保护：
/// - 用 Postfix + ref Task __result 包装，await (original ?? Task.CompletedTask) 再做集谐处决，
///   与 Denia 既有的多人安全模式一致（见 memory.md #74）。
/// - 不在前置 Prefix 中改 __result，避免与 Denia 既有补丁冲突。
/// - 所有游戏命令通过 await 顺序执行，不走 fire-and-forget。
/// </summary>
[HarmonyPatch(typeof(AemeathDetuneState), nameof(AemeathDetuneState.TriggerRupture))]
public static class TuneStrainRupturePatch
{
    public static void Postfix(ref Task __result, Creature target, Creature applier, CardModel? source)
    {
        // 只在目标有集谐·偏移、且（对 TriggerRupture）偏谐已满时接管
        int bias = TuneStrainState.GetBias(target);
        if (bias <= 0) return;
        if (!TuneStrainRuptureGuard.CanResolve(target, requireOffTuneCap: true)) return;
        __result = ResolveAfterRupture(__result, target, applier, source, bias);
    }

    private static async Task ResolveAfterRupture(
        Task original, Creature target, Creature applier, CardModel? source, int bias)
    {
        await (original ?? Task.CompletedTask);
        if (target.IsDead) return;
        // 二次确认偏移仍在（原版谐度破坏不会动集谐·偏移，但稳妥起见）
        if (TuneStrainState.GetBias(target) <= 0) return;
        await TuneStrainState.ResolveRupture(target, applier, source);
    }
}

[HarmonyPatch(typeof(AemeathDetuneState), nameof(AemeathDetuneState.TriggerUnconditionalRupture))]
public static class TuneStrainUnconditionalRupturePatch
{
    public static void Postfix(ref Task __result, Creature target, Creature applier, CardModel? source)
    {
        int bias = TuneStrainState.GetBias(target);
        if (bias <= 0) return;
        if (!TuneStrainRuptureGuard.CanResolve(target, requireOffTuneCap: false)) return;
        __result = ResolveAfterRupture(__result, target, applier, source, bias);
    }

    private static async Task ResolveAfterRupture(
        Task original, Creature target, Creature applier, CardModel? source, int bias)
    {
        await (original ?? Task.CompletedTask);
        if (target.IsDead) return;
        if (TuneStrainState.GetBias(target) <= 0) return;
        await TuneStrainState.ResolveRupture(target, applier, source);
    }
}

/// <summary>集谐处决前置判定（与 Denia 旧实现保持一致的可处决性检查）。</summary>
internal static class TuneStrainRuptureGuard
{
    public static bool CanResolve(Creature target, bool requireOffTuneCap) =>
        !target.IsDead
        && target.GetPower<MegaCrit.Sts2.Core.Models.Powers.IllusionPower>() == null
        && (!requireOffTuneCap || AemeathDetuneState.IsAtOffTuneMax(target));
}

[HarmonyPatch(typeof(AemeathRuptureDamageRule), nameof(AemeathRuptureDamageRule.Apply))]
public static class TuneStrainRuptureDamagePatch
{
    public static void Prefix() => TuneStrainRuptureDamageScope.Enter();

    public static void Postfix(ref Task __result)
    {
        __result = Wrap(__result);
    }

    private static async Task Wrap(Task original)
    {
        try
        {
            await (original ?? Task.CompletedTask);
        }
        finally
        {
            TuneStrainRuptureDamageScope.Exit();
        }
    }
}

internal static class TuneStrainRuptureDamageScope
{
    private static readonly AsyncLocal<int> Depth = new();

    public static bool IsActive => Depth.Value > 0;

    public static void Enter() => Depth.Value++;

    public static void Exit()
    {
        if (Depth.Value > 0)
            Depth.Value--;
    }
}
