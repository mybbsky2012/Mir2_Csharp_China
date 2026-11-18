using System.IO;

namespace ServerPackets;

public sealed class AddBuff : Packet
{
	public ClientBuff Buff;

	public override short Index => 142;

	protected override void ReadPacket(BinaryReader reader)
	{
		Buff = new ClientBuff(reader);
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		Buff.Save(writer);
	}
}
