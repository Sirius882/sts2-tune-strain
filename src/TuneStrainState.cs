#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AemeathWw.Scripts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using TuneStrain.Powers;

namespace TuneStrain;

/// <summary>
/// 集谐系统的核心状态与对外 API。
/// 所有写入都通过游戏命令（PowerCmd 等）走原生同步通道，调用方必须 await。
/// 静态战斗级状态（每场力量奖励计数、临时响应记录）通过 CombatManager 生命周期事件重置。
/// </summary>
public static class TuneStrainState
{
    private const int InitialInterferenceDuration = 3;
    private const int InitialResponseDuration = 3;
    private const int BreakStrengthPerBreakCap = 2;
    private const int BreakStrengthPerCombatCap = 4;

    private static readonly PlayerChoiceContext Throwing = new ThrowingPlayerChoiceContext();

    // 每场战斗的力量奖励累计（按触发者计），保证"每场战斗最多共计不超过 4 点"
    private static readonly Dictionary<Creature, int> BreakStrengthGainedThisCombat = new();
    // 临时集谐响应：玩家 → 本次战斗中被临时打上响应标记的卡牌集合
    private static readonly Dictionary<Player, HashSet<CardModel>> TemporaryResponses = new();

    // 集谐响应度倍率提供者：角色 mod（如 Denia 共鸣模态·集谐）通过注册一个回调把响应 power 层折算前乘以倍率。
    // 设计文档 §3：达妮娅共鸣模态按 2 倍采用响应 power 层。为解耦，集谐系统不直接引用 Denia，而是开放 API。
    private static readonly List<Func<Creature, double>> ResponseDegreeMultipliers = new();

    static TuneStrainState()
    {
        // 静态构造确保在 mod 加载时挂上战斗生命周期回调
        CombatManager.Instance.CombatSetUp += _ => ResetCombatState();
        CombatManager.Instance.CombatEnded += _ => ResetCombatState();
    }

    private static void ResetCombatState()
    {
        BreakStrengthGainedThisCombat.Clear();
        // 临时响应只清理自己记录过的卡牌的关键词，避免误伤永久响应
        foreach (var card in TemporaryResponses.Values.SelectMany(c => c).ToList())
            RemoveTemporaryResponseKeyword(card);
        TemporaryResponses.Clear();
    }

    // ---------------- 集谐·偏移 ----------------

    /// <summary>读取目标当前集谐·偏移层数。</summary>
    public static int GetBias(Creature target) =>
        target.GetPower<TuneStrainBiasPower>()?.Amount ?? 0;

    /// <summary>目标是否处于集谐·干涉（存续期间不能被附加偏移）。</summary>
    public static bool HasInterference(Creature target) =>
        target.GetPower<TuneStrainInterferencePower>() != null;

    /// <summary>
    /// 给目标附加集谐·偏移。拥有集谐·干涉的目标不能被附加。
    /// 调用方必须 await。返回是否成功附加（false = 已干涉/已死/层数非正）。
    /// </summary>
    public static async Task<bool> TryAddBias(Creature target, int amount, Creature applier, CardModel? source)
    {
        if (amount <= 0 || target.IsDead) return false;
        if (HasInterference(target)) return false;

        int current = GetBias(target);
        int delta = amount; // 偏移本身不设上限（上限由调用方根据稀有度裁剪，与系统无关）
        // 用 Apply 让 Counter 自动叠加
        await PowerCmd.Apply<TuneStrainBiasPower>(Throwing, target, delta, applier, source);
        return true;
    }

    // ---------------- 集谐响应 power 读取 ----------------

    /// <summary>读取玩家当前的集谐响应 power 层数（未折算、未乘倍率）。</summary>
    public static int GetResponsePowerAmount(Creature player) =>
        player.GetPower<TuneStrainResponsePower>()?.Amount ?? 0;

