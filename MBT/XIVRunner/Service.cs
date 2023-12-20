using Dalamud.IoC;
using Dalamud.Plugin.Services;

namespace XIVRunner;

internal class Service
{
    [PluginService] public static IDataManager Data { get; private set; } = null!;

    [PluginService] internal static ICondition Condition { get; private set; } = null!;

    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    [PluginService] internal static IClientState ClientState { get; private set; } = null!;

    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    [PluginService] public static IGameInteropProvider Hook { get; private set; } = null!;
}
