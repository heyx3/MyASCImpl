using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


[RequireComponent(typeof(MeshFilter))]
public class ASCGenerator : MonoBehaviour
{
	/// <summary>
	/// The number of sample points for the algorithm is 2^Resolution + 1.
	/// </summary>
	public byte Resolution = 3;
	public int NSamples { get { return 2.Pow(Resolution) + 1; } }

	/// <summary>
	/// The generator will generate in a cube centered around the origin
	///     with the given size along each axis.
	/// </summary>
	public float AreaSize = 2.0f;

	/// <summary>
	/// The noise generation function.
	/// </summary>
	public GPUGraph.RuntimeGraph NoiseGraph;

	/// <summary>
	/// The threshold above which an area of noise is considered solid.
	/// </summary>
	public float Threshold = 0.5f;


	#region Generator stuff

	//Data structures are stored as fields so that they can be drawn as Gizmos for debugging.

	#region Sampling

	private float[,,] gen_samples;
	private bool[,,] gen_isSampleAbove;

	#endregion

	#region Ligns

	/// <summary>
	/// A 1D area in the samples array.
	/// </summary>
	private struct Dike
	{
		/// <summary>
		/// A 2-bit flag indicating how "simple" this dike is.
		/// 0 means nothing interesting is going on.
		/// 1 means the volume crosses from empty to solid.
		/// 2 means the volume crosses from solid to empty.
		/// 3 means the volume has 2 or more crossings, making it not "simple".
		/// </summary>
		public byte Simplicity;
		/// <summary>
		/// The index of the largest dike that is simple
		///     and has the same left edge as this one.
		/// This field is used to traverse across a line of samples (a.k.a. a "lign")
		///     using only the biggest simple dikes in that lign.
		/// </summary>
		public ushort SimplestIndex;

		public bool IsSimple { get { return Simplicity < 3; } }

		public Dike(byte simplicity, ushort simplestIndex)
		{
			Simplicity = simplicity;
			SimplestIndex = simplestIndex;
		}
	}

	/// <summary>
	/// A line of Dikes, covering all samples along that line.
	/// </summary>
	private struct Lign
	{
		/// <summary>
		/// A binary tree of the different-size dikes covering this lign, large to small.
		/// Stored in an array for performance reasons.
		/// The first value is the single dike covering this entire lign.
		/// The second and third values are the two dikes covering the first and last half of the lign,
		///     respectively.
		/// The last [NSamples-1] values are the tiny 2-wide dikes.
		/// </summary>
		public Dike[] Values { get; private set; }
		/// <summary>
		/// The number of samples this lign spans.
		/// </summary>
		public int NSamples { get; private set; }

		/// <summary>
		/// Creates a lign covering the given number of samples.
		/// </summary>
		/// <param name="nSamples">Must be one more than a power of 2!</param>
		public Lign(int nSamples)
		{
			NSamples = nSamples;
			UnityEngine.Assertions.Assert.IsTrue(Mathf.IsPowerOfTwo(nSamples - 1),
												 nSamples.ToString() + "isn't some 2^n + 1");

			//Calculate the number of elements in the binary tree
			//    of the different-size dikes covering this lign.
			int size = ((nSamples - 1) * 2) - 1;

			//Initialize the binary tree.
			Values = new Dike[size];
			for (int i = 0; i < Values.Length; ++i)
			    Values[i] = new Dike(0, ushort.MaxValue);
		}

		/// <summary>
		/// The index in the binary tree of the first dike of smallest size (i.e. size 2).
		/// This dike touches the left-most edge of the Lign, as does its parent, grandparent, etc.
		/// </summary>
		public int IndexOfFirstLeafNode { get { return Values.Length / 2; } }
		/// <summary>
		/// The index in the binary tree of the given node's parent.
		/// </summary>
		public int IndexOfParentNode(int nodeI) { return (nodeI - 1) / 2; }
		/// <summary>
		/// The index in the binary tree of the first child of the given node.
		/// The second child is the next index after that.
		/// </summary>
		public int IndexOfFirstChildNode(int nodeI) { return nodeI * 2; }
		/// <summary>
		/// Gets whether the given node is the first child of its parent.
		/// This means that it and its parent share the same left edge in the lign.
		/// The root node is considered a "first child".
		/// </summary>
		public bool IsFirstChild(int nodeI) { return nodeI == 0 | (nodeI % 2) == 1; }
		/// <summary>
		/// Gets the range of samples that the given dike sits between.
		/// </summary>
		public void GetEdges(int nodeI, out int sampleMin, out int sampleMax)
		{
			//TODO: Figure this out and use it for drawing.
			int firstNodeInRowI = 0;
			int span = NSamples;
			while (firstNodeInRowI < nodeI)
			{
				firstNodeInRowI = IndexOfFirstChildNode(firstNodeInRowI);
				span /= 2;
			}


		}
	}

