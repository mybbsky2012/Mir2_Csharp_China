using System.IO;

namespace ServerPackets;

public sealed class SwitchGroup : Packet
{
	public bool AllowGroup;

	public override short Index => 129;

	protected override void ReadPacket(BinaryReader reader)
	{
		AllowGroup = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(AllowGroup);
	}
}
