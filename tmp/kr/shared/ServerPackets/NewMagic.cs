using System.IO;

namespace ServerPackets;

public sealed class NewMagic : Packet
{
	public ClientMagic Magic;

	public bool Hero;

	public override short Index => 115;

	protected override void ReadPacket(BinaryReader reader)
	{
		Magic = new ClientMagic(reader);
		Hero = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		Magic.Save(writer);
		writer.Write(Hero);
	}
}