	private Lign[,] gen_lignsAlongX, gen_lignsAlongY, gen_lignsAlongZ;

	/// <summary>
	/// Generates the ligns pointing along the given axis.
	/// Assumes the "gen_samples" and "gen_isSampleAbove" arrays have already been set up.
	/// </summary>
	private void Algo_CalcLigns(out Lign[,] ligns, int axis, int axis2, int axis3)
	{
		ligns = new Lign[gen_samples.GetLength(axis2), gen_samples.GetLength(axis3)];
		int nSamples = gen_samples.GetLength(axis);
		foreach (var lignPos2d in gen_lignsAlongX.AllIndices())
		{
			var lign = new Lign(nSamples);

			int firstLeafNodeI = lign.IndexOfFirstLeafNode;

			//Initialize the leaf node dikes.
			for (int leafI = firstLeafNodeI; leafI < lign.Values.Length; ++leafI)
			{
				Vector3i prevSampleI = new Vector3i();
				prevSampleI[axis] = leafI - firstLeafNodeI;
				prevSampleI[axis2] = lignPos2d.x;
				prevSampleI[axis3] = lignPos2d.y;

				Vector3i nextSampleI = prevSampleI;
				nextSampleI[axis] += 1;

				bool prevAbove = gen_isSampleAbove.Get(prevSampleI),
					 nextAbove = gen_isSampleAbove.Get(nextSampleI);

				//Record the "occupancy" value, indicating whether the dike
				//    crosses over a boundary: either b00, b01, or b10.
				var dike = lign.Values[leafI];
				if (prevAbove == nextAbove)
					dike.Simplicity = 0;
				else if (nextAbove)
					dike.Simplicity = 1;
				else
					dike.Simplicity = 2;

				lign.Values[leafI] = dike;
			}


			//Next, fill in the non-leaf nodes (meaning dikes larger than 2 samples wide).

			//Start by computing occupancy.
			//We have to iterate backwards, from the smaller dikes to the larger ones.
			int firstNodeI = firstLeafNodeI;
			int nValues = lign.NSamples - 1;
			while (firstNodeI > 0)
			{
				//Move one layer upwards.
				firstNodeI = lign.IndexOfParentNode(firstNodeI);
				nValues /= 2;

				//Compute occupancy.
				for (int dikeI = firstNodeI; dikeI < (firstNodeI + nValues); ++dikeI)
				{
					int childI = lign.IndexOfFirstChildNode(dikeI);
					Dike child1 = lign.Values[childI],
						 child2 = lign.Values[childI + 1];
					Dike parent = lign.Values[dikeI];

					parent.Simplicity = (byte)(child1.Simplicity | child2.Simplicity);
					lign.Values[dikeI] = parent;
				}
			}

			//Next, find a chain of the largest simple dikes covering the whole lign.
			//First, an edge-case: if the largest dike is simple.
			if (lign.Values[0].IsSimple)
			{
				for (int dikeI = 0; dikeI < lign.Values.Length; ++dikeI)
				{
					var dike = lign.Values[dikeI];
					dike.SimplestIndex = 0;
					lign.Values[dikeI] = dike;
				}
			}
			//Otherwise, iterate down the tree looking for simple dikes.
			//Whenever we find one, if it's the first one in its family,
			//    tell all its parents/grandparents/etc. that it's the biggest simple dike.
			else
			{
				firstNodeI = 0;
				nValues = 1;
				while (firstNodeI < firstLeafNodeI)
				{
					firstNodeI = lign.IndexOfFirstChildNode(firstNodeI);
					nValues *= 2;
					int maxNodeI = firstNodeI + nValues;

					//Check all the dikes in this layer.
					//Keep in mind we're specifically looking for the largest simple dike compared to
					//    all other simple dikes that share the same left edge.
					//Because we only care about dikes that share the same left edge,
					//    first and second children are handled differently.
					for (int dikeI = firstNodeI; dikeI < maxNodeI; dikeI += 2)
					{
						var dike = lign.Values[dikeI];
						var parentDike = lign.Values[lign.IndexOfParentNode(dikeI)];

						//If a first child is the largest simple dike, we have to update the parents.
						//Otherwise, if a parent already knows the largest simple dike, copy that info over.

						//If the parent is simple, it must already know about the largest simple dike.
						if (parentDike.IsSimple)
						{
							dike.SimplestIndex = parentDike.SimplestIndex;
						}
						//Otherwise, if this dike is simple, let all the parents know
						//    that we've found one.
						else if (dike.IsSimple)
						{
							dike.SimplestIndex = (ushort)dikeI;

							//Tell all parents about this dike.
							int parentNodeI = dikeI;
							while (parentNodeI > 0)
							{
								parentNodeI = lign.IndexOfParentNode(parentNodeI);
								var parentDike2 = lign.Values[parentNodeI];
								parentDike2.SimplestIndex = (ushort)dikeI;
								lign.Values[parentNodeI] = parentDike2;

								//If this node doesn't share its left edge with its parent,
								//    stop here.
								if (lign.IsFirstChild(parentNodeI))
									break;
							}
						}

						lign.Values[dikeI] = dike;
					}
					//Now check all the second children in this layer.
					for (int dikeI = firstNodeI + 1; dikeI < maxNodeI; dikeI += 2)
					{
						var dike = lign.Values[dikeI];

						//If a second child is a simple dike, it is always the largest simple dike
						//    for this left edge.
						if (dike.IsSimple)
							dike.SimplestIndex = (ushort)dikeI;
						lign.Values[dikeI] = dike;
					}
				}
			}

			gen_lignsAlongX.Set(lignPos2d, lign);
		}
	}

