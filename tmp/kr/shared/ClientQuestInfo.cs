using System.Collections.Generic;
using System.IO;

public class ClientQuestInfo
{
	public int Index;

	public uint NPCIndex;

	public string Name;

	public string Group;

	public List<string> Description = new List<string>();

	public List<string> TaskDescription = new List<string>();

	public List<string> ReturnDescription = new List<string>();

	public List<string> CompletionDescription = new List<string>();

	public int MinLevelNeeded;

	public int MaxLevelNeeded;

	public int QuestNeeded;

	public RequiredClass ClassNeeded;

	public QuestType Type;

	public int TimeLimitInSeconds;

	public uint RewardGold;

	public uint RewardExp;

	public uint RewardCredit;

	public List<QuestItemReward> RewardsFixedItem = new List<QuestItemReward>();

	public List<QuestItemReward> RewardsSelectItem = new List<QuestItemReward>();

	public uint FinishNPCIndex;

	public bool SameFinishNPC => NPCIndex == FinishNPCIndex;

	public ClientQuestInfo()
	{
	}

	public ClientQuestInfo(BinaryReader reader)
	{
		Index = reader.ReadInt32();
		NPCIndex = reader.ReadUInt32();
		Name = reader.ReadString();
		Group = reader.ReadString();
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			Description.Add(reader.ReadString());
		}
		num = reader.ReadInt32();
		for (int j = 0; j < num; j++)
		{
			TaskDescription.Add(reader.ReadString());
		}
		num = reader.ReadInt32();
		for (int k = 0; k < num; k++)
		{
			ReturnDescription.Add(reader.ReadString());
		}
		num = reader.ReadInt32();
		for (int l = 0; l < num; l++)
		{
			CompletionDescription.Add(reader.ReadString());
		}
		MinLevelNeeded = reader.ReadInt32();
		MaxLevelNeeded = reader.ReadInt32();
		QuestNeeded = reader.ReadInt32();
		ClassNeeded = (RequiredClass)reader.ReadByte();
		Type = (QuestType)reader.ReadByte();
		TimeLimitInSeconds = reader.ReadInt32();
		RewardGold = reader.ReadUInt32();
		RewardExp = reader.ReadUInt32();
		RewardCredit = reader.ReadUInt32();
		num = reader.ReadInt32();
		for (int m = 0; m < num; m++)
		{
			RewardsFixedItem.Add(new QuestItemReward(reader));
		}
		num = reader.ReadInt32();
		for (int n = 0; n < num; n++)
		{
			RewardsSelectItem.Add(new QuestItemReward(reader));
		}
		FinishNPCIndex = reader.ReadUInt32();
	}

	public void Save(BinaryWriter writer)
	{
		writer.Write(Index);
		writer.Write(NPCIndex);
		writer.Write(Name);
		writer.Write(Group);
		writer.Write(Description.Count);
		for (int i = 0; i < Description.Count; i++)
		{
			writer.Write(Description[i]);
		}
		writer.Write(TaskDescription.Count);
		for (int j = 0; j < TaskDescription.Count; j++)
		{
			writer.Write(TaskDescription[j]);
		}
		writer.Write(ReturnDescription.Count);
		for (int k = 0; k < ReturnDescription.Count; k++)
		{
			writer.Write(ReturnDescription[k]);
		}
		writer.Write(CompletionDescription.Count);
		for (int l = 0; l < CompletionDescription.Count; l++)
		{
			writer.Write(CompletionDescription[l]);
		}
		writer.Write(MinLevelNeeded);
		writer.Write(MaxLevelNeeded);
		writer.Write(QuestNeeded);
		writer.Write((byte)ClassNeeded);
		writer.Write((byte)Type);
		writer.Write(TimeLimitInSeconds);
		writer.Write(RewardGold);
		writer.Write(RewardExp);
		writer.Write(RewardCredit);
		writer.Write(RewardsFixedItem.Count);
		for (int m = 0; m < RewardsFixedItem.Count; m++)
		{
			RewardsFixedItem[m].Save(writer);
		}
		writer.Write(RewardsSelectItem.Count);
		for (int n = 0; n < RewardsSelectItem.Count; n++)
		{
			RewardsSelectItem[n].Save(writer);
		}
		writer.Write(FinishNPCIndex);
	}

	public QuestIcon GetQuestIcon(bool taken = false, bool completed = false)
	{
		QuestIcon result = QuestIcon.None;
		switch (Type)
		{
		case QuestType.一般:
		case QuestType.重复:
			result = ((!completed) ? (taken ? QuestIcon.QuestionWhite : QuestIcon.ExclamationYellow) : QuestIcon.QuestionYellow);
			break;
		case QuestType.每日:
			result = ((!completed) ? (taken ? QuestIcon.QuestionWhite : QuestIcon.ExclamationBlue) : QuestIcon.QuestionBlue);
			break;
		case QuestType.主线:
			result = ((!completed) ? (taken ? QuestIcon.QuestionWhite : QuestIcon.ExclamationGreen) : QuestIcon.QuestionGreen);
			break;
		}
		return result;
	}
}
