using System.Collections.Generic;
using System.IO;

public class ClientMapInfo
{
	public int Width;

	public int Height;

	public int BigMap;

	public string Title;

	public List<ClientMovementInfo> Movements = new List<ClientMovementInfo>();

	public List<ClientNPCInfo> NPCs = new List<ClientNPCInfo>();

	public ClientMapInfo()
	{
	}

	public ClientMapInfo(BinaryReader reader)
	{
		Title = reader.ReadString();
		Width = reader.ReadInt32();
		Height = reader.ReadInt32();
		BigMap = reader.ReadInt32();
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			Movements.Add(new ClientMovementInfo(reader));
		}
		num = reader.ReadInt32();
		for (int j = 0; j < num; j++)
		{
			NPCs.Add(new ClientNPCInfo(reader));
		}
	}

	public void Save(BinaryWriter writer)
	{
		writer.Write(Title);
		writer.Write(Width);
		writer.Write(Height);
		writer.Write(BigMap);
		writer.Write(Movements.Count);
		for (int i = 0; i < Movements.Count; i++)
		{
			Movements[i].Save(writer);
		}
		writer.Write(NPCs.Count);
		for (int j = 0; j < NPCs.Count; j++)
		{
			NPCs[j].Save(writer);
		}
	}
}