	#endregion

	#endregion

	//Runs the generation algorithm, spread across multiple frames.
	private System.Collections.IEnumerator Start()
	{
		//Get the number of sample points along each axis.
		int nSamples = NSamples;

		//Sample.
		gen_samples = new float[nSamples, nSamples, nSamples];
		NoiseGraph.GenerateToArray(gen_samples);

		yield return null;

		//Pre-compute the question of whether a sample is above the threshold.
		gen_isSampleAbove = new bool[nSamples, nSamples, nSamples];
		foreach (Vector3i i in gen_isSampleAbove.AllIndices())
			gen_isSampleAbove.Set(i, gen_samples.Get(i) > Threshold);

		yield return null;

		//Create the ligns.
		Algo_CalcLigns(out gen_lignsAlongX, 0, 1, 2);
		Algo_CalcLigns(out gen_lignsAlongY, 1, 0, 2);
		Algo_CalcLigns(out gen_lignsAlongZ, 2, 0, 1);

		yield return null;
	}


	#region Rendering stuff

	//Flags for rendering different stages of the algorithm.
	public bool rend_DoArea = true,
				rend_DoSamples = false,
				rend_DoLigns = false;

	public float rend_Area_Alpha = 0.2f;

	public float rend_Sample_Alpha = 0.05f;
	public Vector3i rend_Sample_MinI = Vector3i.Zero,
					rend_Sample_MaxI = new Vector3i(2, 2, 2);
	public bool rend_Sample_IgnoreBelowThreshold = false;

	public float rend_Lign_Alpha = 0.5f;

	private Vector3i.Iterator rend_SampleIterator
		{ get { return new Vector3i.Iterator(rend_Sample_MinI, rend_Sample_MaxI + 1); } }

	#endregion

