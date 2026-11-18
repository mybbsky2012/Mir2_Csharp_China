using System.IO;

namespace ServerPackets;

public sealed class RefineCancel : Packet
{
	public bool Unlock;

	public override short Index => 46;

	protected override void ReadPacket(BinaryReader reader)
	{
		Unlock = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Unlock);
	}
}
