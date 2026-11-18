using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

public static class Functions
{
	public static bool CompareBytes(byte[] a, byte[] b)
	{
		if (a == b)
		{
			return true;
		}
		if (a == null || b == null || a.Length != b.Length)
		{
			return false;
		}
		for (int i = 0; i < a.Length; i++)
		{
			if (a[i] != b[i])
			{
				return false;
			}
		}
		return true;
	}

	public static string ConvertByteSize(double byteCount)
	{
		string result = "0 Bytes";
		if (byteCount >= 1073741824.0)
		{
			result = $"{byteCount / 1073741824.0:##.##}" + " GB";
		}
		else if (byteCount >= 1048576.0)
		{
			result = $"{byteCount / 1048576.0:##.##}" + " MB";
		}
		else if (byteCount >= 1024.0)
		{
			result = $"{byteCount / 1024.0:##.##}" + " KB";
		}
		else if (byteCount > 0.0 && byteCount < 1024.0)
		{
			result = byteCount + " Bytes";
		}
		return result;
	}

	public static bool TryParse(string s, out Point temp)
	{
		temp = Point.Empty;
		if (string.IsNullOrWhiteSpace(s))
		{
			return false;
		}
		string[] array = s.Split(',');
		if (array.Length <= 1)
		{
			return false;
		}
		if (!int.TryParse(array[0], out var result))
		{
			return false;
		}
		if (!int.TryParse(array[1], out var result2))
		{
			return false;
		}
		temp = new Point(result, result2);
		return true;
	}

	public static Point Subtract(this Point p1, Point p2)
	{
		return new Point(p1.X - p2.X, p1.Y - p2.Y);
	}

	public static Point Subtract(this Point p1, int x, int y)
	{
		return new Point(p1.X - x, p1.Y - y);
	}

	public static Point Add(this Point p1, Point p2)
	{
		return new Point(p1.X + p2.X, p1.Y + p2.Y);
	}

	public static Point Add(this Point p1, int x, int y)
	{
		return new Point(p1.X + x, p1.Y + y);
	}

	public static string PointToString(Point p)
	{
		return $"{p.X}, {p.Y}";
	}

	public static bool InRange(Point a, Point b, int i)
	{
		if (Math.Abs(a.X - b.X) <= i)
		{
			return Math.Abs(a.Y - b.Y) <= i;
		}
		return false;
	}

	public static bool FacingEachOther(MirDirection dirA, Point pointA, MirDirection dirB, Point pointB)
	{
		if (dirA == DirectionFromPoint(pointA, pointB) && dirB == DirectionFromPoint(pointB, pointA))
		{
			return true;
		}
		return false;
	}

	public static string PrintTimeSpanFromSeconds(double secs, bool accurate = true)
	{
		TimeSpan timeSpan = TimeSpan.FromSeconds(secs);
		if (timeSpan.TotalMinutes < 1.0)
		{
			return $"{timeSpan.Seconds}秒";
		}
		if (timeSpan.TotalHours < 1.0)
		{
			return accurate ? $"{timeSpan.Minutes}分 {timeSpan.Seconds:D2}秒" : $"{timeSpan.Minutes}分";
		}
		if (timeSpan.TotalDays < 1.0)
		{
			return accurate ? $"{timeSpan.Hours}时 {timeSpan.Minutes:D2}分 {timeSpan.Seconds:D2}秒" : $"{(int)timeSpan.TotalHours}时 {timeSpan.Minutes:D2}分";
		}
		return accurate ? $"{timeSpan.Days}天 {timeSpan.Hours:D2}时 {timeSpan.Minutes:D2}分 {timeSpan.Seconds:D2}秒" : $"{(int)timeSpan.TotalDays}天 {timeSpan.Hours}时 {timeSpan.Minutes:D2}分";
	}

	public static string PrintTimeSpanFromMilliSeconds(double milliSeconds)
	{
		TimeSpan timeSpan = TimeSpan.FromMilliseconds(milliSeconds);
		if (timeSpan.TotalMinutes < 1.0)
		{
			return $"{timeSpan.Seconds}.{(decimal)(timeSpan.Milliseconds / 100)}秒";
		}
		if (timeSpan.TotalHours < 1.0)
		{
			return $"{timeSpan.TotalMinutes}分 {timeSpan.Seconds:D2}秒";
		}
		if (timeSpan.TotalDays < 1.0)
		{
			return $"{(int)timeSpan.TotalHours}时 {timeSpan.Minutes:D2}分 {timeSpan.Seconds:D2}秒";
		}
		return $"{timeSpan.Days}天 {timeSpan.Hours}时 {timeSpan.Minutes:D2}分 {timeSpan.Seconds:D2}秒";
	}

