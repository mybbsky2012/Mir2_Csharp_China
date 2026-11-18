using System.IO;

namespace ServerPackets;

public sealed class PlaySound : Packet
{
	public int Sound;

	public override short Index => 266;

	protected override void ReadPacket(BinaryReader reader)
	{
		Sound = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Sound);
	}
}
