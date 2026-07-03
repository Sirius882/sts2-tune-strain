using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using TuneStrain;

namespace TuneStrain.EntryPoint;

/// <summary>
/// tune_strain mod 的引导入口。
/// 由游戏的 ModManager 通过 [ModInitializer("Init")] 调用。
/// 负责初始化静态状态、应用 Harmony 补丁、注册集谐响应关键词。
/// </summary>
[ModInitializer("Init")]
public static class TuneStrainEntry
{
    private static Harmony? _harmony;

    public static void Init()
    {
        // 触发 TuneStrainState 的静态构造（挂战斗生命周期回调），确保早于任何 patch 触发
        _ = typeof(TuneStrainState);

        _harmony = new Harmony("sts2.tunestrain");
        _harmony.PatchAll(typeof(TuneStrainEntry).Assembly);

        TuneStrainLog.Info("tune_strain initialized: 集谐·偏移 / 干涉 / 响应 系统已就绪。");
    }
}