	public static MirDirection PreviousDir(MirDirection d)
	{
		return d switch
		{
			MirDirection.Up => MirDirection.UpLeft, 
			MirDirection.UpRight => MirDirection.Up, 
			MirDirection.Right => MirDirection.UpRight, 
			MirDirection.DownRight => MirDirection.Right, 
			MirDirection.Down => MirDirection.DownRight, 
			MirDirection.DownLeft => MirDirection.Down, 
			MirDirection.Left => MirDirection.DownLeft, 
			MirDirection.UpLeft => MirDirection.Left, 
			_ => d, 
		};
	}

	public static MirDirection NextDir(MirDirection d)
	{
		return d switch
		{
			MirDirection.Up => MirDirection.UpRight, 
			MirDirection.UpRight => MirDirection.Right, 
			MirDirection.Right => MirDirection.DownRight, 
			MirDirection.DownRight => MirDirection.Down, 
			MirDirection.Down => MirDirection.DownLeft, 
			MirDirection.DownLeft => MirDirection.Left, 
			MirDirection.Left => MirDirection.UpLeft, 
			MirDirection.UpLeft => MirDirection.Up, 
			_ => d, 
		};
	}

	public static MirDirection DirectionFromPoint(Point source, Point dest)
	{
		if (source.X < dest.X)
		{
			if (source.Y < dest.Y)
			{
				return MirDirection.DownRight;
			}
			if (source.Y > dest.Y)
			{
				return MirDirection.UpRight;
			}
			return MirDirection.Right;
		}
		if (source.X > dest.X)
		{
			if (source.Y < dest.Y)
			{
				return MirDirection.DownLeft;
			}
			if (source.Y > dest.Y)
			{
				return MirDirection.UpLeft;
			}
			return MirDirection.Left;
		}
		if (source.Y >= dest.Y)
		{
			return MirDirection.Up;
		}
		return MirDirection.Down;
	}

	public static MirDirection ShiftDirection(MirDirection dir, int i)
	{
		return (MirDirection)(((int)dir + i + 8) % 8);
	}

	public static Size Add(this Size p1, Size p2)
	{
		return new Size(p1.Width + p2.Width, p1.Height + p2.Height);
	}

	public static Size Add(this Size p1, int width, int height)
	{
		return new Size(p1.Width + width, p1.Height + height);
	}

	public static Point PointMove(Point p, MirDirection d, int i)
	{
		switch (d)
		{
		case MirDirection.Up:
			p.Offset(0, -i);
			break;
		case MirDirection.UpRight:
			p.Offset(i, -i);
			break;
		case MirDirection.Right:
			p.Offset(i, 0);
			break;
		case MirDirection.DownRight:
			p.Offset(i, i);
			break;
		case MirDirection.Down:
			p.Offset(0, i);
			break;
		case MirDirection.DownLeft:
			p.Offset(-i, i);
			break;
		case MirDirection.Left:
			p.Offset(-i, 0);
			break;
		case MirDirection.UpLeft:
			p.Offset(-i, -i);
			break;
		}
		return p;
	}

	public static Point Left(Point p, MirDirection d)
	{
		switch (d)
		{
		case MirDirection.Up:
			p.Offset(-1, 0);
			break;
		case MirDirection.UpRight:
			p.Offset(-1, -1);
			break;
		case MirDirection.Right:
			p.Offset(0, -1);
			break;
		case MirDirection.DownRight:
			p.Offset(1, -1);
			break;
		case MirDirection.Down:
			p.Offset(1, 0);
			break;
		case MirDirection.DownLeft:
			p.Offset(1, 1);
			break;
		case MirDirection.Left:
			p.Offset(0, 1);
			break;
		case MirDirection.UpLeft:
			p.Offset(-1, 1);
			break;
		}
		return p;
	}

	public static Point Right(Point p, MirDirection d)
	{
		switch (d)
		{
		case MirDirection.Up:
			p.Offset(1, 0);
			break;
		case MirDirection.UpRight:
			p.Offset(1, 1);
			break;
		case MirDirection.Right:
			p.Offset(0, 1);
			break;
		case MirDirection.DownRight:
			p.Offset(-1, 1);
			break;
		case MirDirection.Down:
			p.Offset(-1, 0);
			break;
		case MirDirection.DownLeft:
			p.Offset(-1, -1);
			break;
		case MirDirection.Left:
			p.Offset(0, -1);
			break;
		case MirDirection.UpLeft:
			p.Offset(1, -1);
			break;
		}
		return p;
	}

	public static int MaxDistance(Point p1, Point p2)
	{
		return Math.Max(Math.Abs(p1.X - p2.X), Math.Abs(p1.Y - p2.Y));
	}

