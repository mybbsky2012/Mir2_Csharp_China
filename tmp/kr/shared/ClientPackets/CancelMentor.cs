using System.IO;

namespace ClientPackets;

public sealed class CancelMentor : Packet
{
	public override short Index => 92;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
