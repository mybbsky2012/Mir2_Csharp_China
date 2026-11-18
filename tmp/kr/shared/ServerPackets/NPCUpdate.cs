using System.IO;

namespace ServerPackets;

public sealed class NPCUpdate : Packet
{
	public uint NPCID;

	public override short Index => 185;

	protected override void ReadPacket(BinaryReader reader)
	{
		NPCID = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(NPCID);
	}
}
