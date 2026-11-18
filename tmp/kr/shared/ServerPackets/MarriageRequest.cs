using System.IO;

namespace ServerPackets;

public sealed class MarriageRequest : Packet
{
	public string Name;

	public override short Index => 187;

	protected override void ReadPacket(BinaryReader reader)
	{
		Name = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Name);
	}
}
