using System.IO;

namespace ServerPackets;

public sealed class AwakeningNeedMaterials : Packet
{
	public ItemInfo[] Materials;

	public byte[] MaterialsCount;

	public override short Index => 226;

	protected override void ReadPacket(BinaryReader reader)
	{
		if (!reader.ReadBoolean())
		{
			return;
		}
		int num = reader.ReadInt32();
		Materials = new ItemInfo[num];
		MaterialsCount = new byte[num];
		for (int i = 0; i < Materials.Length; i++)
		{
			if (reader.ReadBoolean())
			{
				Materials[i] = new ItemInfo(reader);
				MaterialsCount[i] = reader.ReadByte();
			}
		}
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Materials != null);
		if (Materials == null)
		{
			return;
		}
		writer.Write(Materials.Length);
		for (int i = 0; i < Materials.Length; i++)
		{
			writer.Write(Materials[i] != null);
			if (Materials[i] != null)
			{
				Materials[i].Save(writer);
				writer.Write(MaterialsCount[i]);
			}
		}
	}
}
