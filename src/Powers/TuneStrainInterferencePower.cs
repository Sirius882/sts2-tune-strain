#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using TuneStrain.Patches;

namespace TuneStrain.Powers;

/// <summary>
/// 集谐·干涉：处决后附加在怪物身上的可见 debuff，层数等于处决前的集谐·偏移层数（tsBias）。
/// 存续期间，玩家对该怪物造成的直接攻击伤害按集谐易伤倍率放大。
/// 倍率 = 2 * tsBias2Base(tsBias) ^ tsResponse / 100，由 <see cref="TuneStrainMath"/> 计算。
/// 持续回合数由隐藏的 <see cref="TuneStrainInterferenceDurationPower"/> 单独计量，
/// 干涉本身不随回合衰减（层数恒定，便于 tsBias 稳定参与倍率计算）。
/// 集谐易伤在力量/虚弱/易伤等原版效果结算完成后最后结算（与原版易伤在同一 Multiplicative 链中相乘）。
/// 集谐易伤只作用于直接伤害；聚爆引爆、熔解、谐度破坏、中毒和失去生命值都不受影响。
/// </summary>
public sealed class TuneStrainInterferencePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://tune_strain/images/powers/tune_strain_interference.png";
    public override string? CustomBigIconPath => "res://tune_strain/images/powers/tune_strain_interference.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(
            Title: "集谐·干涉",
            Description: "增加玩家对怪物的伤害。集谐·偏移和集谐响应的层数越高，增伤幅度越大。不能对该目标附加集谐·偏移。持续3回合。",
            SmartDescription: "增加玩家对怪物的伤害。集谐·偏移和集谐响应的层数越高，增伤幅度越大。不能对该目标附加集谐·偏移。持续3回合。");

    static TuneStrainInterferencePower()
    {
        SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(TuneStrainInterferencePower));
    }

    /// <summary>
    /// 集谐易伤：仅对打到本目标上的 powered attack 生效，倍率由 tsBias（本 power 层数）
    /// 与攻击者的集谐响应度共同决定。与原版易伤在同一个 Multiplicative 循环中相乘，
    /// 因此等价于"原版增减伤结算完后再次乘以集谐易伤"。
    /// </summary>
    public override decimal ModifyDamageMultiplicative(
        Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || dealer == null || TuneStrainRuptureDamageScope.IsActive)
            return 1m;

        if (!props.HasFlag(ValueProp.Move) || props.HasFlag(ValueProp.Unpowered) || props.HasFlag(ValueProp.Unblockable))
            return 1m;

        // 攻击者必须持有集谐响应 power（否则 tsRes=0，倍率=2*base^0/100=0.02，会出现无响应也增伤）。
        // 设计：无响应 power 时无集谐易伤（倍率视为 0 增伤）。故无响应 power 直接返回 1。
        int responsePowerAmount = TuneStrainState.GetResponsePowerAmount(dealer);
        if (responsePowerAmount <= 0)
            return 1m;

        double tsBias = Amount;
        double tsRes = TuneStrainState.GetResponseDegree(dealer);
        double ratio = TuneStrainMath.DamageAdvantageRatio(tsBias, tsRes);
        if (ratio <= 0.0)
            return 1m;

        return (decimal)(1.0 + ratio);
    }
}
