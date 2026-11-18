using System.IO;

namespace ClientPackets;

public sealed class ChangeMarriage : Packet
{
	public override short Index => 86;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
