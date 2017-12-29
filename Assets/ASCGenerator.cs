using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


[RequireComponent(typeof(MeshFilter))]
public class ASCGenerator : MonoBehaviour
{
	/// <summary>
	/// The number of sample points for the algorithm is 2^Size + 1.
	/// </summary>
	public byte Resolution = 5;

	/// <summary>
	/// The region in world-space where this generator covers.
	/// </summary>
	public Cube Area = new Cube(-Vector3.one, 2.0f * Vector3.one);

	/// <summary>
	/// The noise generation function.
	/// </summary>
	public GPUGraph.RuntimeGraph NoiseGraph;

	/// <summary>
	/// The threshold above which an area of noise is considered solid.
	/// </summary>
	public float Threshold = 0.5f;


	//Generator data structures:
	private float[,,] samples;
	private bool[,,] isSampleAbove;
	private struct Dike
	{
		public byte Occupancy;
		public ushort SimpleIndex;
		public Dike(byte occupancy, ushort simpleIndex)
		{
			Occupancy = occupancy;
			SimpleIndex = simpleIndex;
		}
	}
	private struct Lign
	{
		/// <summary>
		/// A binary tree of the different-size dikes covering this lign,
		///     arranged from min index to max, then largest to smallest.
		/// </summary>
		public Dike[] Values;
		public Lign(int size)
		{
			Values = new Dike[size];
			for (int i = 0; i < Values.Length; ++i)
			    Values[i] = new Dike(0, ushort.MaxValue);
		}
	}
	private Lign[,] lignsAlongX, lignsAlongY;


	private System.Collections.IEnumerator Start()
	{
		//Get the number of sample points along each axis.
		int nSamples = 2.Pow(Resolution) + 1;

		//Sample.
		samples = new float[nSamples, nSamples, nSamples];
		NoiseGraph.GenerateToArray(samples);

		yield return null;

		//Pre-compute the question of whether a sample is above the threshold.
		isSampleAbove = new bool[nSamples, nSamples, nSamples];
		foreach (Vector3i i in isSampleAbove.AllIndices())
			isSampleAbove.Set(i, samples.Get(i) > Threshold);

		yield return null;

		//Create the ligns.
		lignsAlongX = new Lign[samples.SizeY(), samples.SizeZ()];
		lignsAlongY = new Lign[samples.SizeX(), samples.SizeZ()];
		int lignXSize = ((samples.SizeX() - 1) * 2) - 1,
			lignYSize = ((samples.SizeY() - 1) * 2) - 1;
		foreach (var lignYZPos in lignsAlongX.AllIndices())
		{
			var lign = new Lign(lignXSize);

			//Get the index of the first leaf node in the lign's dike array
			//    ("leaf node" meaning a tiniest-possible dike spanning 2 samples).
			int leafNodeStartI = samples.SizeX() - 1;

			//For each "leaf node" dike, see whether it crosses the boundary from "solid" to "not solid".
			for (int leafI = leafNodeStartI; leafI < lign.Values.Length; ++leafI)
			{
				var prevSampleI = new Vector3i(leafI - leafNodeStartI, lignYZPos.x, lignYZPos.y);
				var nextSampleI = prevSampleI.MoreX;

				bool prevAbove = isSampleAbove.Get(prevSampleI),
					 nextAbove = isSampleAbove.Get(nextSampleI);

				//Record the "occupancy" value, indicating whether the dike
				//    crosses over a boundary: either b00, b01, or b10.
				var dike = lign.Values[leafI];
				if (prevAbove == nextAbove)
					dike.Occupancy = 0;
				else if (nextAbove)
					dike.Occupancy = 1;
				else
					dike.Occupancy = 2;
				lign.Values[leafI] = dike;
			}

			//Next, fill in the non-leaf nodes (meaning dikes larger than 2 samples wide).
			//TODO: Finish.

			lignsAlongX.Set(lignYZPos, lign);
		}
		foreach (var lignXZPos in lignsAlongY.AllIndices())
		{
			var lign = new Lign(lignYSize);

			//TODO: Implement like above.

			lignsAlongY.Set(lignXZPos, lign);
		}

		yield return null;
	}

	private void OnDrawGizmos()
	{
		//Display the generator's status.

		//Draw the area being generated.
		Gizmos.color = new Color(1.0f, 1.0f, 1.0f, 0.35f);
		Gizmos.DrawCube(Area.Center, Area.Size);
	}
}