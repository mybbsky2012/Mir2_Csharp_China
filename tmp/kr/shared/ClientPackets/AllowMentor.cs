using System.IO;

namespace ClientPackets;

public sealed class AllowMentor : Packet
{
	public override short Index => 91;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
