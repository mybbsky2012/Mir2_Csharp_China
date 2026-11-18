using System.Collections.Generic;
using System.IO;

namespace ServerPackets;

public sealed class UpdateIntelligentCreatureList : Packet
{
	public List<ClientIntelligentCreature> CreatureList = new List<ClientIntelligentCreature>();

	public bool CreatureSummoned;

	public IntelligentCreatureType SummonedCreatureType = IntelligentCreatureType.None;

	public int PearlCount;

	public override short Index => 238;

	protected override void ReadPacket(BinaryReader reader)
	{
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			CreatureList.Add(new ClientIntelligentCreature(reader));
		}
		CreatureSummoned = reader.ReadBoolean();
		SummonedCreatureType = (IntelligentCreatureType)reader.ReadByte();
		PearlCount = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(CreatureList.Count);
		for (int i = 0; i < CreatureList.Count; i++)
		{
			CreatureList[i].Save(writer);
		}
		writer.Write(CreatureSummoned);
		writer.Write((byte)SummonedCreatureType);
		writer.Write(PearlCount);
	}
}
