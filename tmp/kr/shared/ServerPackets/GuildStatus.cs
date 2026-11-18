using System.IO;

namespace ServerPackets;

public sealed class GuildStatus : Packet
{
	public string GuildName = string.Empty;

	public string GuildRankName = string.Empty;

	public byte Level;

	public long Experience;

	public long MaxExperience;

	public uint Gold;

	public byte SparePoints;

	public int MemberCount;

	public int MaxMembers;

	public bool Voting;

	public byte ItemCount;

	public byte BuffCount;

	public GuildRankOptions MyOptions;

	public int MyRankId;

	public override short Index => 166;

	protected override void ReadPacket(BinaryReader reader)
	{
		GuildName = reader.ReadString();
		GuildRankName = reader.ReadString();
		Level = reader.ReadByte();
		Experience = reader.ReadInt64();
		MaxExperience = reader.ReadInt64();
		Gold = reader.ReadUInt32();
		SparePoints = reader.ReadByte();
		MemberCount = reader.ReadInt32();
		MaxMembers = reader.ReadInt32();
		Voting = reader.ReadBoolean();
		ItemCount = reader.ReadByte();
		BuffCount = reader.ReadByte();
		MyOptions = (GuildRankOptions)reader.ReadByte();
		MyRankId = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(GuildName);
		writer.Write(GuildRankName);
		writer.Write(Level);
		writer.Write(Experience);
		writer.Write(MaxExperience);
		writer.Write(Gold);
		writer.Write(SparePoints);
		writer.Write(MemberCount);
		writer.Write(MaxMembers);
		writer.Write(Voting);
		writer.Write(ItemCount);
		writer.Write(BuffCount);
		writer.Write((byte)MyOptions);
		writer.Write(MyRankId);
	}
}
