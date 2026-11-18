using System.IO;

namespace ClientPackets;

public sealed class AcceptQuest : Packet
{
	public uint NPCIndex;

	public int QuestIndex;

	public override short Index => 101;

	protected override void ReadPacket(BinaryReader reader)
	{
		NPCIndex = reader.ReadUInt32();
		QuestIndex = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(NPCIndex);
		writer.Write(QuestIndex);
	}
}
