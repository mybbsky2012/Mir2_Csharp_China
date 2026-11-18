using System.IO;

namespace ServerPackets;

public sealed class LogOutFailed : Packet
{
	public override short Index => 57;

	public override bool Observable => false;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
