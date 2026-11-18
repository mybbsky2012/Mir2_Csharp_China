using System.IO;

namespace ServerPackets;

public sealed class ManageHeroes : Packet
{
	public int MaximumCount;

	public ClientHeroInformation CurrentHero;

	public ClientHeroInformation[] Heroes;

	public override short Index => 182;

	protected override void ReadPacket(BinaryReader reader)
	{
		MaximumCount = reader.ReadInt32();
		if (reader.ReadBoolean())
		{
			CurrentHero = new ClientHeroInformation(reader);
		}
		if (!reader.ReadBoolean())
		{
			return;
		}
		int num = reader.ReadInt32();
		Heroes = new ClientHeroInformation[num];
		for (int i = 0; i < num; i++)
		{
			if (reader.ReadBoolean())
			{
				Heroes[i] = new ClientHeroInformation(reader);
			}
		}
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(MaximumCount);
		writer.Write(CurrentHero != null);
		if (CurrentHero != null)
		{
			CurrentHero.Save(writer);
		}
		writer.Write(Heroes != null);
		if (Heroes == null)
		{
			return;
		}
		writer.Write(Heroes.Length);
		for (int i = 0; i < Heroes.Length; i++)
		{
			writer.Write(Heroes[i] != null);
			if (Heroes[i] != null)
			{
				Heroes[i].Save(writer);
			}
		}
	}
}
