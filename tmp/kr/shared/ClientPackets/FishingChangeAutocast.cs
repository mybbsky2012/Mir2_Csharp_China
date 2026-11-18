using System.IO;

namespace ClientPackets;

public sealed class FishingChangeAutocast : Packet
{
	public bool AutoCast;

	public override short Index => 100;

	protected override void ReadPacket(BinaryReader reader)
	{
		AutoCast = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(AutoCast);
	}
}
