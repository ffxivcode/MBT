using ECommons.EzIpcManager;
#nullable disable

namespace MBT.IPC
{
    internal class IPCProvider
    {
        private static MBT _mBT;

        internal IPCProvider(MBT mBT)
        {
            EzIPC.Init(this);
            _mBT = mBT;
        }

        [EzIPC] public void SetFollowStatus(bool on) => _mBT.SetFollow(on);
        [EzIPC] public void SetFollowTarget(string name) => _mBT.FollowTarget = name;
        [EzIPC] public void SetFollowDistance(int distance) => _mBT.FollowDistance = distance;
        [EzIPC] public bool GetFollowStatus() => _mBT.Follow;
        [EzIPC] public int GetFollowDistance() => _mBT.FollowDistance;
        [EzIPC] public string GetFollowTarget() => _mBT.FollowTargetObject?.Name.TextValue ?? null;
    }
}
