using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace MBT;

public class MainWindow : Window, IDisposable
{
    private readonly MBT Plugin;

    public MainWindow(MBT plugin) : base(
        "Multi Boxer Toolkit: /mbt", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize )
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(620, 490),
            MaximumSize = new Vector2(620, 490)
        };

        Plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.Checkbox("Follow Enabed", ref Plugin.follow);
        ImGui.Checkbox("Target Enabed", ref Plugin.targetFollowTargetsTargets);
        ImGui.InputInt("Follow Distance", ref Plugin.followDistance);
        ImGui.InputText("Follow Target", ref Plugin.followTarget, 50);
        if (ImGui.Button("Set Current Target"))
        {
            Plugin.SetTarget();
        }
        /*
        if (ImGui.Button("TEST"))
        {
            Plugin.Test();
        }
        //*/
        ImGui.SameLine(0, 5);
        ImGui.Spacing();

        ImGui.TextColored(Plugin.textFollow1Color, Plugin.textFollow1);
        ImGui.SameLine(0, 5);
        ImGui.TextColored(Plugin.textFollow2Color, Plugin.textFollow2);
        ImGui.SameLine(0, 5);
        ImGui.TextColored(Plugin.textFollow3Color, Plugin.textFollow3);
    }
}
