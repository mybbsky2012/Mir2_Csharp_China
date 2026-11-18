using System.IO;

namespace ServerPackets;

public sealed class MentorRequest : Packet
{
	public string Name;

	public ushort Level;

	public override short Index => 189;

	protected override void ReadPacket(BinaryReader reader)
	{
		Name = reader.ReadString();
		Level = reader.ReadUInt16();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Name);
		writer.Write(Level);
	}
}
