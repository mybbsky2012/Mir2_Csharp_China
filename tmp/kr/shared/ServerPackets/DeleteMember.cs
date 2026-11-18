using System.IO;

namespace ServerPackets;

public sealed class DeleteMember : Packet
{
	public string Name = string.Empty;

	public override short Index => 131;

	protected override void ReadPacket(BinaryReader reader)
	{
		Name = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Name);
	}
}
