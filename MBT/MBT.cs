using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using TinyIpc.Messaging;
using static ECommons.DalamudServices.Svc;
using Dalamud.Plugin.Services;
using ECommons;
using MBT.Movement;
using ECommons.Automation;
using MBT.IPC;
using ECommons.DalamudServices;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Throttlers;
using ECommons.GameHelpers;
using MBT.Windows;
namespace MBT;

/// <summary>
/// MBT v11
/// </summary>

public class MBT : IDalamudPlugin
{
    public string Name => "MBT";
    public static MBT Plugin { get; private set; }
    public Configuration Configuration { get; init; }
    public WindowSystem WindowSystem = new("MBT");
    public MainWindow MainWindow { get; init; }

    internal bool Follow
    {
        get => _follow;
        set
        {
            _follow = value;
            if (value)
                Framework.Update += Framework_Update;
            else
            {
                VNavmesh_IPCSubscriber.Path_Stop();
                _overrideMovement.Enabled = false;
                Framework.Update -= Framework_Update;
            }
        }
    }

    internal float FollowDistance = 1;
    internal float PlayerDistance => Player.Available && FollowTargetObject != null ? Vector3.Distance(Player.Position, FollowTargetObject.Position) : 0;
    internal string FollowTarget = string.Empty;
    internal IGameObject? FollowTargetObject => FollowTargetObject != null && FollowTargetObject.Name.TextValue.Equals(FollowTarget, StringComparison.CurrentCultureIgnoreCase) ? FollowTargetObject : Objects.FirstOrDefault(s => s.Name.ExtractText().ToString().Equals(FollowTarget));

    private bool _follow = false;
    private readonly OverrideMovement _overrideMovement;
    private delegate void ExitDutyDelegate(char timeout);
    private ExitDutyDelegate _exitDuty;
    private readonly TinyMessageBus _messagebusSend = new("DalamudBroadcaster");
    private readonly TinyMessageBus _messagebusReceive = new("DalamudBroadcaster");
    private readonly TinyMessageBus _messagebusSpread = new("DalamudBroadcasterSpread");
    private readonly IPCProvider _ipcProvider;

    public MBT(
        IDalamudPluginInterface pluginInterface)
    {
        try
        {
            Plugin = this;
            ECommonsMain.Init(pluginInterface, this);
            
            //Create MainWindow UI
            MainWindow = new MainWindow(this);
            WindowSystem.AddWindow(MainWindow);

            //Add Commands  ---- More to come
            Commands.AddHandler("/bc", new CommandInfo(OnCommandBC)
            {
                HelpMessage = "/bc fw=toon1,toon2,ETC or all or allbutme or allbut,toon1,toon2,ETC (Remove Space from ToonsFullName) C=/commandname args\"\n" +
                "example: /bc FW=ALL C=/mbt ft Toon Name"
            });
            Commands.AddHandler("/mbt", new CommandInfo(OnCommand)
            {
                HelpMessage = "/mbt -> opens main window\n" +
                "/mbt cometome or ctm Player Name -> Immediately ends all movement and move's straight to Player\n" +
                "/mbt follow on/off or fon/foff -> turns follow on or off\n" +
                "/mbt followtarget or ft Player Name -> sets follow target to Player Name\n" +
                "/mbt followdistance or fd # -> sets Follow distance to # (must be int)\n" +
                "/mbt spread -> Spreads toons away from LocalPlayer\n" +
                "/mbt exitduty or ed -> Immediately exits the duty\n" +
                "/mbt acceptduty or ad -> Immediately accepts the duty finder popup\n" 
            });

            //Draw UI
            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenMainUi += OpenMainUI;

            _messagebusReceive.MessageReceived +=
                (sender, e) => MessageReceived(Encoding.UTF8.GetString((byte[])e.Message));
            _messagebusSpread.MessageReceived +=
                (sender, e) => MessageReceivedSpread(Encoding.UTF8.GetString((byte[])e.Message));

            //_exitDuty = Marshal.GetDelegateForFunctionPointer<ExitDutyDelegate>(SigScanner.ScanText("40 53 48 83 ec 20 48 8b 05 ?? ?? ?? ?? 0f b6 d9"));

            _overrideMovement = new();
            _ipcProvider = new();
        }
        catch (Exception e) { Log.Info($"Failed loading plugin\n{e}"); }
    }

    private static unsafe void AcceptDuty() => Callback.Fire((AtkUnitBase*)GameGui.GetAddonByName("ContentsFinderConfirm", 1), true, 8);

