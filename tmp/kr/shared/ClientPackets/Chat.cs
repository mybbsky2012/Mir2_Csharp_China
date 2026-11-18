using System.Collections.Generic;
using System.IO;

namespace ClientPackets;

public sealed class Chat : Packet
{
	public string Message = string.Empty;

	public List<ChatItem> LinkedItems = new List<ChatItem>();

	public override short Index => 13;

	protected override void ReadPacket(BinaryReader reader)
	{
		Message = reader.ReadString();
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			LinkedItems.Add(new ChatItem(reader));
		}
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Message);
		writer.Write(LinkedItems.Count);
		for (int i = 0; i < LinkedItems.Count; i++)
		{
			LinkedItems[i].Save(writer);
		}
	}
}