    /// <summary>
    /// 读取玩家的集谐响应度 tsRes（已折算 + 已应用角色 mod 注册的倍率）。
    /// 倍率先作用于"响应 power 层数"再折算，等价于设计文档的 tsResponse(2*layers)。
    /// </summary>
    public static double GetResponseDegree(Creature player)
    {
        int layers = GetResponsePowerAmount(player);
        if (layers <= 0) return 0.0;

        double effective = layers;
        foreach (var multiplier in ResponseDegreeMultipliers)
        {
            try { effective *= multiplier(player); }
            catch { /* 单个 provider 出错不应破坏整体 */ }
        }
        return TuneStrainMath.GetResponseDegree((int)effective);
    }

    /// <summary>
    /// 注册一个集谐响应度倍率提供者。返回一个可释放句柄，用于角色 mod 在共鸣模态结束时移除。
    /// 设计文档 §3：达妮娅共鸣模态·集谐在计算增伤时按 2 倍采用响应 power 层。
    /// 为解耦，达妮娅通过此 API 注册一个返回 2.0 的回调（在共鸣模态激活时注册、退出时释放）。
    /// </summary>
    public static IDisposable RegisterResponseDegreeMultiplier(Func<Creature, double> multiplier)
    {
        ArgumentNullException.ThrowIfNull(multiplier);
        ResponseDegreeMultipliers.Add(multiplier);
        return new MultiplierHandle(() => ResponseDegreeMultipliers.Remove(multiplier));
    }

    private sealed class MultiplierHandle : IDisposable
    {
        private Action? _release;
        public MultiplierHandle(Action release) => _release = release;
        public void Dispose() { Interlocked.Exchange(ref _release, null)?.Invoke(); }
    }

    // ---------------- 临时集谐响应 ----------------

    /// <summary>该卡是否已被本场战斗的临时响应机制标记过。</summary>
    public static bool HasTemporaryResponse(CardModel card) =>
        TemporaryResponses.Values.Any(set => set.Contains(card));

    /// <summary>
    /// 给一张永久牌（deck card）及其本场战斗所有副本附加临时集谐响应标记。
    /// 战斗结束时自动清理（只清理自己记录过的卡）。
    /// </summary>
    public static void AddTemporaryResponse(Player player, CardModel deckCard)
    {
        if (!TemporaryResponses.TryGetValue(player, out var set))
            TemporaryResponses[player] = set = new HashSet<CardModel>();

        AddOne(set, deckCard);
        var combatState = player.PlayerCombatState;
        if (combatState == null) return;
        foreach (var combatCard in combatState.AllCards.Where(c => ReferenceEquals(DeckVersionOf(c), deckCard)))
            AddOne(set, combatCard);
    }

    private static CardModel? DeckVersionOf(CardModel card) =>
        card.GetType().GetProperty("DeckVersion")?.GetValue(card) as CardModel;

