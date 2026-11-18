using System.IO;

namespace ServerPackets;

public sealed class SearchMapResult : Packet
{
	public int MapIndex = -1;

	public uint NPCIndex;

	public override short Index => 20;

	protected override void ReadPacket(BinaryReader reader)
	{
		MapIndex = reader.ReadInt32();
		NPCIndex = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(MapIndex);
		writer.Write(NPCIndex);
	}
}
