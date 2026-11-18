using System.IO;

namespace ServerPackets;

public sealed class ReturnToLogin : Packet
{
	public override short Index => 58;

	public override bool Observable => false;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
