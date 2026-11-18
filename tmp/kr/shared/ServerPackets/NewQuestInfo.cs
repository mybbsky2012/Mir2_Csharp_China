using System.IO;

namespace ServerPackets;

public sealed class NewQuestInfo : Packet
{
	public ClientQuestInfo Info;

	public override short Index => 202;

	protected override void ReadPacket(BinaryReader reader)
	{
		Info = new ClientQuestInfo(reader);
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		Info.Save(writer);
	}
}