	public static MirDirection ReverseDirection(MirDirection dir)
	{
		return dir switch
		{
			MirDirection.Up => MirDirection.Down, 
			MirDirection.UpRight => MirDirection.DownLeft, 
			MirDirection.Right => MirDirection.Left, 
			MirDirection.DownRight => MirDirection.UpLeft, 
			MirDirection.Down => MirDirection.Up, 
			MirDirection.DownLeft => MirDirection.UpRight, 
			MirDirection.Left => MirDirection.Right, 
			MirDirection.UpLeft => MirDirection.DownRight, 
			_ => dir, 
		};
	}

	public static ItemInfo GetRealItem(ItemInfo Origin, ushort Level, MirClass job, List<ItemInfo> ItemList)
	{
		if (Origin.ClassBased && Origin.LevelBased)
		{
			return GetClassAndLevelBasedItem(Origin, job, Level, ItemList);
		}
		if (Origin.ClassBased)
		{
			return GetClassBasedItem(Origin, job, ItemList);
		}
		if (Origin.LevelBased)
		{
			return GetLevelBasedItem(Origin, Level, ItemList);
		}
		return Origin;
	}

	public static ItemInfo GetLevelBasedItem(ItemInfo Origin, ushort level, List<ItemInfo> ItemList)
	{
		ItemInfo itemInfo = Origin;
		for (int i = 0; i < ItemList.Count; i++)
		{
			ItemInfo itemInfo2 = ItemList[i];
			if (itemInfo2.Name.StartsWith(Origin.Name) && itemInfo2.RequiredType == RequiredType.Level && itemInfo2.RequiredAmount <= level && itemInfo.RequiredAmount < itemInfo2.RequiredAmount && Origin.RequiredGender == itemInfo2.RequiredGender)
			{
				itemInfo = itemInfo2;
			}
		}
		return itemInfo;
	}

	public static ItemInfo GetClassBasedItem(ItemInfo Origin, MirClass job, List<ItemInfo> ItemList)
	{
		for (int i = 0; i < ItemList.Count; i++)
		{
			ItemInfo itemInfo = ItemList[i];
			if (itemInfo.Name.StartsWith(Origin.Name) && (int)itemInfo.RequiredClass == 1 << (int)job && Origin.RequiredGender == itemInfo.RequiredGender)
			{
				return itemInfo;
			}
		}
		return Origin;
	}

	public static ItemInfo GetClassAndLevelBasedItem(ItemInfo Origin, MirClass job, ushort level, List<ItemInfo> ItemList)
	{
		ItemInfo itemInfo = Origin;
		for (int i = 0; i < ItemList.Count; i++)
		{
			ItemInfo itemInfo2 = ItemList[i];
			if (itemInfo2.Name.StartsWith(Origin.Name) && (int)itemInfo2.RequiredClass == 1 << (int)job && itemInfo2.RequiredType == RequiredType.Level && itemInfo2.RequiredAmount <= level && itemInfo.RequiredAmount <= itemInfo2.RequiredAmount && Origin.RequiredGender == itemInfo2.RequiredGender)
			{
				itemInfo = itemInfo2;
			}
		}
		return itemInfo;
	}

	public static string StringOverLines(string line, int maxWordsPerLine, int maxLettersPerLine)
	{
		string text = string.Empty;
		string[] array = line.Split(' ');
		int num = 0;
		for (int i = 0; i < array.Length; i++)
		{
			num += array[i].Length + 1;
			text = text + array[i] + " ";
			if (i > 0 && i % maxWordsPerLine == 0 && num > maxLettersPerLine)
			{
				num = 0;
				text += "\r\n";
			}
		}
		return text;
	}

	public static IEnumerable<byte[]> SplitArray(byte[] value, int bufferLength)
	{
		int countOfArray = value.Length / bufferLength;
		if (value.Length % bufferLength > 0)
		{
			countOfArray++;
		}
		for (int i = 0; i < countOfArray; i++)
		{
			yield return value.Skip(i * bufferLength).Take(bufferLength).ToArray();
		}
	}

	public static byte[] CombineArray(List<byte[]> arrays)
	{
		byte[] array = new byte[arrays.Sum((byte[] x) => x.Length)];
		int num = 0;
		foreach (byte[] array2 in arrays)
		{
			Buffer.BlockCopy(array2, 0, array, num, array2.Length);
			num += array2.Length;
		}
		return array;
	}

	public static byte[] SerializeToBytes<T>(T item)
	{
		BinaryFormatter binaryFormatter = new BinaryFormatter();
		using MemoryStream memoryStream = new MemoryStream();
		binaryFormatter.Serialize(memoryStream, item);
		memoryStream.Seek(0L, SeekOrigin.Begin);
		return memoryStream.ToArray();
	}

	public static object DeserializeFromBytes(byte[] bytes)
	{
		BinaryFormatter binaryFormatter = new BinaryFormatter();
		using MemoryStream serializationStream = new MemoryStream(bytes);
		return binaryFormatter.Deserialize(serializationStream);
	}
}
