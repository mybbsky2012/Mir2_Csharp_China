using System.Collections.Generic;
using System.IO;

namespace ServerPackets;

public sealed class GuildBuffList : Packet
{
	public byte Remove;

	public List<GuildBuff> ActiveBuffs = new List<GuildBuff>();

	public List<GuildBuffInfo> GuildBuffs = new List<GuildBuffInfo>();

	public override short Index => 246;

	protected override void ReadPacket(BinaryReader reader)
	{
		Remove = reader.ReadByte();
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			ActiveBuffs.Add(new GuildBuff(reader));
		}
		num = reader.ReadInt32();
		for (int j = 0; j < num; j++)
		{
			GuildBuffs.Add(new GuildBuffInfo(reader));
		}
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Remove);
		writer.Write(ActiveBuffs.Count);
		for (int i = 0; i < ActiveBuffs.Count; i++)
		{
			ActiveBuffs[i].Save(writer);
		}
		writer.Write(GuildBuffs.Count);
		for (int j = 0; j < GuildBuffs.Count; j++)
		{
			GuildBuffs[j].Save(writer);
		}
	}
}
