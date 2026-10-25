using System;
using System.IO;
using System.Reflection;

// 冒烟测试：GameShopInfo Reset 包（Item=null）必须能完整序列化（原 NRE 崩溃点）
var packet = new ServerPackets.GameShopInfo { Reset = true };
var method = typeof(Packet).GetMethod("GetPacketBytes", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
var bytes = (byte[])method.Invoke(packet, null);
Console.WriteLine($"Reset 包序列化成功，{bytes.Length} 字节 —— 无 NRE");

// 按客户端 ReadPacket 的读法回读：GameShopItem（含 Info）+ StockLevel + Reset
var reader = new BinaryReader(new MemoryStream(bytes));
reader.ReadInt16(); // 长度
reader.ReadInt16(); // Index
var item = new GameShopItem(reader, true);
int stock = reader.ReadInt32();
bool reset = reader.ReadBoolean();
Console.WriteLine($"回读 OK：ItemIndex={item.ItemIndex} Name='{item.Info.Name}' StockLevel={stock} Reset={reset}，剩余 {reader.BaseStream.Length - reader.BaseStream.Position} 字节");
