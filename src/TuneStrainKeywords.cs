using BaseLib.Patches.Content;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace TuneStrain;

/// <summary>
/// 集谐系统注册的自定义关键词。
/// TuneStrainResponse：附加在卡牌上的标记，本身没有效果；由集谐系统在处决结算时统一扫描计数。
/// 任何依赖集谐系统的角色 mod 都可读取 <see cref="TuneStrainKeywords.TuneStrainResponse"/>。
/// </summary>
public static class TuneStrainKeywords
{
    /// <summary>集谐响应标记关键词。卡牌带此关键词时，被集谐处决结算纳入响应度统计。</summary>
    [CustomEnum("TUNE_STRAIN_RESPONSE")]
    [KeywordProperties(AutoKeywordPosition.Before)]
    public static CardKeyword TuneStrainResponse = CardKeyword.None;
}
