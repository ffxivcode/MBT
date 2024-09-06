using ECommons.EzIpcManager;
#nullable disable

namespace MBT.IPC
{
    internal class IPCProvider
    {
        internal IPCProvider()
        {
            EzIPC.Init(this);
        }

        [EzIPC] public void SetFollowStatus(bool on) => MBT.Plugin.Follow = true;
        [EzIPC] public void SetFollowTarget(string name) => MBT.Plugin.FollowTarget.Item2 = name;
        [EzIPC] public void SetFollowDistance(int distance) => MBT.Plugin.FollowDistance = distance;
        [EzIPC] public bool GetFollowStatus() => MBT.Plugin.Follow;
        [EzIPC] public float GetFollowDistance() => MBT.Plugin.FollowDistance;
        [EzIPC] public string GetFollowTarget() => MBT.Plugin.FollowTargetObject?.Name.TextValue ?? string.Empty;
    }
}