    private static void AddOne(HashSet<CardModel> set, CardModel card)
    {
        if (card.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse)) return;
        CardCmd.ApplyKeyword(card, TuneStrainKeywords.TuneStrainResponse);
        set.Add(card);
    }

    /// <summary>清除本场战斗所有临时集谐响应标记（集谐处决结算时调用）。</summary>
    public static void ClearTemporaryResponses()
    {
        foreach (var card in TemporaryResponses.Values.SelectMany(c => c).ToList())
            RemoveTemporaryResponseKeyword(card);
        TemporaryResponses.Clear();
    }

    private static void RemoveTemporaryResponseKeyword(CardModel card)
    {
        try
        {
            if (card.HasBeenRemovedFromState) return;
            if (!card.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse)) return;
            if (card.CanonicalKeywords.Contains(TuneStrainKeywords.TuneStrainResponse)) return;
            CardCmd.RemoveKeyword(card, TuneStrainKeywords.TuneStrainResponse);
        }
        catch { }
    }

    // ---------------- 集谐处决 ----------------

    /// <summary>
    /// 集谐处决：清空目标身上的集谐·偏移，附加等量的集谐·干涉 + 3 回合持续隐藏 power；
    /// 给触发者等值于偏移层数的力量（每次处决 ≤2、每场战斗共计 ≤4）；
    /// 按抽牌堆/弃牌堆/手牌中的集谐响应牌总数覆盖触发者的集谐响应 power + 3 回合持续隐藏 power；
    /// 清除所有临时集谐响应标记。
    /// 调用方必须 await。本方法不检查前置条件（偏谐是否满等），由调用方决定。
    /// </summary>
    public static async Task ResolveRupture(Creature target, Creature applier, CardModel? source)
    {
        int bias = GetBias(target);
        if (bias <= 0 || target.IsDead) return;

        // 1) 清空偏移
        await PowerCmd.Remove<TuneStrainBiasPower>(target);

        // 2) 附加等量干涉 + 3 回合持续
        await PowerCmd.Apply<TuneStrainInterferencePower>(Throwing, target, bias, applier, source);
        await PowerCmd.Apply<TuneStrainInterferenceDurationPower>(Throwing, target, InitialInterferenceDuration, applier, source);

        // 3) 触发者获得力量（每次 ≤2，每场战斗共计 ≤4）
        await GrantBreakStrengthReward(applier, bias, source);

        // 4) 计算响应牌总数 → 覆盖响应 power 层数 + 刷新持续回合
        Player? player = applier.Player ?? source?.Owner;
        if (player != null)
            await UpdateResponsePowerFromCards(player, applier, source);

        // 5) 清除临时集谐响应标记（设计：处决触发时清除）
        ClearTemporaryResponses();
    }

    private static async Task GrantBreakStrengthReward(Creature applier, int bias, CardModel? source)
    {
        if (applier.IsDead || bias <= 0) return;
        int perBreak = Math.Min(bias, BreakStrengthPerBreakCap);
        int alreadyGained = BreakStrengthGainedThisCombat.GetValueOrDefault(applier, 0);
        int gain = Math.Min(perBreak, BreakStrengthPerCombatCap - alreadyGained);
        if (gain <= 0) return;
        await PowerCmd.Apply<StrengthPower>(Throwing, applier, gain, applier, source);
        BreakStrengthGainedThisCombat[applier] = alreadyGained + gain;
    }

    private static async Task UpdateResponsePowerFromCards(Player player, Creature? applier, CardModel? source)
    {
        int totalResponseCards = CountResponseCards(player);

        int current = GetResponsePowerAmount(player.Creature);
        int newAmount = Math.Max(current, totalResponseCards);

        if (newAmount <= 0)
        {
            // 无响应牌且原本也没有响应 power：什么都不做
            return;
        }

        // 覆盖响应 power 层数：用 Apply 增量到目标值，避免直接写 Amount
        int delta = newAmount - current;
        if (delta > 0)
            await PowerCmd.Apply<TuneStrainResponsePower>(Throwing, player.Creature, delta, applier ?? player.Creature, source);

        // 刷新/创建持续回合 power：直接 Apply 3 层（Counter 叠加会累加，这里设计是"覆盖为 3"而非累加）
        // 为实现"覆盖为初始 3 回合"，先 Remove 再 Apply
        await PowerCmd.Remove<TuneStrainResponseDurationPower>(player.Creature);
        await PowerCmd.Apply<TuneStrainResponseDurationPower>(Throwing, player.Creature, InitialResponseDuration, applier ?? player.Creature, source);
    }

    private static int CountResponseCards(Player player)
    {
        var cs = player.PlayerCombatState;
        if (cs == null) return 0;
        int count = 0;
        count += CountInPile(PileType.Draw, player);
        count += CountInPile(PileType.Hand, player);
        count += CountInPile(PileType.Discard, player);
        return count;
    }

    private static int CountInPile(PileType pile, Player player)
    {
        try
        {
            return pile.GetPile(player).Cards.Count(c => c.Keywords.Contains(TuneStrainKeywords.TuneStrainResponse));
        }
        catch { return 0; }
    }
}
