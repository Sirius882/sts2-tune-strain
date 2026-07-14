#nullable enable
using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace TuneStrain.Powers;

/// <summary>
/// 集谐·偏移：附加在怪物身上的可见 debuff。
/// 单纯的偏移没有任何效果；被集谐处决时清空并转化为等量的集谐·干涉。
/// 层数即 tsBias（处决前的偏移层数），用于决定后续集谐易伤的底数 tsBias2Base。
/// 拥有集谐·干涉的目标不能被附加集谐·偏移（在 <see cref="TuneStrainState.TryAddBias"/> 中检查）。
/// </summary>
public sealed class TuneStrainBiasPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    public override string? CustomPackedIconPath => "res://tune_strain/images/powers/tune_strain_bias.png";
    public override string? CustomBigIconPath => "res://tune_strain/images/powers/tune_strain_bias.png";

    public override List<(string, string)>? Localization =>
        new PowerLoc(
            Title: "集谐·偏移",
            Description: "触发谐度破坏后转化为[gold]集谐·干涉[/gold]。",
            SmartDescription: "触发谐度破坏后转化为集谐·干涉。");

    static TuneStrainBiasPower()
    {
        SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(TuneStrainBiasPower));
    }
}
