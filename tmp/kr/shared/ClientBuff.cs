using System.IO;

public class ClientBuff
{
	public BuffType Type;

	public string Caster;

	public bool Visible;

	public uint ObjectID;

	public long ExpireTime;

	public bool Infinite;

	public Stats Stats;

	public bool Paused;

	public int[] Values;

	public string Name;

	public int Icon;

	public ClientBuff()
	{
		Stats = new Stats();
		Name = string.Empty;
	}

	public ClientBuff(BinaryReader reader)
	{
		Caster = null;
		Type = (BuffType)reader.ReadByte();
		Visible = reader.ReadBoolean();
		ObjectID = reader.ReadUInt32();
		ExpireTime = reader.ReadInt64();
		Infinite = reader.ReadBoolean();
		Paused = reader.ReadBoolean();
		Stats = new Stats(reader);
		int num = reader.ReadInt32();
		Values = new int[num];
		for (int i = 0; i < num; i++)
		{
			Values[i] = reader.ReadInt32();
		}
		Name = reader.ReadString();
		Icon = reader.ReadInt32();
	}

	public void Save(BinaryWriter writer)
	{
		writer.Write((byte)Type);
		writer.Write(Visible);
		writer.Write(ObjectID);
		writer.Write(ExpireTime);
		writer.Write(Infinite);
		writer.Write(Paused);
		Stats.Save(writer);
		writer.Write(Values.Length);
		for (int i = 0; i < Values.Length; i++)
		{
			writer.Write(Values[i]);
		}
		writer.Write(Name ?? string.Empty);
		writer.Write(Icon);
	}
}
