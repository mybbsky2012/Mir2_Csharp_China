using System.IO;

namespace ClientPackets;

public sealed class SetHeroBehaviour : Packet
{
	public HeroBehaviour Behaviour;

	public override short Index => 63;

	protected override void ReadPacket(BinaryReader reader)
	{
		Behaviour = (HeroBehaviour)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Behaviour);
	}
}
