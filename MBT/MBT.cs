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
using System.IO;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons;
using AutoDuty.Managers;
namespace MBT;

/// <summary>
/// MBT v1
/// </summary>

public class MBT : IDalamudPlugin
{
    public string textFollow1 = "";
    public Vector4 textFollow1Color = new(255f, 0f, 0f, 1f);
    public string textFollow2 = "";
    public Vector4 textFollow2Color = new(255f, 0f, 0f, 1f);
    public string textFollow3 = "";
    public Vector4 textFollow3Color = new(255f, 0f, 0f, 1f);
    public bool follow = false;
    public bool following = false;
    private bool spreading = false;
    public int followDistance = 1;
    public string followTarget = "";
    public GameObject? followTargetObject = null;
    public bool stopNavigating = false;
    public string teleportPOS = "";
    public string speedBase = "";
    public List<string> ListBoxText { get; set; } = new List<string>();
    public List<string> ListBoxPOSText { get; set; } = new List<string>();
    private delegate void ExitDutyDelegate(char timeout);
    private ExitDutyDelegate exitDuty;
    public string Name => "MBT";
    public static MBT Plugin { get; private set; }
    //public Configuration Configuration { get; init; }
    public WindowSystem WindowSystem = new("MBT");

    private MainWindow MainWindow { get; init; }

    TinyMessageBus messagebus1 = new("DalamudBroadcaster");
    TinyMessageBus messagebus2 = new("DalamudBroadcaster");
    TinyMessageBus messagebus3 = new("DalamudBroadcasterSpread");

    public DirectoryInfo configDirectory;
    public DirectoryInfo meshesDirectory; 
    public DirectoryInfo pathsDirectory;

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

            //Condition.ConditionChange += Condition_OnConditionChange;

            //messagebus1.MessageReceived +=
            //(sender, e) => MessageReceived(Encoding.UTF8.GetString((byte[])e.Message));
            messagebus2.MessageReceived +=
                (sender, e) => MessageReceived(Encoding.UTF8.GetString((byte[])e.Message));
            messagebus3.MessageReceived +=
                (sender, e) => MessageReceivedSpread(Encoding.UTF8.GetString((byte[])e.Message));

            var exitDuty = 
            SigScanner.ScanText("40 53 48 83 ec 20 48 8b 05 ?? ?? ?? ?? 0f b6 d9");

