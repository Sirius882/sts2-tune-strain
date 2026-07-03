#nullable enable
using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace TuneStrain.Powers;

/// <summary>
/// 集谐响应 power：附加在玩家身上的可见 buff，层数即"集谐响应标记总数"（由处决结算时覆盖写入）。
/// 层数通过 <see cref="TuneStrainMath.GetResponseDegree"/> 折算为集谐响应度 tsRes，
/// 参与集谐易伤倍率计算。持续回合数由隐藏的 <see cref="TuneStrainResponseDurationPower"/> 计量。
/// </summary>
public sealed class TuneStrainResponsePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://tune_strain/images/powers/tune_strain_response.png";
    public override string? CustomBigIconPath => "res://tune_strain/images/powers/tune_strain_response.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(
            Title: "集谐响应",
            Description: "集谐处决结算时按抽牌堆/手牌/弃牌堆的[gold]集谐响应[/gold]牌总数覆盖层数；存续期间使你对带[gold]集谐·干涉[/gold]的怪物造成额外伤害。",
            SmartDescription: "集谐处决结算时按响应牌总数覆盖层数；存续期间你对带集谐·干涉的怪物造成额外伤害。");

    static TuneStrainResponsePower()
    {
        SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(TuneStrainResponsePower));
    }
}
