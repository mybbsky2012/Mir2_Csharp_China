using System.Text;
using Shared;
using S = ServerPackets;

namespace HealthPacketProbe;

/// <summary>
/// 只读验证 S.ObjectHealth 新增 CurrentHP/MaxHP 后的序列化：
/// 1) 新包打包/解包往返字段一致、长度 16 字节；
/// 2) 旧格式包（没有血量字段）用新代码解会抛 InvalidDataException —— 所以服务端和客户端必须一起更新。
/// 不连服务器、不写任何文件。
/// </summary>
internal static class Program
{
    private static int Main()
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        Packet.IsServer = false; // 按客户端方向解析（GetServerPacket）

        int fail = 0;
        void Check(string name, bool ok, string detail = "")
        {
            Console.WriteLine((ok ? "[OK]   " : "[FAIL] ") + name + (string.IsNullOrEmpty(detail) ? "" : "  " + detail));
            if (!ok) fail++;
        }

        // 1) 往返
        var sent = new S.ObjectHealth
        {
            ObjectID = 0x00ABCDEF,
            Percent = 42,
            Expire = 7,
            CurrentHP = 1234,
            MaxHP = 5000
        };

        byte[] bytes = sent.GetPacketBytes().ToArray();
        Check("包长度 = 2(长度) + 2(ID) + 4+1+1+4+4 = 18", bytes.Length == 18, "实际 " + bytes.Length);

        Packet parsed = Packet.ReceivePacket(bytes, out byte[] extra);
        var got = parsed as S.ObjectHealth;
        Check("解析出 S.ObjectHealth", got != null);
        if (got != null)
        {
            Check("ObjectID 一致", got.ObjectID == sent.ObjectID, got.ObjectID.ToString());
            Check("Percent 一致", got.Percent == sent.Percent, got.Percent.ToString());
            Check("Expire 一致", got.Expire == sent.Expire, got.Expire.ToString());
            Check("CurrentHP 一致", got.CurrentHP == sent.CurrentHP, got.CurrentHP.ToString());
            Check("MaxHP 一致", got.MaxHP == sent.MaxHP, got.MaxHP.ToString());
            Check("没有多余字节", extra.Length == 0, "extra=" + extra.Length);
        }

        // 2) 旧格式（只有 ObjectID/Percent/Expire）用新代码解 → 必须报错，用来提醒必须同步更新
        byte[] oldPayload = new byte[10];
        oldPayload[0] = 10; // 长度
        oldPayload[1] = 0;
        BitConverter.GetBytes((short)ServerPacketIds.ObjectHealth).CopyTo(oldPayload, 2);
        BitConverter.GetBytes(0x00ABCDEFu).CopyTo(oldPayload, 4);
        oldPayload[8] = 42;
        oldPayload[9] = 7;

        bool threw = false;
        try { Packet.ReceivePacket(oldPayload, out _); }
        catch (InvalidDataException) { threw = true; }
        catch { threw = true; }
        Check("旧格式包在新客户端上会抛异常（服务端必须同步更新）", threw);

        Console.WriteLine(fail == 0 ? "全部通过" : $"有 {fail} 项失败");
        return fail == 0 ? 0 : 1;
    }
}
