using System.IO;

namespace ClientPackets;

public sealed class RefreshFriends : Packet
{
	public override short Index => 126;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
