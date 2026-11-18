using System.IO;

namespace ClientPackets;

public sealed class SwitchGroup : Packet
{
	public bool AllowGroup;

	public override short Index => 56;

	protected override void ReadPacket(BinaryReader reader)
	{
		AllowGroup = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(AllowGroup);
	}
}
