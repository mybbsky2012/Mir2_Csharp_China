using System.IO;

namespace ServerPackets;

public sealed class BaseStatsInfo : Packet
{
	public BaseStats Stats;

	public override short Index => 160;

	protected override void ReadPacket(BinaryReader reader)
	{
		Stats = new BaseStats(reader);
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		Stats.Save(writer);
	}
}
