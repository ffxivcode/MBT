using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using TinyIpc.Messaging;
using static ECommons.DalamudServices.Svc;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons;
using MBT.Movement;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ECommons.Automation;
using ECommons.DalamudServices;
using MBT.IPC;
namespace MBT;

/// <summary>
/// MBT v1
/// </summary>

public class MBT : IDalamudPlugin
{
    public string Name => "MBT";
    public static MBT Plugin { get; private set; }
    public Configuration Configuration { get; init; }
    public WindowSystem WindowSystem = new("MBT");
    public MainWindow MainWindow { get; init; }

    internal string TextFollow1 = "";
    internal Vector4 TextFollow1Color = new(255f, 0f, 0f, 1f);
    internal string TextFollow2 = "";
    internal Vector4 TextFollow2Color = new(255f, 0f, 0f, 1f);
    internal string TextFollow3 = "";
    internal Vector4 TextFollow3Color = new(255f, 0f, 0f, 1f);
    internal bool Follow = false;
    internal bool UseNavmesh = false;
    internal bool Following = false;
    internal int FollowDistance = 1;
    internal string FollowTarget = "";
    internal GameObject? FollowTargetObject = null;
    internal string TeleportPosition = "";
    internal string SpeedBase = "";
    internal List<string> ListBoxText = [];
    internal List<string> ListBoxPOSText = [];

    private static bool _spreading = false;
    private readonly OverrideMovement _overrideMovement;
    private readonly OverrideAFK _overrideAFK;
    private delegate void ExitDutyDelegate(char timeout);
    private ExitDutyDelegate _exitDuty;
    private readonly TinyMessageBus _messagebusSend = new("DalamudBroadcaster");
    private readonly TinyMessageBus _messagebusReceive = new("DalamudBroadcaster");
    private readonly TinyMessageBus _messagebusSpread = new("DalamudBroadcasterSpread");
    private IPCProvider _ipcProvider;

    public MBT(
        DalamudPluginInterface pluginInterface)
    {
        try
        {
            Plugin = this;
            ECommonsMain.Init(pluginInterface, this, Module.All);
            
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

            //Attach our OnGameFrameworkUpdate function to our game's Framework Update (called once every frame)
            Framework.Update += OnGameFrameworkUpdate;

            _messagebusReceive.MessageReceived +=
                (sender, e) => MessageReceived(Encoding.UTF8.GetString((byte[])e.Message));
            _messagebusSpread.MessageReceived +=
                (sender, e) => MessageReceivedSpread(Encoding.UTF8.GetString((byte[])e.Message));

            _exitDuty = Marshal.GetDelegateForFunctionPointer<ExitDutyDelegate>(SigScanner.ScanText("40 53 48 83 ec 20 48 8b 05 ?? ?? ?? ?? 0f b6 d9"));

            _overrideMovement = new OverrideMovement();
            _overrideAFK = new OverrideAFK();
            _ipcProvider = new IPCProvider(this);
        }
        catch (Exception e) { Log.Info($"Failed loading plugin\n{e}"); }
    }

    private static unsafe void AcceptDuty()
    {
        Callback.Fire((AtkUnitBase*)GameGui.GetAddonByName("ContentsFinderConfirm", 1), true, 8);
    }

    private void Spread()
    {
        if (ClientState.LocalPlayer == null) return;
        if (Party == null) return;
        var playerObjectId = ClientState.LocalPlayer.ObjectId;
        List<uint> partyList = new();

        foreach (var partyMember in Party)
        {
            if (partyMember.ObjectId == ClientState.LocalPlayer.ObjectId) { continue; }

            if(partyMember.GameObject != null)
                partyList.Add(partyMember.GameObject.ObjectId);
        }
        
        for(int i = 0; i < partyList.Count; i++)
        {
            var j = i + 1;
            Log.Info("OnCommandSpread: PartyList: " + partyList[i] + " : " + playerObjectId + "," + partyList[i] + "," + "*" + j + "*");
            _messagebusSpread.PublishAsync(Encoding.UTF8.GetBytes(playerObjectId + "," + partyList[i] + "," + "*" + j +"*"));
        }
    }

