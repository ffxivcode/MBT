using ImGuiNET;
using System;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Common.Math;

namespace MBT
{
    unsafe class SetPos
    {
        #region SetPos

        private static IntPtr SetPosFunPtr
        {
            get
            {
                //#if CNVersion
                if (DalamudAPI.SigScanner.TryScanText("E8 ?? ?? ?? ?? 44 89 A3 ?? ?? ?? ?? 66 C7 83", out var ptr))
                    return ptr;
                // #else
                // if (Service.SigScanner.TryScanText("E8 ?? ?? ?? ?? 44 89 A3 ?? ?? ?? ?? 66 C7 83", out var ptr))
                //     return ptr;
                // #endif
                return IntPtr.Zero;
            }
        }

        public static void SetPosv3(float x, float y, float z)
        {
            if (SetPosFunPtr == IntPtr.Zero)
                return;
            if (DalamudAPI.ClientState.LocalPlayer == null)
                return;
            ((delegate*<long, float, float, float, long>)SetPosFunPtr)(
                (long)DalamudAPI.ClientState.LocalPlayer.Address, x, z, y);
        }

        public static void SetPosv2(float x, float y)
        {
            if (DalamudAPI.ClientState.LocalPlayer == null)
                return;
            float z = DalamudAPI.ClientState.LocalPlayer.Position.Y;
            SetPosv3(x, z, y);
        }

        public static void SetPosPos(Vector3 pos)
        {
            SetPosv3(pos.X, pos.Z, pos.Y);
        }

        public static void SetPosPos(Vector2 pos)
        {
            SetPosv2(pos.X, pos.Y);
        }

        //public static bool CanSetPosMore16M
        //{
        //    get
        //    {
        //        var TerritoryType = Dalamud.ClientState.TerritoryType;
        //        var a = Dalamud.DataManager.GetExcelSheet<TerritoryType>().GetRow(TerritoryType).Name.ToString();
        //    }
        //}
        public static void SetPosToMouse()
        {
            if (DalamudAPI.ClientState.LocalPlayer == null)
                return;
            var mousePos = ImGui.GetIO().MousePos;
            DalamudAPI.GameGui.ScreenToWorld(mousePos, out var pos);
            SetPosPos(pos);
        }

        public static void MoveSpeed(float speedBase)
        {
            DalamudAPI.SigScanner.TryScanText("f3 ?? ?? ?? ?? ?? ?? ?? e8 ?? ?? ?? ?? 48 ?? ?? ?? ?? ?? ?? 0f ?? ?? e8 ?? ?? ?? ?? f3 ?? ?? ?? ?? ?? ?? ?? f3 ?? ?? ?? ?? ?? ?? ?? f3 ?? ?? ?? f3", out var address);
            address = address + 4 + Marshal.ReadInt32(address + 4) + 4;
            SafeMemory.Write<float>(address + 0x14, speedBase);
            SetMoveControlData(speedBase);
        }
        private static void SetMoveControlData(float speed)
        {
            SafeMemory.Write<float>(((delegate* unmanaged[Stdcall]<byte, IntPtr>)DalamudAPI.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 ?? ?? 74 ?? 83 ?? ?? 75 ?? 0F ?? ?? ?? 66"))(1) + 8, speed);
        }

        public static string MousePos()
        {
            Vector2 mousePos = ImGui.GetIO().MousePos;
            DalamudAPI.GameGui.ScreenToWorld(mousePos, out var pos);
            Vector3 pos2 = pos;
            return pos2.ToString();
        }

        #endregion

    }
}
