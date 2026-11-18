using System.IO;

namespace ClientPackets;

public sealed class RequestIntelligentCreatureUpdates : Packet
{
	public bool Update;

	public override short Index => 123;

	protected override void ReadPacket(BinaryReader reader)
	{
		Update = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Update);
	}
}
