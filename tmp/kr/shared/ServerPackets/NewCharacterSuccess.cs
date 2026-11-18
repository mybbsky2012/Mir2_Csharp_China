using System.IO;

namespace ServerPackets;

public sealed class NewCharacterSuccess : Packet
{
	public SelectInfo CharInfo;

	public override short Index => 11;

	protected override void ReadPacket(BinaryReader reader)
	{
		CharInfo = new SelectInfo(reader);
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		CharInfo.Save(writer);
	}
}
