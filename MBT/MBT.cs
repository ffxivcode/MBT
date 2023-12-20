using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using TinyIpc.Messaging;

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
    public bool targetFollowTargetsTargets = false;
    public GameObject? followTargetObject = null;
    private delegate void ExitDutyDelegate(char timeout);
    private ExitDutyDelegate exitDuty;

    public string Name => "Follow";
    public static MBT Plugin { get; private set; }
    //public Configuration Configuration { get; init; }
    public WindowSystem WindowSystem = new("Follow");

    private MainWindow MainWindow { get; init; }

    TinyMessageBus messagebus1 = new("DalamudBroadcaster");
    TinyMessageBus messagebus2 = new("DalamudBroadcaster");
    TinyMessageBus messagebus3 = new("DalamudBroadcasterSpread");

    XIVRunner.XIVRunner _runner;

    public MBT(
        DalamudPluginInterface pluginInterface)
    {
        try
        {
            Plugin = this;
            DalamudAPI.Initialize(pluginInterface);
            _runner = XIVRunner.XIVRunner.Create(pluginInterface);
            _runner.Enable = true;
            _runner.Precision = followDistance + .1f;
            _runner.UseMount = false;
            _runner.TryJump = false;
            //Create MainWindow UI
            MainWindow = new MainWindow(this);
            WindowSystem.AddWindow(MainWindow);

            //Add Commands  ---- More to come
            DalamudAPI.CommandManager.AddHandler("/bc", new CommandInfo(OnCommandBC)
            {
                HelpMessage = "/bc fw=toon1,toon2,ETC or all or allbutme or allbut,toon1,toon2,ETC (Remove Space from ToonsFullName) C=/commandname args\"\n" +
                "example: /bc FW=ALL C=/mbt ft Toon Name"
            });
            DalamudAPI.CommandManager.AddHandler("/mbt", new CommandInfo(OnCommand)
            {
                HelpMessage = "/mbt -> opens main window\n" +
                "/mbt follow on/off -> turns follow on or off\n" +
                "/mbt target on/off - >turns target on or off\n" +
                "/mbt followtarget Player Name -> sets follow target to Player Name\n" +
                "/mbt followdistance # -> sets Follow distance to # (must be int)\n" +
                "/mbt spread -> Spreads toons away from LocalPlayer\n" +
                "/mbt exitduty -> Immediately exits the duty\n" +
                "/mbt acceptduty -> Immediately accepts the duty finder popup\n"
            });

            //Draw UI
            DalamudAPI.PluginInterface.UiBuilder.Draw += DrawUI;
            ChatCommand.Initialize();
            //Attach our OnGameFrameworkUpdate function to our game's Framework Update (called once every frame)
            DalamudAPI.Framework.Update += OnGameFrameworkUpdate;

            //messagebus1.MessageReceived +=
                //(sender, e) => MessageReceived(Encoding.UTF8.GetString((byte[])e.Message));
            messagebus2.MessageReceived +=
                (sender, e) => MessageReceived(Encoding.UTF8.GetString((byte[])e.Message));
            messagebus3.MessageReceived +=
                (sender, e) => MessageReceivedSpread(Encoding.UTF8.GetString((byte[])e.Message));

            var exitDuty = 
            DalamudAPI.SigScanner.ScanText("40 53 48 83 ec 20 48 8b 05 ?? ?? ?? ?? 0f b6 d9");

            this.exitDuty = Marshal.GetDelegateForFunctionPointer<ExitDutyDelegate>(exitDuty);
        }
        catch (Exception e) { DalamudAPI.PluginLog.Info($"Failed loading plugin\n{e}"); }

    }
    private static unsafe void AcceptDuty()
    {
        Callback((AtkUnitBase*)DalamudAPI.GameGui.GetAddonByName("ContentsFinderConfirm", 1), 8);
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
        if (DalamudAPI.ClientState.LocalPlayer == null) return;
        if (DalamudAPI.PartyList == null) return;
        var playerObjectId = DalamudAPI.ClientState.LocalPlayer.ObjectId;
        List<uint> partyList = new();

        foreach (var partyMember in DalamudAPI.PartyList)
        {
            if (partyMember.ObjectId == DalamudAPI.ClientState.LocalPlayer.ObjectId) { continue; }

            if(partyMember.GameObject != null)
                partyList.Add(partyMember.GameObject.ObjectId);
        }
        
        for(int i = 0; i < partyList.Count; i++)
        {
            var j = i + 1;
            DalamudAPI.PluginLog.Info("OnCommandSpread: PartyList: " + partyList[i] + " : " + playerObjectId + "," + partyList[i] + "," + "*" + j + "*");
            messagebus3.PublishAsync(Encoding.UTF8.GetBytes(playerObjectId + "," + partyList[i] + "," + "*" + j +"*"));
        }
    }

    private void MessageReceived(string message)
    {
        if (DalamudAPI.ClientState.LocalPlayer is null) { return; }

        List<string> forWhos;
        if (message.Contains("FW=ALLBUT"))
        {
            if (message.Contains("FW=ALLBUT" + DalamudAPI.ClientState.LocalPlayer.Name.ToString().ToUpper().Replace(" ", "")))
                return;
            else
                forWhos = new List<string> { (DalamudAPI.ClientState.LocalPlayer.Name.ToString().ToUpper().Replace(" ", "")) };
        }
        else if (message.Contains("FW=ALL "))
            forWhos = new List<string> { (DalamudAPI.ClientState.LocalPlayer.Name.ToString().ToUpper().Replace(" ", "")) };
        else
            forWhos = message.Substring(message.IndexOf("FW=") + 3, message.IndexOf(" ") - (message.IndexOf("FW=") + 3)).Split(',').ToList();

        if (forWhos.Any(i => i.Equals(DalamudAPI.ClientState.LocalPlayer.Name.ToString().ToUpper().Replace(" ", ""))))
        {
            DalamudAPI.PluginLog.Info(message.Substring(message.IndexOf("C=") + 2));
            ChatCommand.ExecuteCommand(message.Substring(message.IndexOf("C=") + 2));
        }
    }

    private void MessageReceivedSpread(string message)
    {
        DalamudAPI.PluginLog.Info("MessageRecieved: " + message);
        if (DalamudAPI.ClientState.LocalPlayer == null) return;
        follow = false;
        spreading = true;
        var messageList = message.Split(',').ToList();
        var playerObjectId = DalamudAPI.ClientState.LocalPlayer.ObjectId;
        DalamudAPI.PluginLog.Info("messageList.Count: " + messageList.Count + " messageList[0]: " + messageList[0] + " messageList[1]: " + messageList[1] + " messageList[2]: " + messageList[2]);
        
        if (Convert.ToUInt32(messageList[1]) == playerObjectId)
        {
            var sender = DalamudAPI.ObjectTable.Where(s => s.ObjectId == Convert.ToUInt32(messageList[0]));
            if (sender != null)
            {
                var pos = sender.First().Position;
                switch (messageList[2])
                {
                    case "*1*":
                        _runner.NaviPts.Enqueue(new Vector3(pos.X - 10, pos.Y, pos.Z));
                        break;
                    case "*2*":
                        _runner.NaviPts.Enqueue(new Vector3(pos.X + 10, pos.Y, pos.Z + 5));
                        break;
                    case "*3*":
                        _runner.NaviPts.Enqueue(new Vector3(pos.X + 10, pos.Y, pos.Z - 5));
                        break;
                }
            }
        }
    }
    
    public void SetTarget()
    {
        //If PlayerCharacter's target is not null, Set our followTarget InputText to our Target Object's .Name field
        if (DalamudAPI.TargetManager.Target != null)
        {
            followTarget = DalamudAPI.TargetManager.Target.Name.ToString();
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
        textFollow1 = "Following: " + FollowingSTS;
        textFollow2 = "Name: " + name;
        textFollow3 = "Distance: " + distance + " <= " + followDistance;
        textFollow1Color = color;
        textFollow2Color = color;
        textFollow3Color = color;
    }

    public bool GetFollowTargetObject()
    {
        //Get all spawned objects into obj and pull the object with the Name == followTarget
        //If there are none display not found and return false otherwise grab the first object
        //in the list with that name and set it to your global followTargetObject
        var obj = DalamudAPI.ObjectTable;
        if (obj == null) return false;
        if (followTarget == null) return false;
        if (followTargetObject != null)
        {
            if (obj.Contains(followTargetObject) && followTargetObject.Name.Equals(followTarget))
                return true;
        }

        var ftarget = obj.Where(s => s.Name.ToString() == followTarget);
        if (!ftarget.Any())
        {
            followTargetObject = null;
            SetFollowStatus(false, followTarget + " not found", "0", new(255f, 0f, 0f, 1f));
            if (following)
                _runner.NaviPts.Clear();
            return false;
        }
        var ftar = ftarget.ToList();
        followTargetObject = ftar[0];
        return true;
    }

    public void OnGameFrameworkUpdate(IFramework framework)
    {
        //If follow is not enabled clear TextColored's and return
        if (!follow && !targetFollowTargetsTargets)
        {
            SetFollowStatus(false, "", "", new(255f, 0f, 0f, 1f));
            if (_runner.MovingValid && !spreading)
                _runner.NaviPts.Clear();
            return;
        }

        //If LocalPlayer object is null return (we are not logged in or between zones etc..)
        if (DalamudAPI.ClientState.LocalPlayer == null) return;

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
                
                var player = DalamudAPI.ClientState.LocalPlayer;

                if (targetFollowTargetsTargets)
                {                    
                    if (player.TargetObjectId != followTargetObject.TargetObjectId && followTargetObject.TargetObjectId != 0)
                    {
                        DalamudAPI.PluginLog.Info("1");
                        DalamudAPI.TargetManager.Target = followTargetObject.TargetObject;
                        DalamudAPI.PluginLog.Info("Follow:" + followTargetObject.TargetObject.Name);

                    }
                }
                if (!follow)
                {
                    SetFollowStatus(false, "", "", new(255f, 0f, 0f, 1f));
                    if (_runner.MovingValid && !spreading)
                        _runner.NaviPts.Clear();
                    return;
                }
                var distance = Convert.ToInt32(Vector3.Distance(new Vector3(player.Position.X, player.Position.Y, player.Position.Z), new Vector3(followTargetObject.Position.X, followTargetObject.Position.Y, followTargetObject.Position.Z)));

                SetFollowStatus(true, followTargetObject.Name.ToString(), distance.ToString(), new(0f, 255f, 0f, 1f));

                if ((distance > (followDistance + .1f)) && distance < 100)
                {
                    following = true;
                    //Move.Move.MoveTo(true, followTargetObject.Position, followDistance);
                    _runner.Precision = followDistance + .1f;
                    //_runner.NaviPts.Clear();
                    _runner.NaviPts.Enqueue(followTargetObject.Position);
                }
                else if (following)
                {
                    following = false;
                    //Move.Move.MoveTo(false);
                    _runner.NaviPts.Clear();
                }
            }
            catch (Exception e)
            {
                DalamudAPI.PluginLog.Error(e.ToString());
                SetFollowStatus(false, "", "", new(255f, 0f, 0f, 1f));
                if (following)
                    _runner.NaviPts.Clear();
                //throw;
            }
        }
        else
        {
            SetFollowStatus(false, "No follow target set", "", new(255f, 0f, 0f, 1f));
            if (following)
                _runner.NaviPts.Clear();
        }
    }

    public void Dispose()
    {
        //RemoveAllWindows and Dispose of them, Disable FrameworkUpdate and remove Commands and change back to Legacy if needed
        WindowSystem.RemoveAllWindows();
        ((IDisposable)MainWindow).Dispose();
        DalamudAPI.Framework.Update -= OnGameFrameworkUpdate;
        DalamudAPI.CommandManager.RemoveHandler("/mbt"); 
        DalamudAPI.CommandManager.RemoveHandler("/bc");
        _runner.NaviPts.Clear();
        messagebus1.Dispose();
        messagebus2.Dispose();
        _runner?.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        //In response to the slash command, just display our main ui or turn Follow on or off
        if (args.ToUpper().Contains("FOLLOW ON"))
        {
            follow = true;
            spreading = false;
            _runner.NaviPts.Clear();
        }
        else if (args.ToUpper().Contains("FOLLOW OFF"))
        {
            follow = false;
            following = false;
            _runner.NaviPts.Clear();
        }
        else if (args.ToUpper().Contains("TARGET ON"))
        {
            targetFollowTargetsTargets = true;
        }
        else if (args.ToUpper().Contains("TARGET OFF"))
        {
            targetFollowTargetsTargets = false;
        }
        else if (args.ToUpper().Contains("FOLLOWTARGET"))
        {
            followTarget = args[13..];
        }
        else if (args.ToUpper().Contains("FOLLOWDISTANCE"))
        {
            followDistance = Convert.ToInt32(args[15..]);
        }
        else if (args.ToUpper().Contains("SPREAD"))
        {
            _runner.NaviPts.Clear();
            Spread();
        }
        else if (args.ToUpper().Contains("ACCEPTDUTY"))
        {
            AcceptDuty();
        }
        else if (args.ToUpper().Contains("EXITDUTY"))
        {
            this.exitDuty.Invoke((char)0);
        }
        else if (MainWindow.IsOpen)
            MainWindow.IsOpen = false;
        else
            MainWindow.IsOpen = true;
    }

    private void OnCommandBC(string command, string args)
    {
        if (DalamudAPI.ClientState.LocalPlayer is null) { return; }
        if (args == null) { return; }

        if (!args.ToUpper().Contains("FW=") || !args.ToUpper().Contains("C="))
        {
            DalamudAPI.ChatGui?.Print(new XivChatEntry
            {
                Message = "Broadcast: syntax = /broadcast FW=TOON1,TOON2,ETC or ALL or ALLBUTME or ALLBUT,TOON1,TOON2,ETC (Remove Space from ToonsFullName) C=/commandname args"
            });

            return;
        }
        var forWho = args.Substring(args.ToUpper().IndexOf("FW="), args.IndexOf(" ") - (args.ToUpper().IndexOf("FW="))).ToUpper();
        var rest = "C=" + args.Substring(args.ToUpper().IndexOf("C=") + 2);
        var ARGSc = forWho + " " + rest;

        if (ARGSc.Contains("ALLBUTME"))
            messagebus1.PublishAsync(Encoding.UTF8.GetBytes(ARGSc.Replace("ALLBUTME", "ALLBUT" + DalamudAPI.ClientState.LocalPlayer.Name.ToString().ToUpper().Replace(" ", ""))));
        else
            messagebus1.PublishAsync(Encoding.UTF8.GetBytes(ARGSc));
    }
    
    private void DrawUI()
    {
        //Draw Window
        this.WindowSystem.Draw();
    }

    public unsafe void Test()
    {
        //Just a Test function
        try
        {
            var player = DalamudAPI.ClientState.LocalPlayer;
            _runner.NaviPts.Enqueue(player.Position + new System.Numerics.Vector3(10, 0,
            0)) ;
            //Dalamud.Logging.PluginLog.Log(DalamudAPI.PartyList[0].Name.ToString());
            //DalamudAPI.TargetManager.SetTarget(gameObject);
            //Set the Games MovementMove 0=Standard, 1=Legacy
            //DalamudAPI.GameConfig.UiControl.Set("MoveMode", 1);
            /*if (DalamudAPI.GameConfig.UiControl.GetUInt("MoveMode") == 0)
                textFollow1 = "Standard";
            else if (DalamudAPI.GameConfig.UiControl.GetUInt("MoveMode") == 1)
                textFollow1 = "Legacy";*/
        }
        catch (Exception e)
        {
            textFollow1 = e.ToString();
            //throw;
        }
    }
}