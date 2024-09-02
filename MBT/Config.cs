using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace MBT;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; }
    public bool UseNavmesh = false;

    public void Save() => MBT.PluginInterface!.SavePluginConfig(this);
}