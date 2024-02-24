using System;
using System.Diagnostics;
using System.Numerics;
using AutoDuty.Managers;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace MBT;

public class MainWindow : Window, IDisposable
{
    private readonly MBT Plugin;
    private int currentTab = 1;
    
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

    public override void Draw()
    {
        if (ImGui.BeginTabBar("MainTabBar", ImGuiTabBarFlags.None))
        {
            if (ImGui.BeginTabItem("Follow"))
            {
                if (!IPCManager.VNavmesh_IsEnabled)
                    ImGui.TextColored(new Vector4(0, 255, 0, 1), "This feature requires VNavmesh to be installed and Enabled");
                if (IPCManager.VNavmesh_NavmeshIsNull && IPCManager.VNavmesh_TaskProgress > -1)
                {
                    ImGui.TextColored(new Vector4(0, 255, 0, 1), "Navmesh Loading:");
                    ImGui.ProgressBar(IPCManager.VNavmesh_TaskProgress, new(200, 0));
                }
                using (var d = ImRaii.Disabled(IPCManager.VNavmesh_NavmeshIsNull || !IPCManager.VNavmesh_IsEnabled))
                {
                    ImGui.Text("Follow:");
                    ImGui.SameLine(0, 5);
                    ImGui.TextColored(Plugin.textFollow1Color, Plugin.textFollow1);
                    ImGui.SameLine(0, 5);
                    ImGui.TextColored(Plugin.textFollow2Color, Plugin.textFollow2);
                    ImGui.SameLine(0, 5);
                    ImGui.TextColored(Plugin.textFollow3Color, Plugin.textFollow3);
                    ImGui.Checkbox("Follow Enabed", ref Plugin.follow);
                    ImGui.InputInt("Follow Distance", ref Plugin.followDistance);
                    ImGui.InputTextWithHint("##FollowTarget", "Follow Target", ref Plugin.followTarget, 20);
                    ImGui.SameLine(0, 5);
                    if (ImGui.Button("Add Current Target"))
                        Plugin.SetTarget();
                }
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Movement Hacks"))
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
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }
}
