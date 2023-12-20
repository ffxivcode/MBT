using System;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;

namespace MBT;

// QoLBar https://github.com/UnknownX7/QoLBar/blob/master/Game.cs
public unsafe class ChatCommand
{
    public static UIModule* uiModule;

    // Command Execution
    public delegate void ProcessChatBoxDelegate(UIModule* uiModule, nint message, nint unused, byte a4);
    [Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9")]
    public static ProcessChatBoxDelegate ProcessChatBox;

    public delegate int GetCommandHandlerDelegate(RaptureShellModule* raptureShellModule, nint message, nint unused);
    [Signature("E8 ?? ?? ?? ?? 83 F8 FE 74 1E")]
    public static GetCommandHandlerDelegate GetCommandHandler;
    public static RaptureShellModule* raptureShellModule;
   
    public static void Initialize()
    {
        uiModule = Framework.Instance()->GetUiModule();
        DalamudAPI.GameInteropProvider.InitializeFromAttributes(new ChatCommand());
        // TODO change back to static whenever support is added
        //SignatureHelper.Initialise(typeof(Game));
        //SignatureHelper.Initialise(new ChatCommand());
    }

    public static void ExecuteCommand(string command)
    {
        var stringPtr = nint.Zero;

        try
        {
            stringPtr = Marshal.AllocHGlobal(UTF8String.size);
            using var str = new UTF8String(stringPtr, command);
            Marshal.StructureToPtr(str, stringPtr, false);

            ProcessChatBox(uiModule, stringPtr, nint.Zero, 0);
      
        }
        catch (Exception e) { Dalamud.Logging.PluginLog.Log("Command: " + command + " failed to execute " + e.ToString()); }

        Marshal.FreeHGlobal(stringPtr);
    }
}
[StructLayout(LayoutKind.Sequential, Size = 0x68)]
public readonly struct UTF8String : IDisposable
{
    public const int size = 0x68;

    public readonly IntPtr stringPtr;
    public readonly ulong capacity;
    public readonly ulong length;
    public readonly ulong unknown;
    public readonly byte isEmpty;
    public readonly byte notReallocated; // Taking suggestions for a better name
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x40)]
    public readonly byte[] str;

    public UTF8String(IntPtr loc, string text) : this(loc, Encoding.UTF8.GetBytes(text)) { }

    public UTF8String(IntPtr loc, byte[] text)
    {
        capacity = 0x40;
        length = (ulong)text.Length + 1;
        str = new byte[capacity];

        if (length > capacity)
        {
            stringPtr = Marshal.AllocHGlobal(text.Length + 1);
            capacity = length;
            Marshal.Copy(text, 0, stringPtr, text.Length);
            Marshal.WriteByte(stringPtr, text.Length, 0);
            notReallocated = 0;
        }
        else
        {
            stringPtr = loc + 0x22;
            text.CopyTo(str, 0);
            notReallocated = 1;
        }

        isEmpty = (byte)((length == 1) ? 1 : 0);
        unknown = 0;
    }

    public void Dispose()
    {
        if (notReallocated == 0)
            Marshal.FreeHGlobal(stringPtr);
    }
}