    private void Spread()
    {
        if (!Player.Available || Party.PartyId == 0) return;
        
        var playerGameObjectId = Player.Object.GameObjectId;
        List<uint> partyList = [];

        foreach (var partyMember in Party)
        {
            if (partyMember.ObjectId == Player.Object.GameObjectId) continue;

            if(partyMember.GameObject != null)
                partyList.Add(partyMember.ObjectId);
        }
        
        for(int i = 0; i < partyList.Count; i++)
        {
            var j = i + 1;
            Log.Info("OnCommandSpread: PartyList: " + partyList[i] + " : " + playerGameObjectId + "," + partyList[i] + "," + "*" + j + "*");
            _messagebusSpread.PublishAsync(Encoding.UTF8.GetBytes(playerGameObjectId + "," + partyList[i] + "," + "*" + j +"*"));
        }
    }

    private static void MessageReceived(string message)
    {
        if (!Player.Available) return;

        List<string> forWhos;
        if (message.Contains("FW=ALLBUT"))
        {
            if (message.Contains("FW=ALLBUT" + Player.Name.Replace(" ", string.Empty, StringComparison.CurrentCultureIgnoreCase)))
                return;
            else
                forWhos = [(Player.Name.Replace(" ", string.Empty, StringComparison.CurrentCultureIgnoreCase))];
        }
        else if (message.Contains("FW=ALL "))
            forWhos = [(Player.Name.Replace(" ", string.Empty, StringComparison.CurrentCultureIgnoreCase))];
        else
            forWhos = [.. message[(message.IndexOf("FW=") + 3)..message.IndexOf(' ')].Split(',')];

        if (forWhos.Any(i => i.Equals(Player.Name.Replace(" ", string.Empty, StringComparison.CurrentCultureIgnoreCase))))
        {
            Log.Info(message[(message.IndexOf("C=") + 2)..]);
            ECommons.Automation.Chat.Instance.ExecuteCommand(message[(message.IndexOf("C=") + 2)..]);
        }
    }

    private void MessageReceivedSpread(string message)
    {
        Log.Info("MessageRecieved: " + message);
        if (!Player.Available) return;
        Follow = false;
        var messageList = message.Split(',').ToList();
        var playerGameObjectId = Player.Object.GameObjectId;
        Log.Info("messageList.Count: " + messageList.Count + " messageList[0]: " + messageList[0] + " messageList[1]: " + messageList[1] + " messageList[2]: " + messageList[2]);
        
        if (Convert.ToUInt32(messageList[1]) == playerGameObjectId)
        {
            var sender = Objects.Where(s => s.GameObjectId == Convert.ToUInt32(messageList[0]));
            if (sender != null)
            {
                var pos = sender.First().Position;
                switch (messageList[2])
                {
                    case "*1*":
                        MoveTo(new Vector3(pos.X - 10, pos.Y, pos.Z));
                        break;
                    case "*2*":
                        MoveTo(new Vector3(pos.X + 10, pos.Y, pos.Z + 5));
                        break;
                    case "*3*":
                        MoveTo(new Vector3(pos.X + 10, pos.Y, pos.Z - 5));
                        break;
                }
            }
        }
    }

    private void Framework_Update(IFramework framework)
    {
        if (!Player.Available || !EzThrottler.Throttle("Framework_Update", 50))
            return;

        MoveTo(FollowTargetObject?.Position, FollowDistance);
    }

    public void Dispose()
    {
        //RemoveAllWindows and Dispose of them, Disable FrameworkUpdate and remove Commands
        Follow = false;
        WindowSystem.RemoveAllWindows();
        MainWindow.Dispose();
        Framework.Update -= Framework_Update;
        Commands.RemoveHandler("/mbt");
        Commands.RemoveHandler("/bc");
        ECommonsMain.Dispose();
        _messagebusReceive.Dispose();
        _messagebusSpread.Dispose();
        _messagebusSend.Dispose();
        _overrideMovement.Dispose();
    }

    private void OpenMainUI() => MainWindow.IsOpen = true;
    
