using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


[Serializable]
public struct Vector2i : System.IEquatable<Vector2i>
{
	public static Vector2i Zero { get { return new Vector2i(0, 0); } }


	public int x, y;


	public Vector2i(int _x, int _y) { x = _x; y = _y; }


	public Vector2i LessX { get { return new Vector2i(x - 1, y); } }
	public Vector2i LessY { get { return new Vector2i(x, y - 1); } }
	public Vector2i MoreX { get { return new Vector2i(x + 1, y); } }
	public Vector2i MoreY { get { return new Vector2i(x, y + 1); } }


	public static Vector2i operator +(Vector2i a, Vector2i b) { return new Vector2i(a.x + b.x, a.y + b.y); }
	public static Vector2i operator +(Vector2i a, int b) { return new Vector2i(a.x + b, a.y + b); }
	public static Vector2i operator -(Vector2i a, Vector2i b) { return new Vector2i(a.x - b.x, a.y - b.y); }
	public static Vector2i operator -(Vector2i a, int b) { return new Vector2i(a.x - b, a.y - b); }
	public static Vector2i operator *(Vector2i a, int b) { return new Vector2i(a.x * b, a.y * b); }
	public static Vector2i operator /(Vector2i a, int b) { return new Vector2i(a.x / b, a.y / b); }
	public static Vector2i operator -(Vector2i a) { return new Vector2i(-a.x, -a.y); }

	public int this[int i]
	{
		get
		{
			switch (i)
			{
				case 0: return x;
				case 1: return y;
				default: throw new IndexOutOfRangeException(i.ToString());
			}
		}
		set
		{
			switch (i)
			{
				case 0: x = value; break;
				case 1: y = value; break;
				default: throw new IndexOutOfRangeException(i.ToString());
			}
		}
	}

	public static bool operator ==(Vector2i a, Vector2i b) { return a.x == b.x && a.y == b.y; }
	public static bool operator !=(Vector2i a, Vector2i b) { return !(a == b); }

	public override string ToString()
	{
		return "{" + x + ", " + y + "}";
	}
	public override int GetHashCode()
	{
		return unchecked((x * 73856093) ^ (y * 19349663));
	}
	public override bool Equals(object obj)
	{
		return (obj is Vector2i) && ((Vector2i)obj) == this;
	}
	public bool Equals(Vector2i v) { return v == this; }


	#region Iterator definition
	public struct Iterator
	{
		public Vector2i MinInclusive { get { return minInclusive; } }
		public Vector2i MaxExclusive { get { return maxExclusive; } }
		public Vector2i Current { get { return current; } }

		private Vector2i minInclusive, maxExclusive, current;

		public Iterator(Vector2i maxExclusive) : this(Vector2i.Zero, maxExclusive) { }
		public Iterator(Vector2i _minInclusive, Vector2i _maxExclusive)
		{
			minInclusive = _minInclusive;
			maxExclusive = _maxExclusive;

			current = Vector2i.Zero; //Just to make the compiler shut up
			Reset();
		}

		public bool MoveNext()
		{
			current.x += 1;
			if (current.x >= maxExclusive.x)
				current = new Vector2i(minInclusive.x, current.y + 1);

			return (current.y < maxExclusive.y);
		}
		public void Reset() { current = new Vector2i(minInclusive.x - 1, minInclusive.y); }
		public void Dispose() { }

		public Iterator GetEnumerator() { return this; }
	}
	#endregion
}


[Serializable]
public struct Vector3i : System.IEquatable<Vector3i>
{
	public static Vector3i Zero { get { return new Vector3i(0, 0, 0); } }


	public int x, y, z;


	public Vector3i(int _x, int _y, int _z) { x = _x; y = _y; z = _z; }


	public Vector3i LessX { get { return new Vector3i(x - 1, y, z); } }
	public Vector3i LessY { get { return new Vector3i(x, y - 1, z); } }
	public Vector3i LessZ { get { return new Vector3i(x, y, z - 1); } }
	public Vector3i MoreX { get { return new Vector3i(x + 1, y, z); } }
	public Vector3i MoreY { get { return new Vector3i(x, y + 1, z); } }
	public Vector3i MoreZ { get { return new Vector3i(x, y, z + 1); } }


	public static Vector3i operator +(Vector3i a, Vector3i b) { return new Vector3i(a.x + b.x, a.y + b.y, a.z + b.z); }
	public static Vector3i operator +(Vector3i a, int b) { return new Vector3i(a.x + b, a.y + b, a.z + b); }
	public static Vector3i operator -(Vector3i a, Vector3i b) { return new Vector3i(a.x - b.x, a.y - b.y, a.z - b.z); }
	public static Vector3i operator -(Vector3i a, int b) { return new Vector3i(a.x - b, a.y - b, a.z - b); }
	public static Vector3i operator *(Vector3i a, int b) { return new Vector3i(a.x * b, a.y * b, a.z * b); }
	public static Vector3i operator /(Vector3i a, int b) { return new Vector3i(a.x / b, a.y / b, a.z / b); }
	public static Vector3i operator -(Vector3i a) { return new Vector3i(-a.x, -a.y, -a.z); }

	public int this[int i]
	{
		get
		{
			switch (i)
			{
				case 0: return x;
				case 1: return y;
				case 2: return z;
				default: throw new IndexOutOfRangeException(i.ToString());
			}
		}
		set
		{
			switch (i)
			{
				case 0: x = value; break;
				case 1: y = value; break;
				case 2: z = value; break;
				default: throw new IndexOutOfRangeException(i.ToString());
			}
		}
	}

	public static bool operator ==(Vector3i a, Vector3i b) { return a.x == b.x && a.y == b.y && a.z == b.z; }
	public static bool operator !=(Vector3i a, Vector3i b) { return !(a == b); }

	public override string ToString()
	{
		return "{" + x + ", " + y + ", " + z + "}";
	}
	public override int GetHashCode()
	{
		return unchecked((x * 73856093) ^ (y * 19349663) ^ (z * 83492791));
	}
	public override bool Equals(object obj)
	{
		return (obj is Vector3i) && ((Vector3i)obj) == this;
	}
	public bool Equals(Vector3i v) { return v == this; }


	#region Iterator definition
	public struct Iterator
	{
		public Vector3i MinInclusive { get { return minInclusive; } }
		public Vector3i MaxExclusive { get { return maxExclusive; } }
		public Vector3i Current { get { return current; } }

		private Vector3i minInclusive, maxExclusive, current;

		public Iterator(Vector3i maxExclusive) : this(Vector3i.Zero, maxExclusive) { }
		public Iterator(Vector3i _minInclusive, Vector3i _maxExclusive)
		{
			minInclusive = _minInclusive;
			maxExclusive = _maxExclusive;

			current = Vector3i.Zero; //Just to make the compiler shut up
			Reset();
		}

		public bool MoveNext()
		{
			current.x += 1;
			if (current.x >= maxExclusive.x)
			{
				current = new Vector3i(minInclusive.x, current.y + 1, current.z);
				if (current.y >= maxExclusive.y)
				{
					current = new Vector3i(minInclusive.x, minInclusive.y, current.z + 1);
				}
			}

			return (current.z < maxExclusive.z);
		}
		public void Reset() { current = minInclusive.LessX; }
		public void Dispose() { }

		public Iterator GetEnumerator() { return this; }
	}
	#endregion
}