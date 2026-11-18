using System.IO;

namespace ServerPackets;

public sealed class MentorUpdate : Packet
{
	public string Name;

	public ushort Level;

	public bool Online;

	public long MenteeEXP;

	public override short Index => 245;

	protected override void ReadPacket(BinaryReader reader)
	{
		Name = reader.ReadString();
		Level = reader.ReadUInt16();
		Online = reader.ReadBoolean();
		MenteeEXP = reader.ReadInt64();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Name);
		writer.Write(Level);
		writer.Write(Online);
		writer.Write(MenteeEXP);
	}
}
