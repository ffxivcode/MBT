using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace MBT;

public class MainWindow : Window, IDisposable
{
    private readonly MBT Plugin;
    private int currentTab = 1;
    public MainWindow(MBT plugin) : base(
        "Multi Boxer Toolkit: /mbt", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize )
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(425, 390),
            MaximumSize = new Vector2(425, 390)
        };

        Plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (ImGui.Button("Main")) currentTab = 1;
        ImGui.SameLine(0, 5);
        if (ImGui.Button("Invite")) currentTab = 2;
        ImGui.SameLine(0, 5);
        if (ImGui.Button("Follow")) currentTab = 3;
        ImGui.Separator();
        if (currentTab == 3)
        {
            ImGui.Text("Follow:");
            ImGui.SameLine(0, 5);
            ImGui.TextColored(Plugin.textFollow1Color, Plugin.textFollow1);
            ImGui.SameLine(0, 5);
            ImGui.TextColored(Plugin.textFollow2Color, Plugin.textFollow2);
            ImGui.SameLine(0, 5);
            ImGui.TextColored(Plugin.textFollow3Color, Plugin.textFollow3);
            ImGui.Checkbox("Follow Enabed", ref Plugin.follow);
            ImGui.Checkbox("Target Enabed", ref Plugin.targetFollowTargetsTargets);
            ImGui.InputInt("Follow Distance", ref Plugin.followDistance);
            ImGui.InputTextWithHint("","Follow Target", ref Plugin.followTarget, 20);
            ImGui.SameLine(0, 5);
            if (ImGui.Button("Add Current Target"))
            {
                Plugin.SetTarget();
            }
        }
        else if (currentTab == 2)
        {
            ImGui.Text("Invite:");
            ImGui.Checkbox("Auto Accept Invites Enabed", ref Plugin.inviteAccept);
            ImGui.Checkbox("Only Accept Invites From:", ref Plugin.inviteAcceptSelective);
            ImGui.InputText("", ref Plugin.inputSelectiveCharacter, 20);
            ImGui.SameLine(0, 5);
            if (ImGui.Button("Add Character"))
            {
                Plugin.AddSelectiveInvite();
            }
            if (!ImGui.BeginListBox("##List", new Vector2(-1, -1))) return;
            foreach (var item in Plugin.ListBoxText)
            {
                ImGui.Selectable(item, ImGui.IsItemClicked());

                if (!ImGui.IsItemClicked()) continue;
                //Plugin.textStatus = splitItem[0];
                //Plugin.textStatusColor = colorVector4;
               // Plugin.ListBoxClick(item);
            }
            ImGui.EndListBox();
        }
        else if (currentTab == 1)
        {
            ImGui.Text("Main:");
            //*
            ImGui.Spacing();
            ImGui.Text("Teleport (Use at Own Risk):");
            if (ImGui.Button("Up"))
            {
                Plugin.Up();
            }
            if (ImGui.Button("Left"))
            {
                Plugin.Left();
            }
            ImGui.SameLine(0, 5);
            if (ImGui.Button("Right"))
            {
                Plugin.Right();
            }
            if (ImGui.Button("Down"))
            {
                Plugin.Down();
            }
            if (ImGui.Button("Target"))
            {
                Plugin.TTarget();
            }
            ImGui.InputTextWithHint("","Teleport", ref Plugin.teleportPOS, 20);
            ImGui.SameLine(0, 5);
            if (ImGui.Button("Add Teleport POS"))
            {
                Plugin.AddPos();
            }
            if (ImGui.Button("Teleport to POS"))
            {
                Plugin.TeleportPOS();
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
            ImGui.Spacing();
            ImGui.Text("Navigation:");
            ImGui.InputTextWithHint(" ", "Start Position", ref Plugin.inputStart, 20);
            ImGui.SameLine(0, 5);
            if (ImGui.Button("Add Start POS"))
            {
                Plugin.AddStart();
            }
            ImGui.InputTextWithHint("  ", "End Position", ref Plugin.inputEnd, 20);
            ImGui.SameLine(0, 5);
            if (ImGui.Button("Add End POS"))
            {
                Plugin.AddEnd();
            }
            ImGui.Spacing();
            if (ImGui.Button("Navigate"))
            {
                Plugin.Navigate();
            }
            /*if (ImGui.Button("Test"))
            {
                Plugin.Test();
            }
            ImGui.SameLine(0, 5);
            ImGui.Text(Plugin.textTest);
            //*/
        }
    }
}
