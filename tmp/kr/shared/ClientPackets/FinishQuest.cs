using System.IO;

namespace ClientPackets;

public sealed class FinishQuest : Packet
{
	public int QuestIndex;

	public int SelectedItemIndex;

	public override short Index => 102;

	protected override void ReadPacket(BinaryReader reader)
	{
		QuestIndex = reader.ReadInt32();
		SelectedItemIndex = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(QuestIndex);
		writer.Write(SelectedItemIndex);
	}
}