    private void OnCommand(string command, string args)
    {
        //In response to the slash command, just display our main ui or turn Follow on or off

        if (args.Contains("FOLLOW ON", StringComparison.CurrentCultureIgnoreCase) || args.Contains("FON", StringComparison.CurrentCultureIgnoreCase))
            Follow = true;
        else if (args.Contains("FOLLOW OFF", StringComparison.CurrentCultureIgnoreCase) || args.Contains("FOFF", StringComparison.CurrentCultureIgnoreCase))
            Follow = false;
        else if (args.Contains("COMETOME ", StringComparison.CurrentCultureIgnoreCase))
        {
            var go = Objects.FirstOrDefault(s => s.Name.ExtractText().ToString().Equals(args[9..]));
            if (go != null)
            {
                Follow = false;
                MoveTo(go.Position, 0.1f);
            }
        }
        else if (args.Contains("CTM ", StringComparison.CurrentCultureIgnoreCase))
        {
            var go = Objects.FirstOrDefault(s => s.Name.ExtractText().ToString().Equals(args[4..]));
            if (go != null)
            {
                Follow = false;
                MoveTo(go.Position, 0.1f);
            }
        }
        else if (args.Contains("FOLLOWTARGET ", StringComparison.CurrentCultureIgnoreCase))
        {
            FollowTarget = args[13..];
        }
        else if (args.Contains("FT ", StringComparison.CurrentCultureIgnoreCase))
        {
            FollowTarget = args[3..];
        }
        else if (args.Contains("FOLLOWDISTANCE ", StringComparison.CurrentCultureIgnoreCase))
        {
            FollowDistance = Convert.ToInt32(args[15..]);
        }
        else if (args.Contains("FD ", StringComparison.CurrentCultureIgnoreCase))
        {
            FollowDistance = Convert.ToInt32(args[3..]);
        }
        else if (args.Contains("SPREAD", StringComparison.CurrentCultureIgnoreCase))
        {
            Follow = false;
            Spread();
        }
        else if (args.Contains("ACCEPTDUTY", StringComparison.CurrentCultureIgnoreCase) || args.Contains("AD", StringComparison.CurrentCultureIgnoreCase))
        {
            AcceptDuty();
        }
        /*else if (args.Contains("EXITDUTY", StringComparison.CurrentCultureIgnoreCase) || args.Contains("ED", StringComparison.CurrentCultureIgnoreCase))
        {
            //maybe make this have to be pressed / called / invoked twice to prevent accidental exiting
            _exitDuty.Invoke((char)0);
        }*/
        else if (MainWindow.IsOpen)
            MainWindow.IsOpen = false;
        else
            MainWindow.IsOpen = true;
    }

    private void OnCommandBC(string command, string args)
    {
        if (!Player.Available || args.IsNullOrEmpty()) return;

        if (!args.Contains("FW=", StringComparison.CurrentCultureIgnoreCase) || !args.Contains("C=", StringComparison.CurrentCultureIgnoreCase))
        {
            Svc.Chat?.Print(new XivChatEntry
            {
                Message = "Broadcast: syntax = /broadcast FW=TOON1,TOON2,ETC or ALL or ALLBUTME or ALLBUT,TOON1,TOON2,ETC (Remove Space from ToonsFullName) C=/commandname args"
            });

            return;
        }
        var forWho = args[args.IndexOf("FW=", StringComparison.CurrentCultureIgnoreCase)..args.IndexOf(' ')].ToUpper();
        var rest = string.Concat("C=", args.AsSpan(args.IndexOf("C=", StringComparison.CurrentCultureIgnoreCase) + 2));
        var ARGSc = forWho + " " + rest;

        if (ARGSc.Contains("ALLBUTME"))
            _messagebusSend.PublishAsync(Encoding.UTF8.GetBytes(ARGSc.Replace("ALLBUTME", "ALLBUT" + Player.Name.Replace(" ", string.Empty, StringComparison.CurrentCultureIgnoreCase))));
        else
            _messagebusSend.PublishAsync(Encoding.UTF8.GetBytes(ARGSc));
    }
    
    private void DrawUI() => WindowSystem.Draw();

    private void MoveTo(Vector3? position, float precision = 0.1f)
    {
        if (position == null) return;

        if (Configuration.UseNavmesh)
        {
            if (_overrideMovement.Enabled)
                _overrideMovement.Enabled = false;

            if (VNavmesh_IPCSubscriber.Path_GetTolerance() != precision)
                VNavmesh_IPCSubscriber.Path_SetTolerance(precision);

            VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(position.Value, false);
        }
        else
        {
            if (VNavmesh_IPCSubscriber.Path_IsRunning())
                VNavmesh_IPCSubscriber.Path_Stop();

            if (_overrideMovement.Precision != precision)
                _overrideMovement.Precision = precision;

            _overrideMovement.DesiredPosition = position.Value;
        }
    }
}