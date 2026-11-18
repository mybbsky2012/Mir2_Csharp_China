using System.Collections.Generic;
using System.IO;

public class GuildRank
{
	public List<GuildMember> Members = new List<GuildMember>();

	public string Name = "";

	public int Index;

	public GuildRankOptions Options;

	public GuildRank()
	{
	}

	public GuildRank(BinaryReader reader, bool offline = false)
	{
		Name = reader.ReadString();
		Options = (GuildRankOptions)reader.ReadByte();
		if (!offline)
		{
			Index = reader.ReadInt32();
		}
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			Members.Add(new GuildMember(reader, offline));
		}
	}

	public void Save(BinaryWriter writer, bool save = false)
	{
		writer.Write(Name);
		writer.Write((byte)Options);
		if (!save)
		{
			writer.Write(Index);
		}
		writer.Write(Members.Count);
		for (int i = 0; i < Members.Count; i++)
		{
			Members[i].Save(writer);
		}
	}
}