    private void MessageReceived(string message)
    {
        if (ClientState.LocalPlayer is null) { return; }

        List<string> forWhos;
        if (message.Contains("FW=ALLBUT"))
        {
            if (message.Contains("FW=ALLBUT" + ClientState.LocalPlayer.Name.ToString().ToUpper().Replace(" ", "")))
                return;
            else
                forWhos = new List<string> { (ClientState.LocalPlayer.Name.ToString().ToUpper().Replace(" ", "")) };
        }
        else if (message.Contains("FW=ALL "))
            forWhos = new List<string> { (ClientState.LocalPlayer.Name.ToString().ToUpper().Replace(" ", "")) };
        else
            forWhos = message.Substring(message.IndexOf("FW=") + 3, message.IndexOf(" ") - (message.IndexOf("FW=") + 3)).Split(',').ToList();

        if (forWhos.Any(i => i.Equals(ClientState.LocalPlayer.Name.ToString().ToUpper().Replace(" ", ""))))
        {
            Log.Info(message.Substring(message.IndexOf("C=") + 2));
            ECommons.Automation.Chat.Instance.ExecuteCommand(message.Substring(message.IndexOf("C=") + 2));
        }
    }

    private void MessageReceivedSpread(string message)
    {
        Log.Info("MessageRecieved: " + message);
        if (ClientState.LocalPlayer == null) return;
        Follow = false;
        _spreading = true;
        var messageList = message.Split(',').ToList();
        var playerObjectId = ClientState.LocalPlayer.ObjectId;
        Log.Info("messageList.Count: " + messageList.Count + " messageList[0]: " + messageList[0] + " messageList[1]: " + messageList[1] + " messageList[2]: " + messageList[2]);
        
        if (Convert.ToUInt32(messageList[1]) == playerObjectId)
        {
            var sender = Objects.Where(s => s.ObjectId == Convert.ToUInt32(messageList[0]));
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
    internal void SetTarget()
    {
        //If PlayerCharacter's target is not null, Set our followTarget InputText to our Target Object's .Name field
        if (Targets.Target != null)
        {
            FollowTarget = Targets.Target.Name.TextValue;
        }
    }
    internal void SetFollowStatus(bool sts, string name, string distance, Vector4 color)
    {
        //Set UI TextColored's Values
        string? FollowingSTS;
        if (sts)
            FollowingSTS = "On";
        else
            FollowingSTS = "Off";
        TextFollow1 = " " + FollowingSTS;
        TextFollow2 = "Name: " + name;
        TextFollow3 = "Distance: " + distance + " <= " + FollowDistance;
        TextFollow1Color = color;
        TextFollow2Color = color;
        TextFollow3Color = color;
    }

    private static GameObject? GetGameObjectFromName(string _objectName) => Objects.FirstOrDefault(s => s.Name.ToString().Equals(_objectName));

    public bool GetFollowTargetObject()
    {
        var ftarget = GetGameObjectFromName(FollowTarget);
        if (ftarget == null)
        {
            FollowTargetObject = null;
            SetFollowStatus(false, FollowTarget + " not found", "0", new(255f, 0f, 0f, 1f));
            if (Following)
                Stop();
            return false;
        }
        else
        {
            FollowTargetObject = ftarget;
            return true;
        }
    }

    public void OnGameFrameworkUpdate(IFramework framework)
    {
        /*if (IPCManager.BossMod_IsEnabled && IPCManager.BossMod_ForbiddenZonesCount > 0)
        {
            Stop();
            return;
        }*/
        //If follow is not enabled clear TextColored's and return
        if (!Follow)
        {
            SetFollowStatus(false, "", "", new(255f, 0f, 0f, 1f));
            if (Following && !_spreading)
            {
                Stop();
                Following = false;
            }
            return;
        }

        //If LocalPlayer object is null return (we are not logged in or between zones etc..)
        if (ClientState.LocalPlayer == null) return;

        //If followTarget is not empty GetFollowTargetObject then set our player variable and calculate the distance
        //between player and followTargetObject and if distance > followDistance move to the followTargetObject
        if (!string.IsNullOrEmpty(FollowTarget))
        {
            try
            {
                /// Need to figure out how to handle this
                /// because if the Object leaves the zone it
                /// sorta leaves a ghost GO that is still .IsValid
                /// and all its data is locked to where it was
                /// when it left the zone, this current way is
                /// gross and very inefficient
                var player = ClientState.LocalPlayer;
                if (!GetFollowTargetObject())
                    return;
               
                if (FollowTargetObject == null || player == null) return;

                //if (targetFollowTargetsTargets)
                //{
                //    if (player.TargetObjectId != followTargetObject.TargetObjectId && followTargetObject.TargetObjectId != 0)
                //    {
                //        Log.Info("1");
                //        Targets.Target = followTargetObject.TargetObject;
                //        Log.Info("Follow:" + followTargetObject.TargetObject.Name);

                //    }
                //}
                if (!Follow)
                {
                    SetFollowStatus(false, "", "", new(255f, 0f, 0f, 1f));
                    if (Following && !_spreading)
                    {
                        Stop(); 
                        Following = false;
                    }
                    return;
                }
                var distance = Vector3.Distance(player.Position, FollowTargetObject.Position);

                SetFollowStatus(true, FollowTargetObject.Name.ToString(), ((int)distance).ToString(), new(0f, 255f, 0f, 1f));

                if ((distance > (FollowDistance + .1f)) && distance < 100)
                {
                    Following = true;
                    MoveTo(FollowTargetObject.Position, FollowDistance + .1f);
                }
                else if (Following)
                {
                    Following = false;
                    Stop();
                }
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
                SetFollowStatus(false, "", "", new(255f, 0f, 0f, 1f));
                if (Following)
                {
                    Following = false;
                    Stop();
                }
                //throw;
            }
        }
        else
        {
            SetFollowStatus(false, "No follow target set", "", new(255f, 0f, 0f, 1f));
            if (Following)
            {
                Following = false;
                Stop();
            }
        }
    }
    public void Dispose()
    {
        //RemoveAllWindows and Dispose of them, Disable FrameworkUpdate and remove Commands
        StopAllMovement();
        WindowSystem.RemoveAllWindows();
        MainWindow.Dispose();
        Framework.Update -= OnGameFrameworkUpdate;
        Commands.RemoveHandler("/mbt");
        Commands.RemoveHandler("/bc");
        ECommonsMain.Dispose();
        _messagebusReceive.Dispose();
        _messagebusSpread.Dispose();
        _messagebusSend.Dispose();
        _overrideMovement.Dispose();
    }
    private void OpenMainUI()
    {
        MainWindow.IsOpen = true;
    }
    internal void SetFollow(bool on)
    {
        if (on)
        {
            Follow = true;
            _spreading = false;
        }
        else
        {
            Follow = false;
            Following = false;
        }
        Stop();
    }
    private void OnCommand(string command, string args)
    {
        //In response to the slash command, just display our main ui or turn Follow on or off

        if (args.ToUpper().Contains("FOLLOW ON") || args.ToUpper().Contains("FON"))
            SetFollow(true);
        else if (args.ToUpper().Contains("FOLLOW OFF") || args.ToUpper().Contains("FOFF"))
            SetFollow(false);
        else if (args.ToUpper().Contains("COMETOME "))
        {
            var go = GetGameObjectFromName(args[9..]);
            if (go != null)
            {
                StopAllMovement();
                MoveTo(go.Position, 0.1f);
            }
        }
        else if (args.ToUpper().Contains("CTM "))
        {
            var go = GetGameObjectFromName(args[4..]);
            if (go != null)
            {
                StopAllMovement();
                MoveTo(go.Position, 0.1f);
            }
        }
        else if (args.ToUpper().Contains("FOLLOWTARGET "))
        {
            FollowTarget = args[13..];
        }
        else if (args.ToUpper().Contains("FT "))
        {
            FollowTarget = args[3..];
        }
        else if (args.ToUpper().Contains("FOLLOWDISTANCE "))
        {
            FollowDistance = Convert.ToInt32(args[15..]);
        }
        else if (args.ToUpper().Contains("FD "))
        {
            FollowDistance = Convert.ToInt32(args[3..]);
        }
        else if (args.ToUpper().Contains("SPREAD"))
        {
            Stop();
            Spread();
        }
        else if (args.ToUpper().Contains("ACCEPTDUTY") || args.ToUpper().Contains("AD"))
        {
            AcceptDuty();
        }
        else if (args.ToUpper().Contains("EXITDUTY") || args.ToUpper().Contains("ED"))
        {
            //maybe make this have to be pressed / called / invoked twice to prevent accidental exiting
            this._exitDuty.Invoke((char)0);
        }
        else if (MainWindow.IsOpen)
            MainWindow.IsOpen = false;
        else
            MainWindow.IsOpen = true;
    }

    private void OnCommandBC(string command, string args)
    {
        if (ClientState.LocalPlayer is null) { return; }
        if (args == null) { return; }

        if (!args.ToUpper().Contains("FW=") || !args.ToUpper().Contains("C="))
        {
            Svc.Chat?.Print(new XivChatEntry
            {
                Message = "Broadcast: syntax = /broadcast FW=TOON1,TOON2,ETC or ALL or ALLBUTME or ALLBUT,TOON1,TOON2,ETC (Remove Space from ToonsFullName) C=/commandname args"
            });

            return;
        }
        var forWho = args.Substring(args.ToUpper().IndexOf("FW="), args.IndexOf(" ") - (args.ToUpper().IndexOf("FW="))).ToUpper();
        var rest = "C=" + args.Substring(args.ToUpper().IndexOf("C=") + 2);
        var ARGSc = forWho + " " + rest;

        if (ARGSc.Contains("ALLBUTME"))
            _messagebusSend.PublishAsync(Encoding.UTF8.GetBytes(ARGSc.Replace("ALLBUTME", "ALLBUT" + ClientState.LocalPlayer.Name.ToString().ToUpper().Replace(" ", ""))));
        else
            _messagebusSend.PublishAsync(Encoding.UTF8.GetBytes(ARGSc));
    }
    
    private void DrawUI()
    {
        //Draw Window
        this.WindowSystem.Draw();
    }
    private void StopAllMovement()
    {
        Follow = false;
        Following = false;
        _spreading = false;
        Stop();
    }
    private void Stop()
    {
        /*if (IPCManager.Vnavmesh_Path_IsRunning)
            IPCManager.Vnavmesh_Path_Stop();*/

        if (_overrideMovement.DesiredPosition != null)
            _overrideMovement.DesiredPosition = null;
    }
    private void MoveTo(Vector3 position, float precision = 0.1f)
    {
        _overrideAFK.ResetTimers();
        /*if (UseNavmesh)
        {
            if (_overrideMovement.DesiredPosition != null)
                _overrideMovement.DesiredPosition = null;

            if (IPCManager.Vnavmesh_Path_GetTolerance != precision)
                IPCManager.Vnavmesh_Path_SetTolerance(precision);

            IPCManager.Vnavmesh_Path_MoveTo(position);
        }
        else
        {*/
            //if (IPCManager.Vnavmesh_Path_IsRunning)
                //IPCManager.Vnavmesh_Path_Stop();

            if (_overrideMovement.Precision != precision)
                _overrideMovement.Precision = precision;

            _overrideMovement.DesiredPosition = position;
        //}
    }
    public unsafe void TeleportX(int amount)
    {
        //Teleport
        try
        { 
            var player = ClientState.LocalPlayer;
            SetPos.SetPosPos(player.Position + new Vector3(amount, 0,
            0));
        }
        catch (Exception e)
        {
            Log.Error(e.ToString());
            //throw;
        }
    }
    
    public unsafe void TeleportY(int amount)
    {
        //Teleport
        try
        {

            var player = ClientState.LocalPlayer;
            SetPos.SetPosPos(player.Position + new Vector3(0, amount,
            0));
        }
        catch (Exception e)
        {
            Log.Error(e.ToString());
            //throw;
        }
    }
    public unsafe void TeleportZ(int amount)
    {
        //Teleport
        try
        {

            var player = ClientState.LocalPlayer;
            SetPos.SetPosPos(player.Position + new Vector3(0, 0,
            amount));
        }
        catch (Exception e)
        {
            Log.Error(e.ToString());
            //throw;
        }
    }

    
    public void TTarget()
    {
        //Teleport
        try
        {
            if (Targets.Target != null)
            {
                var player = ClientState.LocalPlayer;
                SetPos.SetPosPos(Targets.Target.Position);
            }
            
        }
        catch (Exception e)
        {
            Log.Error(e.ToString());
            //throw;
        }
    }
    public void SetSpeed()
    {
        try
        {
            SetPos.MoveSpeed(float.Parse(SpeedBase));
        }
        catch (Exception e)
        {
            Log.Error(e.ToString());
            //throw;
        }
    }
    public void TeleportPOS(Vector3 telePos)
    {
        //Teleport
        try
        {
            SetPos.SetPosPos(telePos);
        }
        catch (Exception e)
        {
            Log.Error(e.ToString());
            //throw;
        }
    }
    public void AddPos()
    {
        //Add Start POS
        TeleportPosition = ClientState.LocalPlayer?.Position.ToString().Replace('<', ' ').Replace('>', ' ').Trim() ?? "";
    }
    
    public unsafe void Test()
    {
        //Just a Test function
        try
        {
            //Run through Vault


            //var player = ClientState.LocalPlayer;
            //SetPos.SetPosPos(player.Position + new System.Numerics.Vector3(0, 1,
            //0)) ;
            //Dalamud.Logging.Log.Log(DalamudAPI.PartyList[0].Name.ToString());
            //DalamudAPI.Targets.SetTarget(gameObject);
            //Set the Games MovementMove 0=Standard, 1=Legacy
            //DalamudAPI.GameConfig.UiControl.Set("MoveMode", 1);
            /*if (DalamudAPI.GameConfig.UiControl.GetUInt("MoveMode") == 0)
                textFollow1 = "Standard";
            else if (DalamudAPI.GameConfig.UiControl.GetUInt("MoveMode") == 1)
                textFollow1 = "Legacy";
            //ClickSelectYesNo.Using(default).Yes();
            *if (TryGetAddonByName<AtkUnitBase>("SelectYesno", out var addon))
            {
                Log.Information("got addon");
            }
            else
            {
                Log.Information("no addon found");
            }
            */
            //var addon = GameGui.GetAddonByName("SelectYesno", 1);
            // var addon = (AtkUnitBase*)GameGui.GetAddonByName("SelectYesno", 1);
            // addonPTR->SendClick(addon, EventType.CHANGE, 0, ((AddonSelectYesno*)addon)->YesButton->AtkComponentBase.OwnerNode);
            //ClickSelectYesNo.Using(addon).Yes();
            //addon.
            // Log.Information(addon->AtkValues[0].ToString());
            //textTest = addon->AtkValues[0].ToString();
            //InteractWithObject("Red Coral Formation");
            //var go = GetGroupMemberObjectByRole(4);
            //Log.Info("Our Healer is: " + go.Name.ToString());
            //go = GetGroupMemberObjectByRole(1);
            //Log.Info("Our Tank is: " + go.Name.ToString());
            //Log.Info("Boss: " + IsBossFromIcon((BattleChara)Targets.Target));;

            /*var objs = GetObjectInRadius(Objects, 30);
            foreach (var obj in objs) 
            {
                Log.Info("Name: " + obj.Name.ToString() + " Distance: " + DistanceToPlayer(obj));            
            }*/
            /*var objs = GetObjectInRadius(Objects, 30);
            var battleCharaObjs = objs.OfType<BattleChara>();
            GameObject bossObject = default;
            foreach (var obj in battleCharaObjs)
            {
                Log.Info("Checking: " + obj.Name.ToString());
                if (IsBossFromIcon(obj))
                    bossObject = obj;
            }
            if (bossObject)
                Log.Info("Boss: " + bossObject.Name.ToString());*/
            //Log.Info(DistanceToPlayer(Targets.Target).ToString());
            /*var v3o = new RcVec3f(-113.888f, 150, 210.794f);
             var path = meshesDirectory + "/" + ClientState.TerritoryType.ToString() + ".navmesh";
             var fileStream = File.Open(path, FileMode.Open);
             var nmd = new NavMeshDetour();
             var point = nmd.FindNearestPolyPoint(v3o, new RcVec3f(0, 200, 0), fileStream);
             Log.Info(point.ToString());
             Navigate(new RcVec3f(ClientState.LocalPlayer.Position.X, ClientState.LocalPlayer.Position.Y, ClientState.LocalPlayer.Position.Z), point);*/
            /*var i = Objects.OrderBy(o => DistanceToPlayer(o)).Where(p => p.Name.ToString().ToUpper().Equals("MINERAL DEPOSIT"));

            foreach (var o in i) 
            { 
                Log.Info(o.Name.ToString() + " - " + DistanceToPlayer(o).ToString() + " IsTargetable" + o.IsTargetable);
            }*/
            //if (ECommons.Reflection.DalamudReflector.TryGetDalamudPlugin("vnavmesh", out var _))
            //{
            //PluginInterface.GetIpcSubscriber<bool, object>("vnavmesh.SetMovementAllowed").InvokeAction(true);

            //}
            /*
            //This clicks the Item in the Gathering Window by Index 
            var addon = (AddonGathering*)GameGui.GetAddonByName("Gathering", 1);
            if (addon != null)
            {
                var ids = new List<uint>()
                {
                    addon->GatheredItemId1,
                    addon->GatheredItemId2,
                    addon->GatheredItemId3,
                    addon->GatheredItemId4,
                    addon->GatheredItemId5,
                    addon->GatheredItemId6,
                    addon->GatheredItemId7,
                    addon->GatheredItemId8
                };
                ids.ForEach(p => Log.Info(p.ToString()));
            }
            //var addon2 = (AtkUnitBase*)GameGui.GetAddonByName("Gathering");
            var receiveEventAddress = new nint(addon->AtkUnitBase.AtkEventListener.vfunc[2]);
            var eventDelegate = Marshal.GetDelegateForFunctionPointer<ReceiveEventDelegate>(receiveEventAddress)!;

            var target = AtkStage.GetSingleton();
            var eventData = EventData.ForNormalTarget(target, &addon->AtkUnitBase);
            var inputData = InputData.Empty();

            eventDelegate.Invoke(&addon->AtkUnitBase.AtkEventListener, ClickLib.Enums.EventType.CHANGE, (uint)2, eventData.Data, inputData.Data);*/

            //var addon = GameGui.GetAddonByName("SelectYesno", 1);
            //var add = (AddonSelectYesno*)addon;
            //Log.Info(Vector3.DistanceSquared(Svc.ClientState.LocalPlayer.Position,new Vector3(44.4254f,-9, 112.402f)).ToString());
            // var Agent = AgentContentsFinder.Instance();
            //Agent->OpenRegularDuty(10);
            var taskManager = new TaskManager();
            //s2->UiModule->
            //taskManager.Enqueue(() => OpenDawnStory());

            //Callback.Fire((AtkUnitBase*)GameGui.GetAddonByName("DawnStory", 1), true, 0, 84);

            //taskManager.DelayNext(2000);
            // taskManager.Enqueue(() => OpenDawnStory());
            var addon = (AtkUnitBase*)GameGui.GetAddonByName("DawnStory", 1);
            /*taskManager.Enqueue(() => OpenDawnStory());
            taskManager.DelayNext(2000);
            taskManager.Enqueue(() => addon = (AtkUnitBase*)GameGui.GetAddonByName("DawnStory", 1));
            taskManager.Enqueue(() => SelectExpansionInDawnStory(addon, 1));
            taskManager.DelayNext(2000);*/
            taskManager.Enqueue(() => SelectDutyInDawnStory(addon, "The Vault"));
            /*taskManager.DelayNext(2000);
            taskManager.Enqueue(() => addon->Draw());
            taskManager.DelayNext(2000);
            taskManager.Enqueue(() => ClickDawnStoryRegisterForDuty(addon));*/
        }
        catch (Exception e)
        {
            Log.Error(e.ToString());
            //throw;
        }

    }
    private unsafe static void SelectExpansionInDawnStory(AtkUnitBase* addon, int expansion)
    {
        int expRadioButton;
        switch (expansion)
        {
            case 0:
                expRadioButton = 39;
                break;
            case 1:
                expRadioButton = 38;
                break;
            case 2:
                expRadioButton = 37;
                break;
            case 3:
                expRadioButton = 36;
                break;
            case 4:
                expRadioButton = 35;
                break;
            default:
                expRadioButton = -1;
                break;
        }

        ClickAddonRadioButtonByNode(addon, expRadioButton);
    }
    public unsafe static void SelectDutyInDawnStory(AtkUnitBase* addon, string dutyName)
    {
        try
        {
            var atkComponentTreeListDungeons = (AtkComponentTreeList*)addon->UldManager.NodeList[7]->GetComponent();
            for (ulong i = 0; i < atkComponentTreeListDungeons->Items.Size(); i++)
            {
                var a3 = atkComponentTreeListDungeons->Items.Get(i).Value->Renderer->AtkComponentButton.ButtonTextNode->NodeText;
                if (a3.ToString().Equals(dutyName))
                {
                    var item = atkComponentTreeListDungeons->GetItem((uint)i);
                    if (item != null)
                    {
                        atkComponentTreeListDungeons->SelectItem((uint)i, true);
                        atkComponentTreeListDungeons->AtkComponentList.SelectItem((int)i, true);
                        atkComponentTreeListDungeons->AtkComponentList.DispatchItemEvent((int)i, AtkEventType.MouseClick);
                        var atkEvent = item->Renderer->AtkComponentButton.AtkComponentBase.AtkResNode->AtkEventManager.Event;
                        var atkEventType = atkEvent->Type;
                        var atkEventParam = (int)atkEvent->Param;

                        addon->ReceiveEvent(AtkEventType.MouseClick, 3, atkEvent);
                        return;
                    }
                }
            }
        }
        catch(Exception e) { Log.Info($"{e}"); }
    }
    public unsafe static void OpenDawnStory() => AgentModule.Instance()->GetAgentByInternalID(341)->Show();

    public unsafe static void ClickDawnStoryRegisterForDuty(AtkUnitBase* addon) => ClickAddonButtonByNode(addon, 84);

    public unsafe static void ClickAddonButtonByNode(AtkUnitBase* addon, int node)
    {
        if (node == -1)
            return;

        addon->ReceiveEvent(((AtkComponentButton*)addon->UldManager.NodeList[node]->GetComponent())->AtkComponentBase.OwnerNode->AtkResNode.AtkEventManager.Event->Type, (int)(AtkComponentButton*)addon->UldManager.NodeList[node]->GetComponent()->OwnerNode->AtkResNode.AtkEventManager.Event->Param, (AtkEvent*)((AtkComponentButton*)addon->UldManager.NodeList[node]->GetComponent()->OwnerNode->AtkResNode.AtkEventManager.Event));
    }
    public unsafe static void ClickAddonRadioButtonByNode(AtkUnitBase* addon, int node)
    {
        if (node == -1)
            return;

        addon->ReceiveEvent(((AtkComponentRadioButton*)addon->UldManager.NodeList[node]->GetComponent())->AtkComponentBase.OwnerNode->AtkResNode.AtkEventManager.Event->Type, (int)(AtkComponentRadioButton*)addon->UldManager.NodeList[node]->GetComponent()->OwnerNode->AtkResNode.AtkEventManager.Event->Param, (AtkEvent*)((AtkComponentRadioButton*)addon->UldManager.NodeList[node]->GetComponent()->OwnerNode->AtkResNode.AtkEventManager.Event));
    }
}