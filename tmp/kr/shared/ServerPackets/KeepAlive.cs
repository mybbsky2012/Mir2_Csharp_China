using System.IO;

namespace ServerPackets;

public sealed class KeepAlive : Packet
{
	public long Time;

	public override short Index => 3;

	protected override void ReadPacket(BinaryReader reader)
	{
		Time = reader.ReadInt64();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Time);
	}
}
