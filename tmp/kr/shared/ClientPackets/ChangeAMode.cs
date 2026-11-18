using System.IO;

namespace ClientPackets;

public sealed class ChangeAMode : Packet
{
	public AttackMode Mode;

	public override short Index => 41;

	protected override void ReadPacket(BinaryReader reader)
	{
		Mode = (AttackMode)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Mode);
	}
}
