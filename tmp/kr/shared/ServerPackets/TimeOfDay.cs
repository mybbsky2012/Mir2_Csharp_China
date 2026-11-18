using System.IO;

namespace ServerPackets;

public sealed class TimeOfDay : Packet
{
	public LightSetting Lights;

	public override short Index => 59;

	protected override void ReadPacket(BinaryReader reader)
	{
		Lights = (LightSetting)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Lights);
	}
}
