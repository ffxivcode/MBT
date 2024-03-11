using System;
using System.Diagnostics;
using System.Net.Mail;
using System.Numerics;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using MBT.IPC;

namespace MBT;

public class MainWindow : Window, IDisposable
{
    private readonly MBT Plugin;
    
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
                /*if (!IPCManager.Vnavmesh_IsEnabled)
                {
                    if (Plugin.UseNavmesh)
                        Plugin.UseNavmesh = false;
                }
                else if (!IPCManager.Vnavmesh_Nav_IsReady && IPCManager.Vnavmesh_Nav_BuildProgress > -1)
                {
                    if (Plugin.UseNavmesh)
                        Plugin.UseNavmesh = false;
                    ImGui.TextColored(new Vector4(0, 255, 0, 1), "Navmesh Loading:");
                    ImGui.ProgressBar(IPCManager.Vnavmesh_Nav_BuildProgress, new(200, 0));
                }*/

                ImGui.Text("Follow:");
                ImGui.SameLine(0, 5);
                ImGui.TextColored(Plugin.TextFollow1Color, Plugin.TextFollow1);
                ImGui.SameLine(0, 5);
                ImGui.TextColored(Plugin.TextFollow2Color, Plugin.TextFollow2);
                ImGui.SameLine(0, 5);
                ImGui.TextColored(Plugin.TextFollow3Color, Plugin.TextFollow3);
                ImGui.Checkbox("Follow Enabed", ref Plugin.Follow);
                ImGui.SameLine(0, 5);
                /*using (var d = ImRaii.Disabled(!IPCManager.Vnavmesh_Nav_IsReady || !IPCManager.Vnavmesh_IsEnabled))
                    ImGui.Checkbox("Use Navmesh", ref Plugin.UseNavmesh);
                if (!IPCManager.Vnavmesh_IsEnabled)
                {
                    ImGui.SameLine(0, 2);
                    ImGui.TextColored(new Vector4(255f, 0, 0, 1f), "(Requires vnavmesh plugin)");
                }*/
                ImGui.InputInt("Follow Distance", ref Plugin.FollowDistance);
                ImGui.InputTextWithHint("##FollowTarget", "Follow Target", ref Plugin.FollowTarget, 20);
                ImGui.SameLine(0, 5);
                if (ImGui.Button("Add Current Target"))
                    Plugin.SetTarget();

                if (ImGui.Button("Test"))
                {
                    Plugin.Test();
                }
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Movement Hacks"))
            {
                ImGui.Text("Teleport (Use at Own Risk):");
                if (ImGui.Button("+X"))
                {
                    Plugin.TeleportX(1);
                }
                ImGui.SameLine(0, 5);
                if (ImGui.Button("-X"))
                {
                    Plugin.TeleportX(-1);
                }
                if (ImGui.Button("+Y"))
                {
                    Plugin.TeleportY(1);
                }
                ImGui.SameLine(0, 5);
                if (ImGui.Button("-Y"))
                {
                    Plugin.TeleportY(-1);
                }
                if (ImGui.Button("+Z"))
                {
                    Plugin.TeleportZ(1);
                }
                ImGui.SameLine(0, 5);
                if (ImGui.Button("-Z"))
                {
                    Plugin.TeleportZ(-1);
                }
                if (ImGui.Button("Target"))
                {
                    Plugin.TTarget();
                }
                ImGui.InputTextWithHint("##Teleport", "Teleport", ref Plugin.TeleportPosition, 20);
                ImGui.SameLine(0, 5);
                if (ImGui.Button("Add Teleport POS"))
                {
                    Plugin.AddPos();
                }
                if (ImGui.Button("Teleport to POS"))
                {
                    Plugin.TeleportPOS(new Vector3(float.Parse(Plugin.TeleportPosition.Split(',')[0]), float.Parse(Plugin.TeleportPosition.Split(',')[1]), float.Parse(Plugin.TeleportPosition.Split(',')[2])));
                }
                if (ImGui.Button("Teleport to Mouse"))
                {
                    SetPos.SetPosToMouse();
                }
                ImGui.Spacing();
                ImGui.Text("Speed:");
                ImGui.InputText("Speed Base", ref Plugin.SpeedBase, 20);
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
