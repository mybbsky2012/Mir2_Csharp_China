using System.Collections.Generic;
using System.IO;

namespace ServerPackets;

public sealed class GuildNoticeChange : Packet
{
	public int update;

	public List<string> notice = new List<string>();

	public override short Index => 164;

	protected override void ReadPacket(BinaryReader reader)
	{
		update = reader.ReadInt32();
		for (int i = 0; i < update; i++)
		{
			notice.Add(reader.ReadString());
		}
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		if (update < 0)
		{
			writer.Write(update);
			return;
		}
		writer.Write(notice.Count);
		for (int i = 0; i < notice.Count; i++)
		{
			writer.Write(notice[i]);
		}
	}
}
