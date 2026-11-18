using System.IO;

namespace ClientPackets;

public sealed class NPCConfirmInput : Packet
{
	public uint NPCID;

	public string PageName;

	public string Value;

	public override short Index => 129;

	protected override void ReadPacket(BinaryReader reader)
	{
		NPCID = reader.ReadUInt32();
		PageName = reader.ReadString();
		Value = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(NPCID);
		writer.Write(PageName);
		writer.Write(Value);
	}
}
