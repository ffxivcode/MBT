using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using static MBT.DalamudAPI;

namespace MBT;

public class MainWindow : Window, IDisposable
{
    private readonly MBT Plugin;
    private int currentTab = 1;
    private string dropdownSelected = "";
    private bool open2ndPop = false;
    string input = "";
    string inputTextName = "";
    int inputIW = 200;
    bool showAddActionUI = false;
    bool ddisboss = false;
    List<string> items = new List<string>
        {
            "Wait|how long?",
            "WaitFor|for?",
            "Boss|move to leash location and hit",
            "Interactable|interact with?",
            "SelectYesno|yes or no?",
            "MoveToObject|Object Name?"
        };
    public MainWindow(MBT plugin) : base(
        "Multi Boxer Toolkit: /mbt", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(406, 285),
            MaximumSize = new Vector2(406, 285)
        };
        
        Plugin = plugin;
    }

    public void Dispose() { }

    private void AddAction(string action)
    {
        if (action.Contains("Boss"))
        {
            Plugin.ListBoxPOSText.Add("Boss|" + ClientState.LocalPlayer.Position.ToString().Replace('<', ' ').Replace('>', ' ').Trim());
        }
        else
            Plugin.ListBoxPOSText.Add(action + "|" + input);
        input = "";
    }

    public override void Draw()
    {
        if (ImGui.Button("Main")) currentTab = 1;
        ImGui.SameLine(0, 5);
        if (ImGui.Button("Build Path")) currentTab = 2;
        ImGui.SameLine(0, 5);
        if (ImGui.Button("Movement Hacks")) currentTab = 3;
        ImGui.Separator();
        if (currentTab == 3)
        {
            ImGui.Text("Teleport (Use at Own Risk):");
            if (ImGui.Button("+X"))
            {
                Plugin.teleportX(1);
            }
            ImGui.SameLine(0, 5);
            if (ImGui.Button("-X"))
            {
                Plugin.teleportX(-1);
            }
            if (ImGui.Button("+Y"))
            {
                Plugin.teleportY(1);
            }
            ImGui.SameLine(0, 5);
            if (ImGui.Button("-Y"))
            {
                Plugin.teleportY(-1);
            }
            if (ImGui.Button("+Z"))
            {
                Plugin.teleportZ(1);
            }
            ImGui.SameLine(0, 5);
            if (ImGui.Button("-Z"))
            {
                Plugin.teleportZ(-1);
            }
            if (ImGui.Button("Target"))
            {
                Plugin.TTarget();
            }
            ImGui.InputTextWithHint("##Teleport", "Teleport", ref Plugin.teleportPOS, 20);
            ImGui.SameLine(0, 5);
            if (ImGui.Button("Add Teleport POS"))
            {
                Plugin.AddPos();
            }
            if (ImGui.Button("Teleport to POS"))
            {
                Plugin.TeleportPOS(new Vector3(float.Parse(Plugin.teleportPOS.Split(',')[0]), float.Parse(Plugin.teleportPOS.Split(',')[1]), float.Parse(Plugin.teleportPOS.Split(',')[2])));
            }
            if (ImGui.Button("Teleport to Mouse"))
            {
                SetPos.SetPosToMouse();
            }
            ImGui.Spacing();
            ImGui.Text("Speed:");
            ImGui.InputText("Speed Base", ref Plugin.speedBase, 20);
            if (ImGui.Button("Set Speed"))
            {
                Plugin.SetSpeed();
            }
        }
        else if (currentTab == 2)
        {
            ImGui.Text("Build Path:");
            if (ImGui.Button("Add POS"))
            {
                Plugin.ListBoxPOSText.Add(ClientState.LocalPlayer.Position.ToString().Replace('<', ' ').Replace('>', ' ').Trim());
            }
            ImGui.SameLine(0, 5);
            if (ImGui.Button("Add Action"))
                ImGui.OpenPopup("AddActionPopup");

            if (ImGui.BeginPopup("AddActionPopup"))
            {
                foreach (var item in items)
                {
                    if (ImGui.Selectable(item.Split('|')[0]))
                    {
                        dropdownSelected = item;
                        showAddActionUI = true;
                        if (item.Split('|')[0].Equals("Boss"))
                        {
                            ddisboss = true;
                            input = ClientState.LocalPlayer.Position.ToString().Replace('<', ' ').Replace('>', ' ').Trim();
                            inputIW = 400;
                        }
                        else
                        {
                            ddisboss = false;
                            inputIW = 400;
                            input = "";
                        }
                        inputTextName = item.Split('|')[1];
                    }
                }
                ImGui.EndPopup();
            }
            ImGui.SameLine(0, 5);
            if (ImGui.Button("Clear Path"))
            {
                Plugin.ListBoxPOSText.Clear();
            }
            ImGui.SameLine(0, 5);
            if (ImGui.Button("Save Path"))
            {
                try
                {
                    if (File.Exists(ClientState.TerritoryType.ToString() + ".json"))
                    {
                        File.Delete(ClientState.TerritoryType.ToString() + ".json");
                    }
                    string json = JsonSerializer.Serialize(Plugin.ListBoxPOSText);
                    File.WriteAllText(ClientState.TerritoryType.ToString() + ".json", json);
                }
                catch (Exception e)
                {
                    PluginLog.Error(e.ToString());
                    //throw;
                }
            }
            ImGui.SameLine(0, 5);
            if (ImGui.Button("Load Path"))
            {
                try
                {
                    if (File.Exists(ClientState.TerritoryType.ToString() + ".json"))
                    {
                        Plugin.ListBoxPOSText.Clear();
                        string json = File.ReadAllText(ClientState.TerritoryType.ToString() + ".json");
                        Plugin.ListBoxPOSText = JsonSerializer.Deserialize<List<string>>(json);
                    }
                }
                catch (Exception e)
                {
                    PluginLog.Error(e.ToString());
                    //throw;
                }
            }
            if (showAddActionUI)
            {
                ImGui.PushItemWidth(inputIW);
                if (ddisboss)
                    input = ClientState.LocalPlayer.Position.ToString().Replace('<', ' ').Replace('>', ' ').Trim();
                ImGui.InputText(inputTextName, ref input, 50);
                ImGui.SameLine(0, 5);
                if (ImGui.Button("Add"))
                {
                    AddAction(dropdownSelected.Split('|')[0]);
                    showAddActionUI = false;
                }
            }
            if (!ImGui.BeginListBox("##List", new Vector2(-1, -1))) return;
            foreach (var item in Plugin.ListBoxPOSText)
            {
                ImGui.Selectable(item, ImGui.IsItemClicked(ImGuiMouseButton.Right) || ImGui.IsItemClicked(ImGuiMouseButton.Left));

                if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    // Do stuff on Selectable() double click.
                    if (item.Split('|')[0].Equals("Wait") || item.Split('|')[0].Equals("Interactable") || item.Split('|')[0].Equals("Boss") || item.Split('|')[0].Equals("SelectYesno") || item.Split('|')[0].Equals("MoveToObject") || item.Split('|')[0].Equals("WaitFor"))
                    {
                        //do nothing
                    }
                    else
                        Plugin.TeleportPOS(new Vector3(float.Parse(item.Split(',')[0]), float.Parse(item.Split(',')[1]), float.Parse(item.Split(',')[2])));
                }
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                    Plugin.ListBoxPOSText.Remove(item);
                else if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                {
                    //Add a listbox that when this is selected it puts this item in the list box and allows direct modification of items
                }
            }
            ImGui.EndListBox();
        }
        else if (currentTab == 1)
        {
            ImGui.Text("Main:");
            ImGui.Spacing();
            if (ImGui.Button("Navigate Path"))
            {
                Plugin.NavigatePath();
            }
            ImGui.SameLine(0, 5);
            if (ImGui.Button("Stop Navigating"))
            {
                Plugin.StopNavigating();
            }
            if (ImGui.Button("Test"))
            {
                Plugin.Test();
            }
            //
        }
    }
}
