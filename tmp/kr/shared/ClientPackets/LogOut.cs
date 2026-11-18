using System.IO;

namespace ClientPackets;

public sealed class LogOut : Packet
{
	public override short Index => 9;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
