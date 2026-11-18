using System.IO;

namespace ServerPackets;

public sealed class ShareQuest : Packet
{
	public int QuestIndex;

	public string SharerName;

	public override short Index => 201;

	protected override void ReadPacket(BinaryReader reader)
	{
		QuestIndex = reader.ReadInt32();
		SharerName = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(QuestIndex);
		writer.Write(SharerName);
	}
}
