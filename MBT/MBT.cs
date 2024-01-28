﻿using Dalamud.Game.Command;
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
using static MBT.DalamudAPI;
using System.IO;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using Dalamud.Game.ClientState.Conditions;
using System.Text.Json;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using ClickLib.Clicks;
using Dalamud.Game.ClientState.Objects.Types;
namespace MBT;

/// <summary>
/// MBT v1
/// </summary>

public class MBT : IDalamudPlugin
{
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

    XIVRunner.XIVRunner _runner;
    XIVRunner.XIVRunner _runnerIC;
    public MBT(
        DalamudPluginInterface pluginInterface)
    {
        try
        {
            Plugin = this;
            Initialize(pluginInterface);
            _runner = XIVRunner.XIVRunner.Create(pluginInterface);
            _runner.Enable = true;
            _runner.Precision = .1f;
            _runner.UseMount = false;
            _runner.TryJump = false;
            _runnerIC = XIVRunner.XIVRunner.Create(pluginInterface);
            _runnerIC.Enable = true;
            _runnerIC.Precision = .1f;
            _runnerIC.UseMount = false;
            _runnerIC.TryJump = false;
            //Create MainWindow UI
            MainWindow = new MainWindow(this);
            WindowSystem.AddWindow(MainWindow);

            //Add Commands  ---- More to come
            CommandManager.AddHandler("/bc", new CommandInfo(OnCommandBC)
            {
                HelpMessage = "/bc fw=toon1,toon2,ETC or all or allbutme or allbut,toon1,toon2,ETC (Remove Space from ToonsFullName) C=/commandname args\"\n" +
                "example: /bc FW=ALL C=/mbt ft Toon Name"
            });
            CommandManager.AddHandler("/mbt", new CommandInfo(OnCommand)
            {
                HelpMessage = "/mbt -> opens main window\n" +
                "/mbt spread -> Spreads toons away from LocalPlayer\n" +
                "/mbt exitduty -> Immediately exits the duty\n" +
                "/mbt acceptduty -> Immediately accepts the duty finder popup\n"
            });

            //Draw UI
            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenMainUi += OpenMainUI;
            ChatCommand.Initialize();
            //Attach our OnGameFrameworkUpdate function to our game's Framework Update (called once every frame)
            //Framework.Update += OnGameFrameworkUpdate;
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

            OpenMainUI();
        }
        catch (Exception e) { PluginLog.Info($"Failed loading plugin\n{e}"); }
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
        if (PartyList == null) return;
        var playerObjectId = ClientState.LocalPlayer.ObjectId;
        List<uint> partyList = new();

        foreach (var partyMember in PartyList)
        {
            if (partyMember.ObjectId == ClientState.LocalPlayer.ObjectId) { continue; }

            if(partyMember.GameObject != null)
                partyList.Add(partyMember.GameObject.ObjectId);
        }
        
        for(int i = 0; i < partyList.Count; i++)
        {
            var j = i + 1;
            PluginLog.Info("OnCommandSpread: PartyList: " + partyList[i] + " : " + playerObjectId + "," + partyList[i] + "," + "*" + j + "*");
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
            PluginLog.Info(message.Substring(message.IndexOf("C=") + 2));
            ChatCommand.ExecuteCommand(message.Substring(message.IndexOf("C=") + 2));
        }
    }

