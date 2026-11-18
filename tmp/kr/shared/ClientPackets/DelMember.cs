using System.IO;

namespace ClientPackets;

public sealed class DelMember : Packet
{
	public string Name = string.Empty;

	public override short Index => 58;

	protected override void ReadPacket(BinaryReader reader)
	{
		Name = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Name);
	}
}
