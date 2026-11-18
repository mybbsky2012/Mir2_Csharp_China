using System.IO;

namespace ClientPackets;

public sealed class Disconnect : Packet
{
	public override short Index => 1;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
