using System;
using System.Collections.Generic;

namespace Shared.Extensions;

public static class HelperExtensions
{
	private static Random rng = new Random();

	public static T ValueOrDefault<T>(this object value)
	{
		if (value == null || value.ToString() == "")
		{
			return default(T);
		}
		return (T)Convert.ChangeType(value, typeof(T));
	}

	public static void Shuffle<T>(this IList<T> list)
	{
		int num = list.Count;
		while (num > 1)
		{
			num--;
			int index = rng.Next(num + 1);
			T value = list[index];
			list[index] = list[num];
			list[num] = value;
		}
	}
}
