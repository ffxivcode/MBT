using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace MBT.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly MBT Plugin;
    bool _showPopup;
    string _popupText;
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
    public static void CenteredText(string text)
    {
        float windowWidth = ImGui.GetWindowSize().X;
        float textWidth = ImGui.CalcTextSize(text).X;

        ImGui.SetCursorPosX((windowWidth - textWidth) * 0.5f);
        ImGui.Text(text);
    }
    public static bool CenteredButton(string label, float percentWidth, float xIndent = 0)
    {
        var buttonWidth = ImGui.GetContentRegionAvail().X * percentWidth;
        ImGui.SetCursorPosX(xIndent + (ImGui.GetContentRegionAvail().X - buttonWidth) / 2f);
        return ImGui.Button(label, new Vector2(buttonWidth, 35f));
    }
    private void ShowPopup(string popupText)
    {
        _popupText = popupText.Trim();
        _showPopup = true;
    }
    private void DrawPopup()
    {
        if (_showPopup)
        {
            ImGui.OpenPopup("Notification");
        }

        if (ImGui.BeginPopupModal("Notification", ref _showPopup, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar))
        {
            CenteredText(_popupText);
            ImGui.Spacing();
            if (CenteredButton("OK", .5f))
            {
                _showPopup = false;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }
    public override void Draw()
    {
        DrawPopup();
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
        /*ImGui.SameLine(0, 5);
        using (var d = ImRaii.Disabled(!IPCManager.Vnavmesh_Nav_IsReady || !IPCManager.Vnavmesh_IsEnabled))
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
            Plugin.SetTargetB = true;
    }
}
