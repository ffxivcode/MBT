using System;
using System.Numerics;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ImGuiNET;
using MBT.IPC;

namespace MBT.Windows;

public class MainWindow(MBT Plugin) : Window(
    $"Multi Boxer Toolkit(0.0.0.{Plugin.Configuration.Version}): /mbt###MBT"), IDisposable
{
    public void Dispose() { }

    public override void Draw()
    {
        var _follow = Plugin.Follow;

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
            ImGui.TextColored(new Vector4(0f, 1f, 0f, 1f), "Navmesh Loading:");
            ImGui.ProgressBar(VNavmesh_IPCSubscriber.Nav_BuildProgress(), new(200, 0));
        }
        ImGui.PushStyleColor(ImGuiCol.Text, Plugin.Follow ? new Vector4(0f, 1f, 0f, 1f) : new Vector4(1f, 0f, 0f, 1f));
        ImGui.TextWrapped($"Follow: {(Plugin.Follow ? "On" : "Off")} Name: {(Plugin.followTargetObject != null ? $"{Plugin.FollowTargetObject?.Name.ExtractText()}" : $"{Plugin.FollowTarget} not found")} Distance: {Plugin.PlayerDistance:F1} <= {Plugin.FollowDistance} Moving To: {Plugin.FollowTargetPosition:F1}");
        ImGui.PopStyleColor();
        ImGui.NewLine();
        if (ImGuiEx.CheckboxWrapped("Follow Enabed", ref _follow))
            Plugin.Follow = _follow;
        using (ImRaii.Disabled(!VNavmesh_IPCSubscriber.Nav_IsReady() || !VNavmesh_IPCSubscriber.IsEnabled))
        {
            if (ImGuiEx.CheckboxWrapped("Use Navmesh", ref Plugin.Configuration.UseNavmesh))
                Plugin.Configuration.Save();
        }
        if (!VNavmesh_IPCSubscriber.IsEnabled)
        {
            ImGui.SameLine(0, 2);
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(255f, 0, 0, 1f));
            ImGui.TextWrapped("(Requires vnavmesh plugin)");
        }
        ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X - 250);
        ImGui.InputFloat("Follow Distance", ref Plugin.FollowDistance);
        ImGui.InputTextWithHint("##FollowTarget", "Follow Target", ref Plugin.FollowTarget, 20);
        ImGui.SameLine(0, 5);
        if (ImGui.Button("Add Current Target"))
            Plugin.FollowTarget = Svc.Targets.Target != null ? Svc.Targets.Target.Name.TextValue : string.Empty;
        ImGui.PopItemWidth();
    }
}
