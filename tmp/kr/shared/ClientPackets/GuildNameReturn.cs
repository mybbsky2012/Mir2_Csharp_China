using System.IO;

namespace ClientPackets;

public sealed class GuildNameReturn : Packet
{
	public string Name;

	public override short Index => 79;

	protected override void ReadPacket(BinaryReader reader)
	{
		Name = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Name);
	}
}
