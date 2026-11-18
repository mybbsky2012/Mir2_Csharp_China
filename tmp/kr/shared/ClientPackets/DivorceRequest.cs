using System.IO;

namespace ClientPackets;

public sealed class DivorceRequest : Packet
{
	public override short Index => 87;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
