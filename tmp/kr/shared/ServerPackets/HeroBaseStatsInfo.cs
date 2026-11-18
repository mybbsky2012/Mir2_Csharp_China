using System.IO;

namespace ServerPackets;

public sealed class HeroBaseStatsInfo : Packet
{
	public BaseStats Stats;

	public override short Index => 161;

	protected override void ReadPacket(BinaryReader reader)
	{
		Stats = new BaseStats(reader);
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		Stats.Save(writer);
	}
}
