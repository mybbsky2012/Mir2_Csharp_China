using System.IO;

namespace ClientPackets;

public sealed class EditGuildMember : Packet
{
	public byte ChangeType;

	public byte RankIndex;

	public string Name = "";

	public string RankName = "";

	public override short Index => 76;

	protected override void ReadPacket(BinaryReader reader)
	{
		ChangeType = reader.ReadByte();
		RankIndex = reader.ReadByte();
		Name = reader.ReadString();
		RankName = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ChangeType);
		writer.Write(RankIndex);
		writer.Write(Name);
		writer.Write(RankName);
	}
}
