using System.Collections.Generic;
using System.IO;

namespace ServerPackets;

public sealed class GuildMemberChange : Packet
{
	public string Name = string.Empty;

	public byte Status;

	public byte RankIndex;

	public List<GuildRank> Ranks = new List<GuildRank>();

	public override short Index => 165;

	protected override void ReadPacket(BinaryReader reader)
	{
		Name = reader.ReadString();
		RankIndex = reader.ReadByte();
		Status = reader.ReadByte();
		if (Status > 5)
		{
			int num = reader.ReadInt32();
			for (int i = 0; i < num; i++)
			{
				Ranks.Add(new GuildRank(reader));
			}
		}
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Name);
		writer.Write(RankIndex);
		writer.Write(Status);
		if (Status > 5)
		{
			writer.Write(Ranks.Count);
			for (int i = 0; i < Ranks.Count; i++)
			{
				Ranks[i].Save(writer);
			}
		}
	}
}
