using System.IO;

namespace ServerPackets;

public sealed class Awakening : Packet
{
	public int result;

	public long removeID = -1L;

	public override short Index => 228;

	protected override void ReadPacket(BinaryReader reader)
	{
		result = reader.ReadInt32();
		removeID = reader.ReadInt64();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(result);
		writer.Write(removeID);
	}
}
