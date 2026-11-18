using System.IO;

namespace ClientPackets;

public sealed class FishingCast : Packet
{
	public bool CastOut;

	public override short Index => 99;

	protected override void ReadPacket(BinaryReader reader)
	{
		CastOut = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(CastOut);
	}
}
