using System.IO;

namespace ServerPackets;

public sealed class ParcelCollected : Packet
{
	public sbyte Result;

	public override short Index => 233;

	protected override void ReadPacket(BinaryReader reader)
	{
		Result = reader.ReadSByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Result);
	}
}
