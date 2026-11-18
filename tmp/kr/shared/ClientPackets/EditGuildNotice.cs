using System.Collections.Generic;
using System.IO;

namespace ClientPackets;

public sealed class EditGuildNotice : Packet
{
	public List<string> notice = new List<string>();

	public override short Index => 77;

	protected override void ReadPacket(BinaryReader reader)
	{
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			notice.Add(reader.ReadString());
		}
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(notice.Count);
		for (int i = 0; i < notice.Count; i++)
		{
			writer.Write(notice[i]);
		}
	}
}
