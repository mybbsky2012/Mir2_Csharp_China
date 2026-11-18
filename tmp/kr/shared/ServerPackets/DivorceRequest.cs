using System.IO;

namespace ServerPackets;

public sealed class DivorceRequest : Packet
{
	public string Name;

	public override short Index => 188;

	protected override void ReadPacket(BinaryReader reader)
	{
		Name = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Name);
	}
}
