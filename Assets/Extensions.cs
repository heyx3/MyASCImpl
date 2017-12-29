using Array = System.Array;


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