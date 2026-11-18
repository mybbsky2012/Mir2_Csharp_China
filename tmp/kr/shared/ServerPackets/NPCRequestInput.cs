using System.IO;

namespace ServerPackets;

public sealed class NPCRequestInput : Packet
{
	public uint NPCID;

	public string PageName;

	public override short Index => 247;

	protected override void ReadPacket(BinaryReader reader)
	{
		NPCID = reader.ReadUInt32();
		PageName = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(NPCID);
		writer.Write(PageName);
	}
}
