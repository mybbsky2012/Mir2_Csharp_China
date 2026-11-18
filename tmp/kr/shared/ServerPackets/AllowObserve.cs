using System.IO;

namespace ServerPackets;

public sealed class AllowObserve : Packet
{
	public bool Allow;

	public override short Index => 140;

	protected override void ReadPacket(BinaryReader reader)
	{
		Allow = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Allow);
	}
}
