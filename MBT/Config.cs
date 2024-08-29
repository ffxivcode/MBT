using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace MBT;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get => 15; set { } }
    public bool UseNavmesh = false;
    // the below exist just to make saving less cumbersome
    [NonSerialized]
    private IDalamudPluginInterface? PluginInterface;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.PluginInterface = pluginInterface;
    }

    public void Save()
    {
        this.PluginInterface!.SavePluginConfig(this);
    }
}