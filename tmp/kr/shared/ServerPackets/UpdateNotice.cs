using System.IO;

namespace ServerPackets;

public sealed class UpdateNotice : Packet
{
	public Notice Notice = new Notice();

	public override short Index => 269;

	protected override void ReadPacket(BinaryReader reader)
	{
		Notice = new Notice(reader);
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		Notice.Save(writer);
	}
}
