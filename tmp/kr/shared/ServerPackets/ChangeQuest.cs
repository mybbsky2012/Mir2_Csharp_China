using System.IO;

namespace ServerPackets;

public sealed class ChangeQuest : Packet
{
	public ClientQuestProgress Quest = new ClientQuestProgress();

	public QuestState QuestState;

	public bool TrackQuest;

	public override short Index => 199;

	protected override void ReadPacket(BinaryReader reader)
	{
		Quest = new ClientQuestProgress(reader);
		QuestState = (QuestState)reader.ReadByte();
		TrackQuest = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		Quest.Save(writer);
		writer.Write((byte)QuestState);
		writer.Write(TrackQuest);
	}
}
