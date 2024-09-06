using System;
using System.Linq;
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
        var follow = Plugin.Follow;
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
        if (ImGuiEx.CheckboxWrapped("Follow Enabed", ref follow))
            Plugin.Follow = follow;
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
        if (ImGui.InputFloat("Follow Distance", ref Plugin.FollowDistance, 0.25f, 1f))
            Plugin.FollowDistance = Plugin.FollowDistance > 0.25f ? Plugin.FollowDistance : 0.25f;
        if (ImGui.InputTextWithHint("##FollowTarget", "Follow Target", ref Plugin.FollowTarget.Item2, 20))
            Plugin.FollowTarget.Item1 = Svc.Objects.FirstOrDefault(x => x.Name.ExtractText().Equals(Plugin.FollowTarget.Item2, StringComparison.InvariantCultureIgnoreCase))?.GameObjectId ?? 0;
        ImGui.SameLine(0, 5);
        if (ImGui.Button("Add Current Target"))
        {
            if (Svc.Targets.Target != null)
            {
                Plugin.FollowTarget.Item1 = Svc.Targets.Target.GameObjectId;
                Plugin.FollowTarget.Item2 = Svc.Targets.Target.Name.TextValue;
            }
        }
        ImGui.PopItemWidth();
    }
}
