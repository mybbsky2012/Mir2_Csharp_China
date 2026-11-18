using System.IO;

public class GuildBuff
{
	public int Id;

	public GuildBuffInfo Info;

	public bool Active;

	public int ActiveTimeRemaining;

	public bool UsingGuildSkillIcon
	{
		get
		{
			if (Info != null)
			{
				return Info.Icon < 1000;
			}
			return false;
		}
	}

	public GuildBuff()
	{
	}

	public GuildBuff(BinaryReader reader)
	{
		Id = reader.ReadInt32();
		Active = reader.ReadBoolean();
		ActiveTimeRemaining = reader.ReadInt32();
	}

	public void Save(BinaryWriter writer)
	{
		writer.Write(Id);
		writer.Write(Active);
		writer.Write(ActiveTimeRemaining);
	}
}
