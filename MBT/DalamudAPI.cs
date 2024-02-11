global using Dalamud;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.IoC;
using Dalamud.Memory;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MBT
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

        [PluginService] public static IBuddyList BuddyList { get; private set; }

        [PluginService] public static IPluginLog PluginLog { get; private set; }

        [PluginService] public static IChatGui ChatGui { get; private set; }

        [PluginService] public static IGameInteropProvider GameInteropProvider { get; private set; }

        [PluginService] public static ICondition Condition { get; private set; }

        [PluginService] public static IDataManager DataManager { get; private set; }

        public static IEnumerable<T> GetObjectInRadius<T>(IEnumerable<T> objects, float radius) where T : GameObject
        => objects.Where(o => DistanceToPlayer(o) <= radius);

        public static ExcelSheet<T> GetSheet<T>() where T : ExcelRow => DataManager.GetExcelSheet<T>();

        public static float DistanceToPlayer(GameObject obj)
        {
            if (obj == null) return float.MaxValue;
            var player = Player.Object;
            if (player == null) return float.MaxValue;

            var distance = Vector3.Distance(player.Position, obj.Position) - player.HitboxRadius;
            distance -= obj.HitboxRadius;
            return distance;
        }
        public unsafe static class Player
        {
            public static PlayerCharacter Object => ClientState.LocalPlayer;
            public static bool Available => ClientState.LocalPlayer != null;
            public static bool Interactable => Available && Object.IsTargetable;
            public static ulong CID => ClientState.LocalContentId;
            public static StatusList Status => ClientState.LocalPlayer.StatusList;
            public static string Name => ClientState.LocalPlayer?.Name.ToString();
            public static int Level => ClientState.LocalPlayer?.Level ?? 0;
            public static bool IsInHomeWorld => ClientState.LocalPlayer.HomeWorld.Id == ClientState.LocalPlayer.CurrentWorld.Id;
            public static string HomeWorld => ClientState.LocalPlayer?.HomeWorld.GameData.Name.ToString();
            public static string CurrentWorld => ClientState.LocalPlayer?.CurrentWorld.GameData.Name.ToString();
            public static Character* Character => (Character*)ClientState.LocalPlayer.Address;
            public static BattleChara* BattleChara => (BattleChara*)ClientState.LocalPlayer.Address;
            public static GameObject* GameObject => (GameObject*)ClientState.LocalPlayer.Address;
        }
        public static BNpcBase GetObjectNPC(GameObject obj)
        {
            if (obj == null) return null;
            return GetSheet<BNpcBase>().GetRow(obj.DataId);
        }

        public unsafe static bool TryGetAddonByName<T>(string Addon, out T* AddonPtr) where T : unmanaged
        {
            var a = GameGui.GetAddonByName(Addon, 1);
            if (a == IntPtr.Zero)
            {
                AddonPtr = null;
                return false;
            }
            else
            {
                AddonPtr = (T*)a;
                return true;
            }
        }
        public DalamudAPI(DalamudPluginInterface pluginInterface)
        {
            if (!pluginInterface.Inject(this))
                PluginLog.Error("Plugin Error: DalamudAPI Injection Failure");
            else
                PluginLog.Information("Plugin: DalamudAPI Injection Success");
        }

        public static void Initialize(DalamudPluginInterface pluginInterface) => _ = new DalamudAPI(pluginInterface);

        public unsafe static bool IsAddonReady(AtkUnitBase* Addon)
        {
            return Addon->IsVisible && Addon->UldManager.LoadedState == AtkLoadState.Loaded;
        }

        public unsafe static nint GetSpecificYesno(List<string> s)
        {
            for (int i = 1; i < 100; i++)
            {
                try
                {
                    var addonAUB = (AtkUnitBase*)GameGui.GetAddonByName("SelectYesno", i);
                    var addon = GameGui.GetAddonByName("SelectYesno", i);
                    if (addonAUB == null) return -1;
                    if (IsAddonReady(addonAUB))
                    {
                        var textNode = addonAUB->UldManager.NodeList[15]->GetAsAtkTextNode();
                        var text = MemoryHelper.ReadSeString(&textNode->NodeText).ExtractText();
                        PluginLog.Information(text);
                        foreach (var e in s)
                        {
                            if (!text.Contains(e))
                            {
                                continue;
                            }
                        }
                        PluginLog.Verbose($"SelectYesno {s} addon {i}");
                        return addon;
                    }
                }
                catch (Exception e)
                {
                    PluginLog.Information(e.ToString());
                    return -1;
                }
            }
            return -1;
        }
    }
}