    private void MessageReceivedSpread(string message)
    {
        PluginLog.Info("MessageRecieved: " + message);
        if (ClientState.LocalPlayer == null) return;
        var messageList = message.Split(',').ToList();
        var playerObjectId = ClientState.LocalPlayer.ObjectId;
        PluginLog.Info("messageList.Count: " + messageList.Count + " messageList[0]: " + messageList[0] + " messageList[1]: " + messageList[1] + " messageList[2]: " + messageList[2]);
        
        if (Convert.ToUInt32(messageList[1]) == playerObjectId)
        {
            var sender = ObjectTable.Where(s => s.ObjectId == Convert.ToUInt32(messageList[0]));
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
    public void Dispose()
    {
        //RemoveAllWindows and Dispose of them, Disable FrameworkUpdate and remove Commands and change back to Legacy if needed
        WindowSystem.RemoveAllWindows();
        ((IDisposable)MainWindow).Dispose();
        //Framework.Update -= OnGameFrameworkUpdate;
        //Condition.ConditionChange -= Condition_OnConditionChange;
        CommandManager.RemoveHandler("/mbt"); 
        CommandManager.RemoveHandler("/bc");
        _runner.NaviPts.Clear();
        _runnerIC.NaviPts.Clear();
        messagebus1.Dispose();
        messagebus2.Dispose();
        _runner?.Dispose();
        _runnerIC?.Dispose();
    }
    private void OpenMainUI()
    {
        MainWindow.IsOpen = true;
    }
    private void OnCommand(string command, string args)
    {
        //In response to the slash command, just display our main ui or turn Follow on or off
 
        if (args.ToUpper().Contains("SPREAD"))
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
        if (ClientState.LocalPlayer is null) { return; }
        if (args == null) { return; }

        if (!args.ToUpper().Contains("FW=") || !args.ToUpper().Contains("C="))
        {
            ChatGui?.Print(new XivChatEntry
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
            PluginLog.Error(e.ToString());
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
            PluginLog.Error(e.ToString());
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
            PluginLog.Error(e.ToString());
            //throw;
        }
    }

    
    public unsafe void TTarget()
    {
        //Teleport
        try
        {
            if (TargetManager.Target != null)
            {
                var player = ClientState.LocalPlayer;
                SetPos.SetPosPos(TargetManager.Target.Position);
            }
            
        }
        catch (Exception e)
        {
            PluginLog.Error(e.ToString());
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
            PluginLog.Error(e.ToString());
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
            PluginLog.Error(e.ToString());
            PluginLog.Error(e.ToString());
            //throw;
        }
    }
    public unsafe void AddPos()
    {
        //Add Start POS
        teleportPOS = ClientState.LocalPlayer.Position.ToString().Replace('<', ' ').Replace('>', ' ').Trim();
    }
    public unsafe void Navigate(RcVec3f _startPos, RcVec3f _endPos)
    {
        //Navigate
        try
        {
            string path = ClientState.TerritoryType.ToString() + ".navmesh";
            FileStream fileStream = File.Open(path, FileMode.Open);
            var list = new List<DtStraightPath>();
            var nmd = new NavMeshDetour();
            list = nmd.QueryPath(_startPos, _endPos, fileStream);
            PluginLog.Info(list.Count.ToString());
            _runner.Enable = false;
            if (list.Count > 2) 
            {
                foreach (var item in list)
                {
                    PluginLog.Info(item.pos.ToString());
                    var v3 = new Vector3(item.pos.X, item.pos.Y, item.pos.Z);
                    _runner.NaviPts.Enqueue(v3);
                }
            }
            else
            {
                var v3 = new Vector3(_startPos.X, _startPos.Y, _startPos.Z);
                _runner.NaviPts.Enqueue(v3);
                v3 = new Vector3(_endPos.X, _endPos.Y, _endPos.Z);
                _runner.NaviPts.Enqueue(v3);
            }
            _runner.Enable = true;
        }
        catch (Exception e)
        {
            PluginLog.Error(e.ToString());
            //throw;
        }
    }
    public void StopNavigating()
    {
        _runner.Enable = false;
        _runnerIC.Enable = false;
        _runner.NaviPts.Clear();
        _runnerIC.NaviPts.Clear();
        stopNavigating = true;
    }
    public async void NavigatePath()
    {
        try
        {
            if (!File.Exists(ClientState.TerritoryType.ToString() + ".json")) return;
            string json = File.ReadAllText(ClientState.TerritoryType.ToString() + ".json");
            var path = JsonSerializer.Deserialize<List<string>>(json);
            foreach (var item in path)
            {
                if(stopNavigating)
                {
                    stopNavigating = false;
                    return;
                }
                var targetPos = new RcVec3f();
                var stopForCombat = true;
                var targetPosSet = false;
                if (item.Split('|')[0].Equals("Wait"))
                {
                    if (item.Split('|')[1].Equals(""))
                        await Task.Delay(50);
                    else
                        await Task.Delay(int.Parse(item.Split('|')[1]));
                    continue;
                }
                else if (item.Split('|')[0].Equals("WaitFor"))
                {
                    if (item.Split('|')[1].Equals("BetweenAreas"))
                    {
                        while (Condition[ConditionFlag.BetweenAreas])
                        {
                            await Task.Delay(500);
                            PluginLog.Information("Waiting for BetweenAreas");
                            if (_runner.NaviPts.Count != 0)
                                _runner.NaviPts.Clear();
                        }
                    }
                    continue;
                }
                else if (item.Split('|')[0].Equals("Interactable"))
                {
                    await Task.Delay(2000);
                    try
                    {
                        var baseObjs = ObjectTable.Where(x => x.Name.ToString().Contains(item.Split('|')[1]));
                        if (baseObjs.Count() == 0)
                            continue;

                        var baseObj = baseObjs.First();
                        if (baseObjs.Count() > 1)
                        { 
                            //get objectr with closest distance
                            var closestDistance = 999999999999999;
                            var player = ClientState.LocalPlayer;
                            foreach (var Obj in baseObjs)
                            {
                                var distance = Convert.ToInt32(Vector3.Distance(new Vector3(player.Position.X, player.Position.Y, player.Position.Z), new Vector3(Obj.Position.X, Obj.Position.Y, Obj.Position.Z)));
                                if (distance < closestDistance)
                                {
                                    closestDistance = distance;
                                    baseObj = Obj;
                                }
                            }
                        }
                        var cnt = 0;
                        while (cnt++ < 4)
                        {
                            InteractWithObject(baseObj);
                            await Task.Delay(1000);
                        }
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Error(ex.ToString());
                    }

                    await Task.Delay(50);
                    continue;
                }
                else if (item.Split('|')[0].Equals("SelectYesno"))
                {
                    try
                    {
                        nint addon;
                        int cnt = 0;
                        while ((addon = GameGui.GetAddonByName("SelectYesno", 1)) == 0 && (cnt++ < 500))
                            await Task.Delay(10);
                        if (addon == 0)
                            continue;
                        await Task.Delay(25);
                        //PluginLog.Info("addon: "+addon.ToString());
                        if (item.Split('|')[1].Equals(""))
                            ClickSelectYesNo.Using(addon).Yes();
                        else
                        {
                            if (item.Split('|')[1].ToUpper().Equals("YES"))
                            {
                                ClickSelectYesNo.Using(addon).Yes();
                            }
                            else if (item.Split('|')[1].ToUpper().Equals("NO"))
                            {
                                ClickSelectYesNo.Using(addon).No();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Error(ex.ToString());
                    }
                    await Task.Delay(50);
                    continue;
                }
                else if (item.Split('|')[0].Equals("Boss"))
                {
                    stopForCombat = false;
                    targetPosSet = true;
                    if (item.Split('|')[1].Equals(""))
                        targetPos = new RcVec3f(ClientState.LocalPlayer.Position.X, ClientState.LocalPlayer.Position.Y, ClientState.LocalPlayer.Position.Z);
                    else
                        targetPos = new RcVec3f(float.Parse(item.Split('|')[1].Split(',')[0]), float.Parse(item.Split('|')[1].Split(',')[1]), float.Parse(item.Split('|')[1].Split(',')[2]));
                }
                else if (item.Split('|')[0].Equals("MoveToObject"))
                {
                    var objs = ObjectTable.Where(x => x.Name.ToString().Contains(item.Split('|')[1]));
                    if (objs.Count() == 0)
                        continue;
                    targetPosSet = true;
                    targetPos = new RcVec3f(objs.First().Position.X, objs.First().Position.Y, objs.First().Position.Z);
                }
                
                var playerPos = new RcVec3f(ClientState.LocalPlayer.Position.X, ClientState.LocalPlayer.Position.Y, ClientState.LocalPlayer.Position.Z);
                if (!targetPosSet)
                    targetPos = new RcVec3f(float.Parse(item.Split(',')[0]), float.Parse(item.Split(',')[1]), float.Parse(item.Split(',')[2]));
                Navigate(playerPos, targetPos);
                //wait for xivrunner queue to be empty
                while (_runner.NaviPts.Count != 0)
                {
                    //PluginLog.Information("Waiting for navigation");w
                    if (Condition[ConditionFlag.InCombat] && stopForCombat)
                    {
                        PluginLog.Information("Waiting for InCombat");
                        _runner.Enable = false;
                        _runnerIC.Enable = true;
                        var playerPos2 = ClientState.LocalPlayer.Position;
                        while (Condition[ConditionFlag.InCombat])
                        {
                            if (_runnerIC.NaviPts.Count == 0)
                                _runnerIC.NaviPts.Enqueue(playerPos2);
                            await Task.Delay(500);
                        }
                        _runnerIC.NaviPts.Clear();
                        _runnerIC.Enable = false;
                        _runner.Enable = true;
                        //PluginLog.Information("Waiting for InCombat");

                    }
                    await Task.Delay(5);
                }
            }
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex.ToString());
        }
    }
    public unsafe void InteractWithObject(GameObject baseObj)
    {
        try
        {            
            var convObj = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)baseObj.Address;
            TargetSystem.Instance()->InteractWithObject(convObj, true);
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex.ToString());
        }
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
            //Dalamud.Logging.PluginLog.Log(DalamudAPI.PartyList[0].Name.ToString());
            //DalamudAPI.TargetManager.SetTarget(gameObject);
            //Set the Games MovementMove 0=Standard, 1=Legacy
            //DalamudAPI.GameConfig.UiControl.Set("MoveMode", 1);
            /*if (DalamudAPI.GameConfig.UiControl.GetUInt("MoveMode") == 0)
                textFollow1 = "Standard";
            else if (DalamudAPI.GameConfig.UiControl.GetUInt("MoveMode") == 1)
                textFollow1 = "Legacy";
            //ClickSelectYesNo.Using(default).Yes();
            *if (TryGetAddonByName<AtkUnitBase>("SelectYesno", out var addon))
            {
                PluginLog.Information("got addon");
            }
            else
            {
                PluginLog.Information("no addon found");
            }
            */
            //var addon = GameGui.GetAddonByName("SelectYesno", 1);
            // var addon = (AtkUnitBase*)GameGui.GetAddonByName("SelectYesno", 1);
            // addonPTR->SendClick(addon, EventType.CHANGE, 0, ((AddonSelectYesno*)addon)->YesButton->AtkComponentBase.OwnerNode);
            //ClickSelectYesNo.Using(addon).Yes();
            //addon.
            // PluginLog.Information(addon->AtkValues[0].ToString());
            //textTest = addon->AtkValues[0].ToString();
            //InteractWithObject("Red Coral Formation");
            PluginLog.Info(ClientState.LocalPlayer.Position.ToString());
        }
        catch (Exception e)
        {
            PluginLog.Error(e.ToString());
            //throw;
        }
    }

    private void Condition_OnConditionChange(ConditionFlag flag, bool value)
    {
        PluginLog.Info(flag.ToString());
        if (flag == ConditionFlag.InCombat)
            _runner.Enable = false;
        else
            _runner.Enable = true;
    }

}