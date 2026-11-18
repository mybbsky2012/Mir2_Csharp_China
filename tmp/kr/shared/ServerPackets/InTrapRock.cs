using System.IO;

namespace ServerPackets;

public sealed class InTrapRock : Packet
{
	public bool Trapped;

	public override short Index => 159;

	protected override void ReadPacket(BinaryReader reader)
	{
		Trapped = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Trapped);
	}
}
