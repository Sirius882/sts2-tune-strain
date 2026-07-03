using System;
using Godot;

namespace TuneStrain;

/// <summary>集谐系统的统一日志入口，便于排查多人同步与补丁问题。</summary>
internal static class TuneStrainLog
{
    public static void Info(string message) => GD.Print($"[tune_strain] {message}");
    public static void Warn(string message) => GD.PrintErr($"[tune_strain][WARN] {message}");
    public static void Error(string message, Exception? ex = null) =>
        GD.PrintErr(ex == null ? $"[tune_strain][ERROR] {message}" : $"[tune_strain][ERROR] {message} :: {ex}");
}
