using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using SamplePlugin;

namespace LalafellSupremacy.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(LalafellSupremacy plugin) : base("Lalafell Supremacy Config Window")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(350, 100);
        SizeCondition = ImGuiCond.Always;

        Configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // can't ref a property, so use a local copy
        var configEnabled = Configuration.Enabled;
        if (ImGui.Checkbox("Enable Plugin", ref configEnabled))
        {
            Configuration.Enabled = configEnabled;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            Configuration.Save();
        }
        ImGui.Text("Haven't figured out how to safely force reload all players.\nTeleport to the same area in the mean time.");
    }
}
