using System.IO;

namespace ServerPackets;

public sealed class HealthChanged : Packet
{
	public int HP;

	public int MP;

	public override short Index => 75;

	protected override void ReadPacket(BinaryReader reader)
	{
		HP = reader.ReadInt32();
		MP = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(HP);
		writer.Write(MP);
	}
}
