using System.IO;

namespace ServerPackets;

public sealed class UpdateHeroSpawnState : Packet
{
	public HeroSpawnState State;

	public override short Index => 177;

	protected override void ReadPacket(BinaryReader reader)
	{
		State = (HeroSpawnState)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)State);
	}
}
