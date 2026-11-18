using System.IO;

namespace ServerPackets;

public sealed class OpenBrowser : Packet
{
	public string Url;

	public override short Index => 265;

	protected override void ReadPacket(BinaryReader reader)
	{
		Url = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Url);
	}
}
