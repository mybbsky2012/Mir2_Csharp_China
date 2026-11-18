using System.IO;

namespace ClientPackets;

public sealed class Inspect : Packet
{
	public uint ObjectID;

	public bool Ranking;

	public bool Hero;

	public override short Index => 39;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Ranking = reader.ReadBoolean();
		Hero = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(Ranking);
		writer.Write(Hero);
	}
}
