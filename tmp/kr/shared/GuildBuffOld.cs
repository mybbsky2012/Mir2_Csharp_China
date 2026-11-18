using System.IO;

public class GuildBuffOld
{
	public GuildBuffOld()
	{
	}

	public GuildBuffOld(BinaryReader reader)
	{
		reader.ReadByte();
		reader.ReadInt64();
	}
}
