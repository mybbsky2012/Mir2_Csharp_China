using System.IO;

namespace ClientPackets;

public sealed class SearchMap : Packet
{
	public string Text;

	public override short Index => 38;

	protected override void ReadPacket(BinaryReader reader)
	{
		Text = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Text);
	}
}
