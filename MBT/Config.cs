using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace MBT;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 13;

    // the below exist just to make saving less cumbersome
    [NonSerialized]
    private DalamudPluginInterface? PluginInterface;

    public void Initialize(DalamudPluginInterface pluginInterface)
    {
        this.PluginInterface = pluginInterface;
    }

    public void Save()
    {
        this.PluginInterface!.SavePluginConfig(this);
    }
}