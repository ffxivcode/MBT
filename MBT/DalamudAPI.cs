global using Dalamud;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Dalamud
{
    public class DalamudAPI
    {
        [PluginService] public static DalamudPluginInterface PluginInterface { get; private set; }

        [PluginService] public static ICommandManager CommandManager { get; private set; }

        [PluginService] public static IFramework Framework { get; private set; }

        [PluginService] public static ISigScanner SigScanner { get; private set; }

        [PluginService] public static IClientState ClientState { get; private set; }

        [PluginService] public static IObjectTable ObjectTable { get; private set; }

        [PluginService] public static ITargetManager TargetManager { get; private set; }

        [PluginService] public static IGameConfig GameConfig { get; private set; }

        [PluginService] public static IGameGui GameGui { get; private set; }

        [PluginService] public static IKeyState KeyState { get; private set; }

        [PluginService] public static IPartyList PartyList { get; private set; }

        [PluginService] public static IPluginLog PluginLog { get; private set; }

        [PluginService] public static IChatGui? ChatGui { get; private set; }

        [PluginService] public static IGameInteropProvider? GameInteropProvider { get; private set; }

        public DalamudAPI(DalamudPluginInterface pluginInterface)
        {
            if (!pluginInterface.Inject(this))
                PluginLog.Error("Plugin Error: DalamudAPI Injection Failure");
            else
                PluginLog.Information("Plugin: DalamudAPI Injection Success");
        }

        public static void Initialize(DalamudPluginInterface pluginInterface) => _ = new DalamudAPI(pluginInterface);
    }
}