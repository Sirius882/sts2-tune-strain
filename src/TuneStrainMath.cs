#nullable enable
namespace TuneStrain;

/// <summary>
/// 集谐系统的纯数学函数集。不引用任何游戏状态，便于单测与解耦。
/// 设计文档 §2.2 / §2.3 的算法实现。
/// </summary>
public static class TuneStrainMath
{
    /// <summary>
    /// tsBias → 倍率底数 tsBias2Base。
    /// 1 层 → 1.15，2 层及以上 → 1.2。
    /// 为以后 tsBias &gt; 2 可能设计更多效果留出空间（此处留 API）。
    /// </summary>
    public static double BiasToBase(double tsBias)
    {
        if (tsBias <= 1.0)
            return 1.15;
        return 1.2;
    }

    /// <summary>
    /// 集谐响应度 tsResponse：超额累进折算响应 power 层数 → 响应度。
    /// ≤30 全额；30~50 超出部分按 1/2；50~100 超出部分按 1/4；&gt;100 超出部分不计。
    /// 不取整（保留小数）。灵感来自个人所得税超额累进税率。
    /// </summary>
    public static double GetResponseDegree(int responsePowerAmount)
    {
        if (responsePowerAmount <= 0)
            return 0.0;

        double r = 0.0;
        if (responsePowerAmount <= 30)
            return responsePowerAmount;

        r += 30.0;
        if (responsePowerAmount <= 50)
            return r + (responsePowerAmount - 30) * 0.5;

        r += (50 - 30) * 0.5; // 10
        if (responsePowerAmount <= 100)
            return r + (responsePowerAmount - 50) * 0.25;

        r += (100 - 50) * 0.25; // 12.5
        // 超出 100 的部分不计
        return r;
    }

    /// <summary>
    /// 集谐易伤增伤比例 tsDmgAdvRatio = tsBias2Base(tsBias)^tsRes / 100。
    /// 最终伤害 = 原伤害 × (1 + ratio)。
    /// 调用方负责保证 tsRes 已经过 tsResponse 折算（含共鸣模态的 2 倍放大）。
    /// </summary>
    public static double DamageAdvantageRatio(double tsBias, double tsRes)
    {
        if (tsBias <= 0 || tsRes <= 0)
            return 0.0;
        double b = BiasToBase(tsBias);
        double a = System.Math.Pow(b, tsRes);
        return a / 100.0;
    }
}
