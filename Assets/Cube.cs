using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


/// <summary>
/// The 3D version of a Rect.
/// </summary>
[Serializable]
public struct Cube
{
	public Vector3 Min, Size;

	public Vector3 Max { get { return Min + Size; } }
	public Vector3 Extents { get { return Size * 0.5f; } }
	public Vector3 Center {  get { return Min + Extents; } }


	public Cube(Vector3 min, Vector3 size)
	{
		Min = min;
		Size = size;
	}


	public bool Contains(Vector3 v)
	{
		var max = Max;
		return v.x >= Min.x & v.y >= Min.y & v.z >= Min.z &
			   v.x <= max.x & v.y <= max.y & v.z <= max.z;
	}
}