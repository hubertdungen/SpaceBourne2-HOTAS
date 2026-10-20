using System.Runtime.InteropServices;

namespace SB2Hotas;

// Maps the controller buttons the player assigns to keys the game already uses.
// No input is sent until the player arms the bridge and SpaceBourne 2 is the foreground window.
internal sealed class ButtonOutput
{
    readonly HashSet<string> held=[];
    static readonly Dictionary<string,ushort> keys=BuildKeys();
    static Dictionary<string,ushort> BuildKeys()
    {
        var k=new Dictionary<string,ushort>
        {
            ["Space"]=0x20,["Shift"]=0xA0,["Ctrl"]=0xA2,["Alt"]=0xA4,["Tab"]=9,["Escape"]=27,["Enter"]=13,["Backspace"]=8,
            ["Caps Lock"]=0x14,["<"]=0xE2,["Up"]=0x26,["Down"]=0x28,["Left"]=0x25,["Right"]=0x27
        };
        for(int i=1;i<=12;i++) k["F"+i]=(ushort)(0x6F+i);
        return k;
    }
    // Handled by the bridge itself (they flip the throttle), never sent to the game.
    public const string ReverseHold="Throttle reverse (hold)", ReverseToggle="Throttle reverse (toggle)";
    public static readonly string[] Options=new[]{"None",ReverseHold,ReverseToggle,"Left mouse","Right mouse","Middle mouse","Wheel up","Wheel down"}
        .Concat(keys.Keys).Concat(Enumerable.Range('A',26).Select(c=>((char)c).ToString()))
        .Concat(Enumerable.Range(0,10).Select(i=>i.ToString())).ToArray();
    public void Update(HashSet<string> wanted)
    {
        foreach(var key in held.Except(wanted).ToArray()) { Send(key,false); held.Remove(key); }
        foreach(var key in wanted.Except(held).ToArray()) { Send(key,true); held.Add(key); }
    }
    public void Release()=>Update([]);
    static void Send(string key,bool down)
    {
        INPUT input=new();
        uint mouse=key switch { "Left mouse"=>down?2u:4u,"Right mouse"=>down?8u:16u,"Middle mouse"=>down?32u:64u,_=>0 };
        if(key is "Wheel up" or "Wheel down")
        {
            // One wheel notch per press (the game adjusts speed with the wheel); releasing sends nothing.
            if(!down) return;
            input.type=0; input.data.mouse.flags=0x0800; input.data.mouse.data=unchecked((uint)(key=="Wheel up" ? 120 : -120));
        }
        else if(mouse!=0) { input.type=0; input.data.mouse.flags=mouse; }
        else
        {
            ushort vk=keys.TryGetValue(key,out var v)?v:key.Length==1?(ushort)key[0]:(ushort)0;
            if(vk==0) return;
            input.type=1;
            input.data.keyboard.scan=(ushort)MapVirtualKey(vk,0);
            // Arrow keys share scan codes with the numpad; the extended flag picks the arrows.
            input.data.keyboard.flags=8u|(down?0u:2u)|(vk is >=0x25 and <=0x28 ? 1u : 0u);
        }
        if(SendInput(1,[input],Marshal.SizeOf<INPUT>())!=1) throw new IOException("Windows rejected the key press. Run the game and the bridge with the same permissions (both normal or both as administrator).");
    }
    [StructLayout(LayoutKind.Sequential)] struct INPUT {public uint type; public UNION data;}
    [StructLayout(LayoutKind.Explicit)] struct UNION { [FieldOffset(0)] public MOUSE mouse; [FieldOffset(0)] public KEY keyboard; }
    [StructLayout(LayoutKind.Sequential)] struct MOUSE {public int x,y; public uint data,flags,time; public UIntPtr extra;}
    [StructLayout(LayoutKind.Sequential)] struct KEY {public ushort vk,scan;public uint flags,time;public UIntPtr extra;}
    [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint count,INPUT[] inputs,int size);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code,uint type);
}
