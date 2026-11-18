using System.IO;

namespace ServerPackets;

public sealed class NewHeroInfo : Packet
{
	public ClientHeroInformation Info;

	public int StorageIndex = -1;

	public override short Index => 33;

	protected override void ReadPacket(BinaryReader reader)
	{
		Info = new ClientHeroInformation(reader);
		StorageIndex = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		Info.Save(writer);
		writer.Write(StorageIndex);
	}
}
