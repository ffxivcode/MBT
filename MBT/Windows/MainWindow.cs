using System;
using System.Numerics;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using MBT.IPC;

namespace MBT.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly MBT Plugin;
    bool _showPopup;
    string _popupText;
    public MainWindow(MBT plugin) : base(
        "Multi Boxer Toolkit: /mbt###MBT", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize)
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
        if (!VNavmesh_IPCSubscriber.IsEnabled)
        {
            if (Plugin.Configuration.UseNavmesh)
            {
                Plugin.Configuration.UseNavmesh = false;
                Plugin.Configuration.Save();
            }
        }
        else if (!VNavmesh_IPCSubscriber.Nav_IsReady() && VNavmesh_IPCSubscriber.Nav_BuildProgress() > -1)
        {
            if (Plugin.Configuration.UseNavmesh)
            {
                Plugin.Configuration.UseNavmesh = false;
                Plugin.Configuration.Save();
            }
            ImGui.TextColored(new Vector4(0, 255, 0, 1), "Navmesh Loading:");
            ImGui.ProgressBar(VNavmesh_IPCSubscriber.Nav_BuildProgress(), new(200, 0));
        }
        ImGui.Text("Follow:");
        ImGui.SameLine(0, 5);
        ImGui.TextColored(Plugin.TextFollow1Color, Plugin.TextFollow1);
        ImGui.SameLine(0, 5);
        ImGui.TextColored(Plugin.TextFollow2Color, Plugin.TextFollow2);
        ImGui.SameLine(0, 5);
        ImGui.TextColored(Plugin.TextFollow3Color, Plugin.TextFollow3);
        ImGui.Checkbox("Follow Enabed", ref Plugin.Follow);
        ImGui.SameLine(0, 5);
        using (ImRaii.Disabled(!VNavmesh_IPCSubscriber.Nav_IsReady() || !VNavmesh_IPCSubscriber.IsEnabled))
        {
            if (ImGui.Checkbox("Use Navmesh", ref Plugin.Configuration.UseNavmesh))
                Plugin.Configuration.Save();
        }
        if (!VNavmesh_IPCSubscriber.IsEnabled)
        {
            ImGui.SameLine(0, 2);
            ImGui.TextColored(new Vector4(255f, 0, 0, 1f), "(Requires vnavmesh plugin)");
        }
        ImGui.InputInt("Follow Distance", ref Plugin.FollowDistance);
        ImGui.InputTextWithHint("##FollowTarget", "Follow Target", ref Plugin.FollowTarget, 20);
        ImGui.SameLine(0, 5);
        if (ImGui.Button("Add Current Target"))
            Plugin.SetTargetB = true;
    }
}
