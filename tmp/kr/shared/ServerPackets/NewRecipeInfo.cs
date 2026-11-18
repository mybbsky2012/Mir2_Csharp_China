using System.IO;

namespace ServerPackets;

public sealed class NewRecipeInfo : Packet
{
	public ClientRecipeInfo Info;

	public override short Index => 264;

	protected override void ReadPacket(BinaryReader reader)
	{
		Info = new ClientRecipeInfo(reader);
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		Info.Save(writer);
	}
}
