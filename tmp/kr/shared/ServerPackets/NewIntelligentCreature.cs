using System.IO;

namespace ServerPackets;

public sealed class NewIntelligentCreature : Packet
{
	public ClientIntelligentCreature Creature;

	public override short Index => 237;

	protected override void ReadPacket(BinaryReader reader)
	{
		Creature = new ClientIntelligentCreature(reader);
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		Creature.Save(writer);
	}
}
