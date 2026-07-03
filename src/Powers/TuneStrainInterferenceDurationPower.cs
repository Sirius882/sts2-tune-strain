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

namespace TuneStrain.Powers;

/// <summary>
/// 隐藏 power：计量集谐·干涉的剩余持续回合数。
/// 初始 3 回合，每回合扣 1 层，扣到 0 时清除集谐·干涉 power 和自己。
/// 设计：集谐·干涉存续期间不能被附加集谐·偏移，故不可能再次触发集谐处决，
/// 因此干涉的持续回合数没有刷新的情况。
/// 一个 power 只保存一个整型变量（Amount），通过游戏原生同步机制实现多端同步。
/// </summary>
public sealed class TuneStrainInterferenceDurationPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

    static TuneStrainInterferenceDurationPower()
    {
        SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(TuneStrainInterferenceDurationPower));
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext,
        CombatSide side, IEnumerable<Creature> participants)
    {
        // 设计未指定按哪一方回合衰减；采用"敌方回合结束扣 1"以与 Aemeath 干涉保持一致的节奏
        // （怪物的负面状态通常在怪物自己回合结束时递减）。
        if (side != CombatSide.Enemy) return;
        if (Owner.IsDead) return;

        int remaining = Amount - 1;
        if (remaining <= 0)
        {
            await PowerCmd.Remove<TuneStrainInterferencePower>(Owner);
            await PowerCmd.Remove<TuneStrainInterferenceDurationPower>(Owner);
        }
        else
        {
            // 用 ModifyAmount 改 Amount 而非自己写字段，确保走游戏同步通道
            await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -1m, Owner, null);
        }
    }
}