            this.exitDuty = Marshal.GetDelegateForFunctionPointer<ExitDutyDelegate>(exitDuty);
        }
        catch (Exception e) { Log.Info($"Failed loading plugin\n{e}"); }
    }

    private static unsafe void AcceptDuty()
    {
        Callback((AtkUnitBase*)GameGui.GetAddonByName("ContentsFinderConfirm", 1), 8);
    }
    private unsafe static void Callback(AtkUnitBase* unitBase, params object[] values)
    {
        //if (unitBase == null) throw new Exception("Null UnitBase");
        if (unitBase == null) return;
        var atkValues = (AtkValue*)Marshal.AllocHGlobal(values.Length * sizeof(AtkValue));
        if (atkValues == null) return;
        try
        {
            for (var i = 0; i < values.Length; i++)
            {
                var v = values[i];
                switch (v)
                {
                    case uint uintValue:
                        atkValues[i].Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.UInt;
                        atkValues[i].UInt = uintValue;
                        break;
                    case int intValue:
                        atkValues[i].Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int;
                        atkValues[i].Int = intValue;
                        break;
                    case float floatValue:
                        atkValues[i].Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Float;
                        atkValues[i].Float = floatValue;
                        break;
                    case bool boolValue:
                        atkValues[i].Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Bool;
                        atkValues[i].Byte = (byte)(boolValue ? 1 : 0);
                        break;
                    case string stringValue:
                        {
                            atkValues[i].Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.String;
                            var stringBytes = Encoding.UTF8.GetBytes(stringValue);
                            var stringAlloc = Marshal.AllocHGlobal(stringBytes.Length + 1);
                            Marshal.Copy(stringBytes, 0, stringAlloc, stringBytes.Length);
                            Marshal.WriteByte(stringAlloc, stringBytes.Length, 0);
                            atkValues[i].String = (byte*)stringAlloc;
                            break;
                        }
                    default:
                        throw new ArgumentException($"Unable to convert type {v.GetType()} to AtkValue");
                }
            }

            unitBase->FireCallback(values.Length, atkValues);
        }
        finally
        {
            for (var i = 0; i < values.Length; i++)
            {
                if (atkValues[i].Type == FFXIVClientStructs.FFXIV.Component.GUI.ValueType.String)
                {
                    Marshal.FreeHGlobal(new IntPtr(atkValues[i].String));
                }
            }
            Marshal.FreeHGlobal(new IntPtr(atkValues));
        }
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
            messagebus3.PublishAsync(Encoding.UTF8.GetBytes(playerObjectId + "," + partyList[i] + "," + "*" + j +"*"));
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
        follow = false;
        spreading = true;
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
                        IPCManager.Vnavmesh_Path_MoveTo(new Vector3(pos.X - 10, pos.Y, pos.Z));
                        break;
                    case "*2*":
                        IPCManager.Vnavmesh_Path_MoveTo(new Vector3(pos.X + 10, pos.Y, pos.Z + 5));
                        break;
                    case "*3*":
                        IPCManager.Vnavmesh_Path_MoveTo(new Vector3(pos.X + 10, pos.Y, pos.Z - 5));
                        break;
                }
            }
        }
    }
    public void SetTarget()
    {
        //If PlayerCharacter's target is not null, Set our followTarget InputText to our Target Object's .Name field
        if (Targets.Target != null)
        {
            followTarget = Targets.Target.Name.ToString();
        }
    }
    public void SetFollowStatus(bool sts, string name, string distance, Vector4 color)
    {
        //Set UI TextColored's Values
        string? FollowingSTS;
        if (sts)
            FollowingSTS = "On";
        else
            FollowingSTS = "Off";
        textFollow1 = " " + FollowingSTS;
        textFollow2 = "Name: " + name;
        textFollow3 = "Distance: " + distance + " <= " + followDistance;
        textFollow1Color = color;
        textFollow2Color = color;
        textFollow3Color = color;
    }

    private static GameObject? GetGameObjectFromName(string _objectName) => Objects.FirstOrDefault(s => s.Name.ToString().Equals(_objectName));

    public bool GetFollowTargetObject()
    {
        var ftarget = GetGameObjectFromName(followTarget);
        if (ftarget == null)
        {
            followTargetObject = null;
            SetFollowStatus(false, followTarget + " not found", "0", new(255f, 0f, 0f, 1f));
            if (following)
                IPCManager.Vnavmesh_Path_Stop();
            return false;
        }
        else
        {
            followTargetObject = ftarget;
            return true;
        }
    }

    public void OnGameFrameworkUpdate(IFramework framework)
    {
        //If follow is not enabled clear TextColored's and return
        if (!follow)
        {
            SetFollowStatus(false, "", "", new(255f, 0f, 0f, 1f));
            if (following && !spreading)
            {
                IPCManager.Vnavmesh_Path_Stop();
                following = false;
            }
            return;
        }

        //If LocalPlayer object is null return (we are not logged in or between zones etc..)
        if (ClientState.LocalPlayer == null) return;

        //If followTarget is not empty GetFollowTargetObject then set our player variable and calculate the distance
        //between player and followTargetObject and if distance > followDistance move to the followTargetObject
        if (!string.IsNullOrEmpty(followTarget))
        {
            try
            {
                /// Need to figure out how to handle this
                /// because if the Object leaves the zone it
                /// sorta leaves a ghost GO that is still .IsValid
                /// and all its data is locked to where it was
                /// when it left the zone, this current way is
                /// gross and very inefficient

                if (!GetFollowTargetObject())
                    return;
                if (followTargetObject == null) return;

                var player = ClientState.LocalPlayer;

                //if (targetFollowTargetsTargets)
                //{
                //    if (player.TargetObjectId != followTargetObject.TargetObjectId && followTargetObject.TargetObjectId != 0)
                //    {
                //        Log.Info("1");
                //        Targets.Target = followTargetObject.TargetObject;
                //        Log.Info("Follow:" + followTargetObject.TargetObject.Name);

                //    }
                //}
                if (!follow)
                {
                    SetFollowStatus(false, "", "", new(255f, 0f, 0f, 1f));
                    if (following && !spreading)
                    {
                        IPCManager.Vnavmesh_Path_Stop(); 
                        following = false;
                    }
                    return;
                }
                var distance = Convert.ToInt32(Vector3.Distance(new Vector3(player.Position.X, player.Position.Y, player.Position.Z), new Vector3(followTargetObject.Position.X, followTargetObject.Position.Y, followTargetObject.Position.Z)));

                SetFollowStatus(true, followTargetObject.Name.ToString(), distance.ToString(), new(0f, 255f, 0f, 1f));

                if ((distance > (followDistance + .1f)) && distance < 100)
                {
                    following = true;
                    //Move.Move.MoveTo(true, followTargetObject.Position, followDistance);
                    IPCManager.Vnavmesh_Path_SetTolerance(followDistance + .1f);
                    IPCManager.Vnavmesh_Path_MoveTo(followTargetObject.Position);
                }
                else if (following)
                {
                    following = false;
                    //Move.Move.MoveTo(false);
                    IPCManager.Vnavmesh_Path_Stop();
                }
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
                SetFollowStatus(false, "", "", new(255f, 0f, 0f, 1f));
                if (following)
                {
                    following = false;
                    IPCManager.Vnavmesh_Path_Stop();
                }
                //throw;
            }
        }
        else
        {
            SetFollowStatus(false, "No follow target set", "", new(255f, 0f, 0f, 1f));
            if (following)
            {
                following = false;
                IPCManager.Vnavmesh_Path_Stop();
            }
        }
    }
    public void Dispose()
    {
        //RemoveAllWindows and Dispose of them, Disable FrameworkUpdate and remove Commands
        WindowSystem.RemoveAllWindows();
        MainWindow.Dispose();
        Framework.Update -= OnGameFrameworkUpdate;
        Commands.RemoveHandler("/mbt");
        Commands.RemoveHandler("/bc");
        messagebus1.Dispose();
        messagebus2.Dispose();
    }
    private void OpenMainUI()
    {
        MainWindow.IsOpen = true;
    }
    private void OnCommand(string command, string args)
    {
        //In response to the slash command, just display our main ui or turn Follow on or off

        if (args.ToUpper().Contains("FOLLOW ON") || args.ToUpper().Contains("FON"))
        {
            follow = true;
            spreading = false;
            IPCManager.Vnavmesh_Path_Stop();
        }
        else if (args.ToUpper().Contains("FOLLOW OFF") || args.ToUpper().Contains("FOFF"))
        {
            follow = false;
            following = false;
            IPCManager.Vnavmesh_Path_Stop();
        }
        else if (args.ToUpper().Contains("COMETOME "))
        {
            var go = GetGameObjectFromName(args[9..]);
            if (go != null)
            {
                StopAllMovement();
                IPCManager.Vnavmesh_Path_SetTolerance(0.1f);
                IPCManager.Vnavmesh_Path_MoveTo(go.Position);
            }
        }
        else if (args.ToUpper().Contains("CTM "))
        {
            var go = GetGameObjectFromName(args[4..]);
            if (go != null)
            {
                StopAllMovement();
                IPCManager.Vnavmesh_Path_SetTolerance(0.1f);
                IPCManager.Vnavmesh_Path_MoveTo(go.Position);
            }
        }
        else if (args.ToUpper().Contains("FOLLOWTARGET "))
        {
            followTarget = args[13..];
        }
        else if (args.ToUpper().Contains("FT "))
        {
            followTarget = args[3..];
        }
        else if (args.ToUpper().Contains("FOLLOWDISTANCE "))
        {
            followDistance = Convert.ToInt32(args[15..]);
        }
        else if (args.ToUpper().Contains("FD "))
        {
            followDistance = Convert.ToInt32(args[3..]);
        }
        else if (args.ToUpper().Contains("SPREAD"))
        {
            IPCManager.Vnavmesh_Path_Stop();
            Spread();
        }
        else if (args.ToUpper().Contains("ACCEPTDUTY") || args.ToUpper().Contains("AD"))
        {
            AcceptDuty();
        }
        else if (args.ToUpper().Contains("EXITDUTY") || args.ToUpper().Contains("ED"))
        {
            //maybe make this have to be pressed / called / invoked twice to prevent accidental exiting
            this.exitDuty.Invoke((char)0);
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
            Chat?.Print(new XivChatEntry
            {
                Message = "Broadcast: syntax = /broadcast FW=TOON1,TOON2,ETC or ALL or ALLBUTME or ALLBUT,TOON1,TOON2,ETC (Remove Space from ToonsFullName) C=/commandname args"
            });

            return;
        }
        var forWho = args.Substring(args.ToUpper().IndexOf("FW="), args.IndexOf(" ") - (args.ToUpper().IndexOf("FW="))).ToUpper();
        var rest = "C=" + args.Substring(args.ToUpper().IndexOf("C=") + 2);
        var ARGSc = forWho + " " + rest;

        if (ARGSc.Contains("ALLBUTME"))
            messagebus1.PublishAsync(Encoding.UTF8.GetBytes(ARGSc.Replace("ALLBUTME", "ALLBUT" + ClientState.LocalPlayer.Name.ToString().ToUpper().Replace(" ", ""))));
        else
            messagebus1.PublishAsync(Encoding.UTF8.GetBytes(ARGSc));
    }
    
    private void DrawUI()
    {
        //Draw Window
        this.WindowSystem.Draw();
    }
    private void StopAllMovement()
    {
        follow = false;
        following = false;
        spreading = false;
        IPCManager.Vnavmesh_Path_Stop();
    }
    public unsafe void teleportX(int amount)
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
    
    public unsafe void teleportY(int amount)
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
    public unsafe void teleportZ(int amount)
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

    
    public unsafe void TTarget()
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
    public unsafe void SetSpeed()
    {
        try
        {
            SetPos.MoveSpeed(float.Parse(speedBase));
        }
        catch (Exception e)
        {
            Log.Error(e.ToString());
            //throw;
        }
    }
    public unsafe void TeleportPOS(Vector3 telePos)
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
    public unsafe void AddPos()
    {
        //Add Start POS
        teleportPOS = ClientState.LocalPlayer.Position.ToString().Replace('<', ' ').Replace('>', ' ').Trim();
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
        }
        catch (Exception e)
        {
            Log.Error(e.ToString());
            //throw;
        }
    }
}