	private void OnDrawGizmos()
	{
		if (rend_DoArea)
		{
			Gizmos.color = new Color(1.0f, 1.0f, 1.0f, rend_Area_Alpha);
			Gizmos.DrawCube(Vector3.zero, new Vector3(AreaSize, AreaSize, AreaSize));
		}

		//If samples haven't even been created yet, there's nothing else to render.
		if (gen_samples == null)
			return;

		//Compute some spacing data.
		float halfAreaSize = AreaSize * 0.5f;
		int nSamples = NSamples;
		float denom = 1.0f / (nSamples - 1);
		float sampleIncrement = AreaSize / (float)(nSamples - 1);


		if (rend_DoSamples)
		{
			foreach (var sampleI in rend_SampleIterator)
			{
				float value = gen_samples.Get(sampleI);
				if (rend_Sample_IgnoreBelowThreshold && value < Threshold)
					continue;

				Gizmos.color = new Color(value, value, value, rend_Sample_Alpha);

				Vector3 pos = new Vector3(-halfAreaSize + (sampleIncrement * sampleI.x),
										  -halfAreaSize + (sampleIncrement * sampleI.y),
										  -halfAreaSize + (sampleIncrement * sampleI.z));
				Gizmos.DrawSphere(pos, sampleIncrement * 0.5f * value);
			}
		}

		if (rend_DoLigns)
		{
			foreach (var sampleI in rend_SampleIterator)
			{
				//If this index is the beginning of a lign, render that lign.
				if (sampleI.x == 0)
				{
					var lign = gen_lignsAlongX[sampleI.y, sampleI.z];

					//Render all the largest simple dikes needed to cover the lign.
					int dikeI = 0;
					int sampleMin,
						sampleMax = -1;
					while (sampleMax < nSamples - 1)
					{
						//Get the samples this dike spans.
						dikeI = lign.Values[dikeI].SimplestIndex;
						lign.GetEdges(dikeI, out sampleMin, out sampleMax);

						//Calculate the world-space position of the dike.
						const float border = 0.1f;
						float sampleMinPosX = -halfAreaSize + ((sampleMin + border) * sampleIncrement),
							  sampleMaxPosX = -halfAreaSize + ((sampleMax - border) * sampleIncrement);
						Vector2 samplePosYZ = new Vector2(-halfAreaSize + (sampleI.y * sampleIncrement),
														  -halfAreaSize + (sampleI.z * sampleIncrement));

						//Draw the dike.
						Gizmos.color = new Color(1.0f, 0.0f, 0.0f, rend_Lign_Alpha);
						Gizmos.DrawLine(new Vector3(sampleMinPosX, samplePosYZ.x, samplePosYZ.y),
										new Vector3(sampleMaxPosX, samplePosYZ.x, samplePosYZ.y));
					}
				}
				if (sampleI.y == 0)
				{
					var lign = gen_lignsAlongY[sampleI.x, sampleI.z];

					//Render all the largest simple dikes needed to cover the lign.
					int dikeI = 0;
					int sampleMin,
						sampleMax = -1;
					while (sampleMax < nSamples - 1)
					{
						//Get the samples this dike spans.
						dikeI = lign.Values[dikeI].SimplestIndex;
						lign.GetEdges(dikeI, out sampleMin, out sampleMax);

						//Calculate the world-space position of the dike.
						const float border = 0.1f;
						float sampleMinPosY = -halfAreaSize + ((sampleMin + border) * sampleIncrement),
							  sampleMaxPosY = -halfAreaSize + ((sampleMax - border) * sampleIncrement);
						Vector2 samplePosXZ = new Vector2(-halfAreaSize + (sampleI.x * sampleIncrement),
														  -halfAreaSize + (sampleI.z * sampleIncrement));

						//Draw the dike.
						Gizmos.color = new Color(0.0f, 1.0f, 0.0f, rend_Lign_Alpha);
						Gizmos.DrawLine(new Vector3(samplePosXZ.x, sampleMinPosY, samplePosXZ.y),
										new Vector3(samplePosXZ.x, sampleMaxPosY, samplePosXZ.y));
					}
				}
				if (sampleI.z == 0)
				{
					var lign = gen_lignsAlongZ[sampleI.x, sampleI.y];

					//Render all the largest simple dikes needed to cover the lign.
					int dikeI = 0;
					int sampleMin,
						sampleMax = -1;
					while (sampleMax < nSamples - 1)
					{
						//Get the samples this dike spans.
						dikeI = lign.Values[dikeI].SimplestIndex;
						lign.GetEdges(dikeI, out sampleMin, out sampleMax);

						//Calculate the world-space position of the dike.
						const float border = 0.1f;
						float sampleMinPosZ = -halfAreaSize + ((sampleMin + border) * sampleIncrement),
							  sampleMaxPosZ = -halfAreaSize + ((sampleMax - border) * sampleIncrement);
						Vector2 samplePosXY = new Vector2(-halfAreaSize + (sampleI.x * sampleIncrement),
														  -halfAreaSize + (sampleI.y * sampleIncrement));

						//Draw the dike.
						Gizmos.color = new Color(0.0f, 1.0f, 0.0f, rend_Lign_Alpha);
						Gizmos.DrawLine(new Vector3(samplePosXY.x, samplePosXY.y, sampleMinPosZ),
										new Vector3(samplePosXY.x, samplePosXY.y, sampleMaxPosZ));
					}
				}
			}
		}
	}
}