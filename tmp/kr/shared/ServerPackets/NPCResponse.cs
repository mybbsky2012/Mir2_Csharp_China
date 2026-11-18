using System.Collections.Generic;
using System.IO;

namespace ServerPackets;

public sealed class NPCResponse : Packet
{
	public List<string> Page;

	public override short Index => 91;

	protected override void ReadPacket(BinaryReader reader)
	{
		Page = new List<string>();
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			Page.Add(reader.ReadString());
		}
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Page.Count);
		for (int i = 0; i < Page.Count; i++)
		{
			writer.Write(Page[i]);
		}
	}
}
