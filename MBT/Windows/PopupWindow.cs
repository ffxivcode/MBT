using ImGuiNET;
using System;
using System.Numerics;

namespace GambaTracker.Windows
{
    public class PopupWindow
    {
        private bool _isVisible = false;
        private string _popupTitle;
        private string _message;
        private Action _onYesAction;
        private Action _onNoAction;

        public PopupWindow(string popupTitle, string message, Action onYesAction, Action onNoAction)
        {
            _popupTitle = popupTitle;
            _message = message;
            _onYesAction = onYesAction;
            _onNoAction = onNoAction;
        }

        // Call this method to display the popup
        public void Show()
        {
            _isVisible = true;
        }

        public void Dispose() { }

        // This method should be called from your plugin's rendering loop
        public void Draw()
        {
            if (!_isVisible) return;

            // Ensure the window is always centered
            Vector2 center = ImGui.GetMainViewport().GetCenter();
            ImGui.SetNextWindowPos(center, ImGuiCond.Always, new Vector2(0.5f, 0.5f));

            // Window flags to simulate a popup behavior without using an actual modal popup
            ImGuiWindowFlags windowFlags = ImGuiWindowFlags.AlwaysAutoResize |
                                           ImGuiWindowFlags.NoResize |
                                           ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoCollapse;

            // Begin a regular window that looks and feels like a popup
            bool open = true;
            if (ImGui.Begin("##PopupWindow", ref open, windowFlags))
            {
                ImGui.Text(_message);


                // Calculate the width needed to center the buttons accounting for the space between them
                float buttonWidth = ImGui.CalcTextSize("Yes").X + ImGui.CalcTextSize("No").X + ImGui.GetStyle().ItemSpacing.X;
                float windowAvailWidth = ImGui.GetContentRegionAvail().X;
                float startingPosX = (windowAvailWidth - buttonWidth) * 0.5f;

                ImGui.SetCursorPosX(startingPosX); // Move cursor to the correct X position to start drawing buttons

                if (ImGui.Button("Ok"))
                {
                    _onYesAction?.Invoke();
                    _isVisible = false; // Hide the window
                }

                ImGui.SameLine();

                if (ImGui.Button("No"))
                {
                    _onNoAction?.Invoke();
                    _isVisible = false; // Hide the window
                }

                ImGui.End();
            }

            if (!open) _isVisible = false;
        }

    }
}