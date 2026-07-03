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
/// 隐藏 power：计量集谐响应 power 的剩余持续回合数。
/// 初始 3 回合，每回合扣 1 层，扣到 0 时清除集谐响应 power 和自己。
/// 复数个怪物的场景下，集谐响应 power 的持续回合数有可能被刷新（通过重新 Apply 覆盖）。
/// 一个 power 只保存一个整型变量。
/// </summary>
public sealed class TuneStrainResponseDurationPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

    static TuneStrainResponseDurationPower()
    {
        SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(TuneStrainResponseDurationPower));
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext,
        CombatSide side, IEnumerable<Creature> participants)
    {
        // 玩家身上的 buff 持续时间在玩家自己回合结束时递减
        if (side != CombatSide.Player) return;
        if (Owner.IsDead) return;

        int remaining = Amount - 1;
        if (remaining <= 0)
        {
            await PowerCmd.Remove<TuneStrainResponsePower>(Owner);
            await PowerCmd.Remove<TuneStrainResponseDurationPower>(Owner);
        }
        else
        {
            await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -1m, Owner, null);
        }
    }
}
