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

/// <summary>
/// A region of 2d integer coordinates.
/// You can "foreach" over an instance of this class, for every coordinate inside it.
/// </summary>
[Serializable]
public struct Rect2i : System.IEquatable<Rect2i>
{
	/// <summary>
	/// Min is inclusive, Max is exclusive.
	/// </summary>
	public Vector2i Min, Max;

	public Vector2i Size { get { return Max - Min; } }

	public Vector2i MinMaxCorner { get { return new Vector2i(Min.x, Max.y); } }
	public Vector2i MaxMinCorner { get { return new Vector2i(Max.x, Min.y); } }


	public Rect2i(Vector2i min, Vector2i maxExclusive)
	{
		Min = min;
		Max = maxExclusive;
	}


	public bool Contains(Vector2i v) { return v.x >= Min.x & v.y >= Min.y & v.x < Max.x & v.y < Max.y; }
	public bool Contains(Rect2i r)
	{
		//If this instance contains r's min and max, it must contain all of r.
		return Contains(r.Min) & Contains(new Vector2i(r.Max.x - 1, r.Max.y - 1));
	}
	public bool Touches(Rect2i r)
	{
		//If one rectangle is on the left or above the other, they do not touch.
		//Otherwise, they must be touching.
		return !(Min.x >= r.Max.x | Max.x <= r.Min.x |
				 Min.y >= r.Max.y | Max.y <= r.Min.y);
	}

	public override string ToString()
	{
		return "[" + Min + " - " + Max + ")";
	}
	public override int GetHashCode()
	{
		return unchecked((Min.x * 73856093) ^ (Min.y * 19349663) ^
						 (Max.x * 83492791) ^ (Max.y * 4256233));
	}
	public override bool Equals(object obj)
	{
		return (obj is Rect2i) && Equals((Rect2i)obj);
	}
	public bool Equals(Rect2i obj)
	{
		return Min == obj.Min & Max == obj.Max;
	}

	public static bool operator ==(Rect2i a, Rect2i b) { return a.Equals(b); }
	public static bool operator !=(Rect2i a, Rect2i b) { return !a.Equals(b); }

	public Vector2i.Iterator GetEnumerator() { return new Vector2i.Iterator(Min, Max); }
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

/// <summary>
/// A region of 2d integer coordinates.
/// You can "foreach" over an instance of this class, for every coordinate inside it.
/// </summary>
[Serializable]
public struct Rect3i : System.IEquatable<Rect3i>
{
	/// <summary>
	/// Min is inclusive, Max is exclusive.
	/// </summary>
	public Vector3i Min, Max;

	public Vector3i Size { get { return Max - Min; } }


	public Rect3i(Vector3i minInclusive, Vector3i maxExclusive)
	{
		Min = minInclusive;
		Max = maxExclusive;
	}
	public Rect3i(Rect2i range2D, int z)
		: this(new Vector3i(range2D.Min.x, range2D.Min.y, z),
			   new Vector3i(range2D.Max.x, range2D.Max.y, z + 1)) { }


	public bool ContainsX(int x) { return x >= Min.x & x < Max.x; }
	public bool ContainsY(int y) { return y >= Min.y & y < Max.y; }
	public bool ContainsZ(int z) { return z >= Min.z & z < Max.z; }
	public bool Contains(Vector3i v)
	{
		return ContainsX(v.x) & ContainsY(v.y) & ContainsZ(v.z);
	}
	public bool Contains(Rect3i r)
	{
		//If this instance contains r's min and max, it must contain all of r.
		return Contains(r.Min) & Contains(new Vector3i(r.Max.x - 1, r.Max.y - 1, r.Max.z - 1));
	}
	public bool Touches(Rect3i r)
	{
		//If one rectangle is on the left or above or behind the other, they do not touch.
		//Otherwise, they must be touching.
		return !(Min.x >= r.Max.x | Max.x <= r.Min.x |
				 Min.y >= r.Max.y | Max.y <= r.Min.y |
				 Min.z >= r.Max.z | Max.z <= r.Min.z);
	}

	public override string ToString()
	{
		return "[" + Min + " - " + Max + ")";
	}
	public override int GetHashCode()
	{
		return unchecked((Min.x * 73856093) ^ (Max.x * 19349663) ^
						 (Min.y * 83492791) ^ (Max.y * 4256233) ^
						 (Min.z * 15485867) ^ (Max.z * 32451169));
	}
	public override bool Equals(object obj)
	{
		return (obj is Rect3i) && Equals((Rect3i)obj);
	}
	public bool Equals(Rect3i obj)
	{
		return Min == obj.Min & Max == obj.Max;
	}

	public static bool operator ==(Rect3i a, Rect3i b) { return a.Equals(b); }
	public static bool operator !=(Rect3i a, Rect3i b) { return !a.Equals(b); }

	public Vector3i.Iterator GetEnumerator() { return new Vector3i.Iterator(Min, Max); }
}