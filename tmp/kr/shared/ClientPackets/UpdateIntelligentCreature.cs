using System.IO;

namespace ClientPackets;

public sealed class UpdateIntelligentCreature : Packet
{
	public ClientIntelligentCreature Creature;

	public bool SummonMe;

	public bool UnSummonMe;

	public bool ReleaseMe;

	public override short Index => 121;

	protected override void ReadPacket(BinaryReader reader)
	{
		Creature = new ClientIntelligentCreature(reader);
		SummonMe = reader.ReadBoolean();
		UnSummonMe = reader.ReadBoolean();
		ReleaseMe = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		Creature.Save(writer);
		writer.Write(SummonMe);
		writer.Write(UnSummonMe);
		writer.Write(ReleaseMe);
	}
}
