using System.Collections.Generic;
using System.IO;

namespace ServerPackets;

public sealed class CompleteQuest : Packet
{
	public List<int> CompletedQuests = new List<int>();

	public override short Index => 200;

	protected override void ReadPacket(BinaryReader reader)
	{
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			CompletedQuests.Add(reader.ReadInt32());
		}
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(CompletedQuests.Count);
		foreach (int completedQuest in CompletedQuests)
		{
			writer.Write(completedQuest);
		}
	}
}
