using System.IO;

namespace ClientPackets;

public sealed class AbandonQuest : Packet
{
	public int QuestIndex;

	public override short Index => 103;

	protected override void ReadPacket(BinaryReader reader)
	{
		QuestIndex = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(QuestIndex);
	}
}
