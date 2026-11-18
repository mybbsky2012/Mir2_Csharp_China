using System.IO;

namespace ServerPackets;

public sealed class StartGameDelay : Packet
{
	public long Milliseconds;

	public override short Index => 16;

	protected override void ReadPacket(BinaryReader reader)
	{
		Milliseconds = reader.ReadInt64();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Milliseconds);
	}
}
