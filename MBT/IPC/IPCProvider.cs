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

        [EzIPC] public void SetFollowStatus(bool on) => MBT.Plugin.SetFollow(on);
        [EzIPC] public void SetFollowTarget(string name) => MBT.Plugin.FollowTarget = name;
        [EzIPC] public void SetFollowDistance(int distance) => MBT.Plugin.FollowDistance = distance;
        [EzIPC] public bool GetFollowStatus() => MBT.Plugin.Follow;
        [EzIPC] public int GetFollowDistance() => MBT.Plugin.FollowDistance;
        [EzIPC] public string GetFollowTarget() => MBT.Plugin.FollowTargetObject?.Name.TextValue ?? null;
    }
}
