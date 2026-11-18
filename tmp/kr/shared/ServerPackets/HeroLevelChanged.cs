using System.IO;

namespace ServerPackets;

public sealed class HeroLevelChanged : Packet
{
	public ushort Level;

	public long Experience;

	public long MaxExperience;

	public override short Index => 86;

	protected override void ReadPacket(BinaryReader reader)
	{
		Level = reader.ReadUInt16();
		Experience = reader.ReadInt64();
		MaxExperience = reader.ReadInt64();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Level);
		writer.Write(Experience);
		writer.Write(MaxExperience);
	}
}
