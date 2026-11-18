using System.IO;

namespace ClientPackets;

public sealed class RefineCancel : Packet
{
	public override short Index => 26;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
