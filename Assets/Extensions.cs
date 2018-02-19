using UnityEngine;

using Array = System.Array;


public static class MyAssert
{
	[System.Diagnostics.Conditional("UNITY_ASSERTIONS")]
	public static void IsTrue(System.Func<bool> runTest, string message = null)
	{
		bool result = runTest();

		if (message == null)
			UnityEngine.Assertions.Assert.IsTrue(result);
		else
			UnityEngine.Assertions.Assert.IsTrue(result, message);
	}

	[System.Diagnostics.Conditional("UNITY_ASSERTIONS")]
	public static void IsTrue(System.Func<string> runTest)
	{
		var result = runTest();

		if (result != null)
			UnityEngine.Assertions.Assert.IsTrue(false, result);
	}

}

public static class MyExtensions
{
	/// <summary>
	/// Wraps this integer around the range [0, max).
	/// </summary>
	public static int Wrap(this int i, int max)
	{
		while (i < 0)
			i += max;
		return i % max;
	}
	/// <summary>
	/// Raises this integer to the given power.
	/// </summary>
	public static int Pow(this int baseI, uint expI)
	{
		int val = 1;
		for (uint i = 0; i < expI; ++i)
			val *= baseI;
		return val;
	}
}
public static class Array2DExtensions
{
    public static T Get<T>(this T[,] array, Vector2i pos)
    {
        return array[pos.x, pos.y];
    }
    public static void Set<T>(this T[,] array, Vector2i pos, T newVal)
    {
        array[pos.x, pos.y] = newVal;
    }

    public static bool IsInRange<T>(this T[,] array, Vector2i pos)
    {
        return pos.x >= 0 & pos.y >= 0 &
               pos.x < array.GetLength(0) & pos.y < array.GetLength(1);
    }

    public static int SizeX(this Array array) { return array.GetLength(0); }
    public static int SizeY(this Array array) { return array.GetLength(1); }
    public static Vector2i SizeXY<T>(this T[,] array) { return new Vector2i(array.SizeX(), array.SizeY()); }

    public static Vector2i.Iterator AllIndices<T>(this T[,] array) { return new Vector2i.Iterator(array.SizeXY()); }
	public static System.Collections.Generic.IEnumerable<T> AllItems<T>(this T[,] array)
	{
		foreach (var i in array.AllIndices())
			yield return array.Get(i);
	}
}
public static class Array3DExtensions
{
	public static T Get<T>(this T[,,] array, Vector3i pos)
	{
		return array[pos.x, pos.y, pos.z];
	}
	public static void Set<T>(this T[,,] array, Vector3i pos, T newVal)
	{
		array[pos.x, pos.y, pos.z] = newVal;
	}

	public static bool IsInRange<T>(this T[,,] array, Vector3i pos)
	{
		return pos.x >= 0 & pos.y >= 0 & pos.z >= 0 &
			   pos.x < array.GetLength(0) & pos.y < array.GetLength(1) & pos.z < array.GetLength(2);
	}

	public static int SizeZ(this Array array) { return array.GetLength(2); }
	public static Vector3i SizeXYZ<T>(this T[,,] array) { return new Vector3i(array.SizeX(), array.SizeY(), array.SizeZ()); }

	public static Vector3i.Iterator AllIndices<T>(this T[,,] array) { return new Vector3i.Iterator(array.SizeXYZ()); }
	public static Vector3i.Iterator AllIndicesX<T>(this T[,,] array, int x) { return new Vector3i.Iterator(new Vector3i(x, 0, 0),
																										   new Vector3i(x + 1, array.SizeY(), array.SizeZ())); }
	public static Vector3i.Iterator AllIndicesY<T>(this T[,,] array, int y) { return new Vector3i.Iterator(new Vector3i(0, y, 0),
																										   new Vector3i(array.SizeX(), y + 1, array.SizeZ())); }
	public static Vector3i.Iterator AllIndicesZ<T>(this T[,,] array, int z) { return new Vector3i.Iterator(new Vector3i(0, 0, z),
																										   new Vector3i(array.SizeX(), array.SizeY(), z + 1)); }
}

public static class VectorExtensions
{
	public static float Get(this Vector2 v, int i)
	{
		switch (i)
		{
			case 0: return v.x;
			case 1: return v.y;
			default: throw new System.NotImplementedException(i.ToString());
		}
	}
	public static Vector2 Set(this Vector2 v, int i, float val)
	{
		switch (i)
		{
			case 0: v.x = val; break;
			case 1: v.y = val; break;
			default: throw new System.NotImplementedException(i.ToString());
		}
		return v;
	}

	public static float Get(this Vector3 v, int i)
	{
		switch (i)
		{
			case 0: return v.x;
			case 1: return v.y;
			case 2: return v.z;
			default: throw new System.NotImplementedException(i.ToString());
		}
	}
	public static Vector3 Set(this Vector3 v, int i, float val)
	{
		switch (i)
		{
			case 0: v.x = val; break;
			case 1: v.y = val; break;
			case 2: v.z = val; break;
			default: throw new System.NotImplementedException(i.ToString());
		}
		return v;
	}
}