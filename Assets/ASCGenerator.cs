using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


//A Lign is a binary tree of the dikes that can span it.
using Lign = ASCGenerator.SpanTree<ASCGenerator.Dike>;

//A Strip is a binary tree of plots, which are two dikes laid on top of each other.
//Unlike the dikes, we don't need to store a "simplicity" value.
using Strip = ASCGenerator.SpanTree<ushort>;


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

	/// <summary>
	/// An optimized binary tree of "spans".
	/// A "span" covers some range of "samples".
	/// The root of the tree is a "span" covering all samples.
	///	The root's 2 children are spans that each cover half of the samples, and so on.
	/// </summary>
	public struct SpanTree<Data>
	{
		/// <summary>
		/// The binary tree, stored as an array for best performance.
		/// The first value is the single span covering all samples.
		/// The second and third values are the two spans
		///     covering the first and last half of the samples, respectively.
		/// The last [NSamples-1] values are the tiny 2-wide spans in order.
		/// </summary>
		public Data[] Values { get; private set; }
		/// <summary>
		/// The number of samples this instance spans.
		/// </summary>
		public int NSamples { get; private set; }

		/// <summary>
		/// Creates a tree of "spans" for the given number of samples.
		/// </summary>
		/// <param name="nSamples">Must be one more than a power of 2!</param>
		public SpanTree(int nSamples)
		{
			NSamples = nSamples;
			UnityEngine.Assertions.Assert.IsTrue(Mathf.IsPowerOfTwo(nSamples - 1),
												 nSamples.ToString() + "isn't some 2^n + 1");

			//Calculate the number of elements in the binary tree.
			int size = ((nSamples - 1) * 2) - 1;
			Values = new Data[size];
		}

		//Below are utility methods that tell you about specific nodes based on their index.
		/// <summary>
		/// The index in the binary tree of the first span of smallest size (i.e. size 2).
		/// This span touches the left-most edge of the sample range,
		///     as does its parent, grandparent, etc. all the way back to the root.
		/// </summary>
		public int IndexOfFirstLeafNode { get { return Values.Length / 2; } }
		/// <summary>
		/// The index in the binary tree of the given node's parent.
		/// </summary>
		public int IndexOfParentNode(int spanI) { return (spanI - 1) / 2; }
		/// <summary>
		/// The index in the binary tree of the first child of the given node.
		/// The second child is the next index after that.
		/// </summary>
		public int IndexOfFirstChildNode(int spanI) { return (spanI * 2) + 1; }
		/// <summary>
		/// Gets whether the given node is the first child of its parent.
		/// This means that it and its parent share the same left edge of their spans.
		/// The root node is considered a "first child".
		/// </summary>
		public bool IsFirstChild(int spanI) { return spanI == 0 | (spanI % 2) == 1; }
		/// <summary>
		/// Gets the index of the first node in the same layer of the tree as the given node.
		/// </summary>
		public int IndexOfFirstInLayer(int spanI) { return (Mathf.NextPowerOfTwo(spanI + 2) / 2) - 1; }
		/// <summary>
		/// Gets whether the given span reaches to the end of the samples.
		/// </summary>
		public bool IsLastInLayer(int spanI) { return Mathf.IsPowerOfTwo(spanI + 2); }
		/// <summary>
		/// Gets the range of samples that the given span sits between.
		/// </summary>
		public void GetEdges(int spanI, out int sampleMin, out int sampleMax)
		{
			int firstNodeI = IndexOfFirstInLayer(spanI),
				nSamples = (NSamples - 1) / (firstNodeI + 1);

			sampleMin = (spanI - firstNodeI) * nSamples;
			sampleMax = sampleMin + nSamples;
		}
		/// <summary>
		/// Gets the largest span that starts at exactly the given min sample
		///     and ends no later than the given max sample.
		/// </summary>
		public int GetLargestSpan(int minSampleI, int maxSampleI)
		{
			//Shortcut: if minSample is odd, it's longest span will be the leaf node.
			if (minSampleI % 2 == 1)
				return IndexOfFirstLeafNode + minSampleI;

			//Start at the root and work downward until we find it.
			int spanI = 0,
				spanStart = 0,
				spanEnd = NSamples - 1,
				spanMid = spanEnd / 2;
			while (spanStart != minSampleI)
			{
				if (spanMid <= minSampleI)
				{
					spanI = IndexOfFirstChildNode(spanI) + 1;
					spanStart = spanMid;
				}
				else
				{
					spanI = IndexOfFirstChildNode(spanI);
					spanEnd = spanMid;
				}
			}
			return spanI;
		}
	}


	#region Sampling

	private float[,,] gen_samples;
	private bool[,,] gen_isSampleAbove;

	#endregion

	#region Ligns

	/// <summary>
	/// A 1D area in the samples array.
	/// </summary>
	public struct Dike
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

		public override string ToString()
		{
			return "[" + Simplicity + ":" + SimplestIndex + "]";
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
		foreach (var lignPos2d in ligns.AllIndices())
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
					Dike parent = lign.Values[dikeI];

					int childI = lign.IndexOfFirstChildNode(dikeI);
					Dike child1 = lign.Values[childI],
						 child2 = lign.Values[childI + 1];

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
								if (!lign.IsFirstChild(parentNodeI))
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

			ligns.Set(lignPos2d, lign);
		}
	}

	#endregion

	#region Strip

	private Strip[,] gen_stripsAlongXY;

	private void Algo_CalcStrips(out Strip[,] strips, Lign[,] ligns)
	{
		strips = new Strip[gen_samples.GetLength(1) - 1,
						   gen_samples.GetLength(2)];
		foreach (var stripPos2D in strips.AllIndices())
		{
			var strip = new Strip(gen_samples.GetLength(0));

			//Each lign has its own path through the largest simple dikes.
			//Merge these largest simple dikes together into largest simple "plots"
			//    by always picking the smaller of the two dikes.
			Lign lign1 = ligns.Get(stripPos2D),
				 lign2 = ligns.Get(stripPos2D.MoreX);

			for (int i = 0; i < strip.Values.Length; ++i)
			{
				strip.Values[i] = Math.Max(lign1.Values[i].SimplestIndex,
										   lign2.Values[i].SimplestIndex);
			}

			strips.Set(stripPos2D, strip);
		}
	}

	#endregion

	#region Padis

	List<Rect2i>[] gen_padiRectsPerXYFarm;
	List<Rect2i> gen_newPadis;

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
		yield return null;
		Algo_CalcLigns(out gen_lignsAlongY, 1, 0, 2);
		yield return null;
		Algo_CalcLigns(out gen_lignsAlongZ, 2, 0, 1);

		yield return null;

		//Create strips.
		Algo_CalcStrips(out gen_stripsAlongXY, gen_lignsAlongX);

		yield return null;

		//Create padis.
		//TODO: Instead of this stuff with rectangle lists, just use the combination of X and Y SpanTrees to implicitly define the rectangles.
		gen_padiRectsPerXYFarm = new List<Rect2i>[gen_samples.SizeZ()];
		gen_newPadis = new List<Rect2i>(gen_samples.SizeX());
		for (int z = 0; z < gen_padiRectsPerXYFarm.Length; ++z)
		{
			//Allocate the list with a good first estimate of how many elements it will have.
			var padis = new List<Rect2i>(gen_samples.SizeX() * gen_samples.SizeY() / 2);
			gen_padiRectsPerXYFarm[z] = padis;
			//For every strip in the farm...
			for (int stripI = 0; stripI < gen_stripsAlongXY.SizeX(); ++stripI)
			{
				var strip = gen_stripsAlongXY[stripI, z];

				//For every length-maximal simple plot in the strip...
				ushort plotI = 0;
				while (true)
				{
					plotI = strip.Values[plotI];

					//Start the padi with just the plot's rectangle.
					int minX, maxX;
					strip.GetEdges(plotI, out minX, out maxX);
					Rect2i padi = new Rect2i(new Vector2i(minX, stripI),
											 new Vector2i(maxX, stripI + 1));

					//Move downward through neighbor strips of the same size and add them to the padi.
					for (int neighborStripI = stripI + 1;
						 neighborStripI < gen_stripsAlongXY.SizeX();
						 ++neighborStripI)
					{
						var neighborStrip = gen_stripsAlongXY[neighborStripI, z];
						//If this plot on the neighbor strip is the length-maximal simple plot,
						//    add it to the padi.
						if (neighborStrip.Values[plotI] == plotI)
							padi = new Rect2i(padi.Min, new Vector2i(maxX, neighborStripI + 1));
					}

					//Split up the padi so it conforms to the binary tree structure.
					gen_newPadis.Clear();
					gen_newPadis.Add(padi);
					for (int newPadiI = 0; newPadiI < gen_newPadis.Count; ++newPadiI)
					{
						var newPadi = gen_newPadis[newPadiI];

						//For each vertical lign, see whether it breaks up this padi.
						for (int lignI = newPadi.Min.x; lignI <= newPadi.Max.x; ++lignI)
						{
							var lign = gen_lignsAlongY[lignI, z];

							//Get the largest dike that starts at the beginning of the padi
							//    and doesn't pass over the end of it.
							int dikeI = lign.GetLargestSpan(newPadi.Min.y, newPadi.Max.y);
							int dikeMin, dikeMax;
							lign.GetEdges(dikeI, out dikeMin, out dikeMax);

							//If the dike isn't simple, split the padi.
							bool isSimple = lign.Values[dikeI].SimplestIndex > dikeI;
							if (!isSimple)
							{
								int splitPoint = (dikeMin + dikeMax) / 2;
								Rect2i newPadi1 = new Rect2i(newPadi.Min,
															 new Vector2i(newPadi.Max.x, splitPoint)),
									   newPadi2 = new Rect2i(new Vector2i(newPadi.Min.x, splitPoint),
															 newPadi.Max);
							}
							//Otherwise, if the padi sticks out past the end of the dike, split the padi.
							else if (dikeMax > newPadi.Max.y)
							{
								Rect2i newPadi1 = new Rect2i(newPadi.Min,
															 new Vector2i(newPadi.Max.x, dikeMax)),
									   newPadi2 = new Rect2i(new Vector2i(newPadi.Min.x, dikeMax),
															 newPadi.Max);

								//We know the first part of the new padi fits in a simple dike,
								//    so just replace the current padi with it.
								gen_newPadis[newPadiI] = newPadi1;
								//The second part of the new padi goes at the end of the list
								//    so that this algorithm will check it.
								gen_newPadis.Add(newPadi2);
								break;
							}
						}
					}

					//For each padi, try to add it to the list.
					//TODO: Would my BVH structure work better here?
					foreach (var newPadi in gen_newPadis)
					{
						bool shouldDrop = false;

						//See if it interacts with other padis.
						for (int oldPadiI = 0; oldPadiI < nCurrentPadis; ++oldPadiI)
						{
							var oldPadi = padis[oldPadiI];
							//If a current padi totally contains this one,
							//    then this one isn't necessary.
							if (oldPadi.Contains(newPadi))
							{
								shouldDrop = true;
								break;
							}
							//If this padi totally contains a current one,
							//    then the current one isn't necessary.
							else if (newPadi.Contains(oldPadi))
							{
								padis.RemoveAt(oldPadiI);
								oldPadiI -= 1;
								continue;
							}
							//If the two padis just touch a bit, they need to be clipped.
							else if (newPadi.Touches(oldPadi))
							{
								TODO: Implement.
							}
						}

						if (!shouldDrop)
							padis.Add(newPadi);
					}


					//Exit the loop if we've reached the end of the strip.
					if (strip.IsLastInLayer(plotI))
						break;
				}
			}
		}
	}


	#region Rendering stuff

	//Flags for rendering different stages of the algorithm.
	public bool rend_DoArea = true,
				rend_DoSamples = false,
				rend_DoLigns = false,
				rend_DoStrips = false,
				rend_DoPadis = false;

	public float rend_Area_Alpha = 0.2f,
				 rend_Sample_Alpha = 0.05f,
				 rend_Lign_Alpha = 0.5f;
	public Color rend_Strip_Color = Color.white,
				 rend_Padi_Color = Color.white;

	public Vector3i rend_Sample_MinI = Vector3i.Zero,
					rend_Sample_MaxI = new Vector3i(2, 2, 2);
	public bool rend_Sample_IgnoreBelowThreshold = false;


	private Vector3i.Iterator rend_SampleIterator
		{ get { return new Vector3i.Iterator(rend_Sample_MinI, rend_Sample_MaxI + 1); } }
	private float rend_SampleSpaceIncrement { get { return AreaSize / (float)(NSamples - 1); } }

	private float rend_SampleToWorldSpace(float sampleI)
	{
		return -(AreaSize * 0.5f) + (sampleI * rend_SampleSpaceIncrement);
	}
	private Vector3 rend_SampleToWorldSpace(Vector3i sample)
	{
		return rend_SampleToWorldSpace(sample.x, sample.y, sample.z);
	}
	private Vector3 rend_SampleToWorldSpace(float sampleX, float sampleY, float sampleZ)
	{
		float halfAreaSize = AreaSize * 0.5f;
		float sampleIncrement = rend_SampleSpaceIncrement;

		return new Vector3(-halfAreaSize + (sampleX * sampleIncrement),
						   -halfAreaSize + (sampleY * sampleIncrement),
						   -halfAreaSize + (sampleZ * sampleIncrement));
	}
	private void rend_DrawSpans<T>(SpanTree<T> spanTree,
								   int axis, int axis2, int axis3, Vector2i pos23,
								   Func<T, ushort> getSimplestIndex,
								   Action<Vector3, Vector3> spanDrawer)
	{
		//Render all the largest simple dikes needed to cover the lign.
		int spanI = 0;
		int sampleMin,
			sampleMax = -1;
		while (sampleMax < spanTree.NSamples - 1)
		{
			//Get the samples this dike spans.
			spanI = getSimplestIndex(spanTree.Values[spanI]);
			spanTree.GetEdges(spanI, out sampleMin, out sampleMax);

			//Calculate the positions to draw the gizmos at.
			const float border = 0.1f;
			float sampleMinPos1 = rend_SampleToWorldSpace(sampleMin + border),
				  sampleMaxPos1 = rend_SampleToWorldSpace(sampleMax - border);
			Vector2 samplePos23 = new Vector2(rend_SampleToWorldSpace(pos23.x),
											  rend_SampleToWorldSpace(pos23.y));

			//Draw the dike.
			Vector3 lineStart = new Vector3(),
					lineEnd = new Vector3();
			lineStart[axis] = sampleMinPos1;
			lineStart[axis2] = samplePos23.x;
			lineStart[axis3] = samplePos23.y;
			lineEnd[axis] = sampleMaxPos1;
			lineEnd[axis2] = samplePos23.x;
			lineEnd[axis3] = samplePos23.y;
			spanDrawer(lineStart, lineEnd);

			spanI += 1;
		}
	}

	#endregion

	private void OnDrawGizmos()
	{
		if (rend_DoArea)
		{
			Gizmos.color = new Color(1.0f, 1.0f, 1.0f, rend_Area_Alpha);
			Gizmos.DrawCube(Vector3.zero, new Vector3(AreaSize, AreaSize, AreaSize));
		}


		if (rend_DoSamples && gen_samples != null)
		{
			float sampleIncrement = rend_SampleSpaceIncrement;
			foreach (var sampleI in rend_SampleIterator)
			{
				float value = gen_samples.Get(sampleI);
				if (rend_Sample_IgnoreBelowThreshold && !gen_isSampleAbove.Get(sampleI))
					continue;

				Gizmos.color = new Color(value, value, value, rend_Sample_Alpha);

				Gizmos.DrawSphere(rend_SampleToWorldSpace(sampleI),
								  sampleIncrement * 0.5f * value);
			}
		}

		if (rend_DoLigns)
		{
			foreach (var sampleI in rend_SampleIterator)
			{
				//If this index is the beginning of a lign, render that lign.
				if (gen_lignsAlongX != null && sampleI.x == 0)
				{
					var lign = gen_lignsAlongX[sampleI.y, sampleI.z];
					Gizmos.color = new Color(1.0f, 0.0f, 0.0f, rend_Lign_Alpha);
					rend_DrawSpans(lign, 0, 1, 2, new Vector2i(sampleI.y, sampleI.z),
								   dike => dike.SimplestIndex,
								   (a, b) => Gizmos.DrawLine(a, b));
				}
				if (gen_lignsAlongY != null && sampleI.y == 0)
				{
					var lign = gen_lignsAlongY[sampleI.x, sampleI.z];
					Gizmos.color = new Color(0.0f, 1.0f, 0.0f, rend_Lign_Alpha);
					rend_DrawSpans(lign, 1, 0, 2, new Vector2i(sampleI.x, sampleI.z),
							       dike => dike.SimplestIndex,
							       (a, b) => Gizmos.DrawLine(a, b));
				}
				if (gen_lignsAlongZ != null && sampleI.z == 0)
				{
					var lign = gen_lignsAlongZ[sampleI.x, sampleI.y];
					Gizmos.color = new Color(0.0f, 0.0f, 1.0f, rend_Lign_Alpha);
					rend_DrawSpans(lign, 2, 0, 1, new Vector2i(sampleI.x, sampleI.y),
							       dike => dike.SimplestIndex,
							       (a, b) => Gizmos.DrawLine(a, b));
				}
			}
		}

		if (rend_DoStrips)
		{
			float sampleIncrement = rend_SampleSpaceIncrement;

			foreach (var sampleI in rend_SampleIterator)
			{
				//If this index is the beginning of a strip, render that strip.
				if (gen_stripsAlongXY != null && sampleI.x == 0 &&
					sampleI.y < gen_stripsAlongXY.GetLength(0))
				{
					var strip = gen_stripsAlongXY[sampleI.y, sampleI.z];
					Gizmos.color = rend_Strip_Color;
					rend_DrawSpans(strip, 0, 1, 2, new Vector2i(sampleI.y, sampleI.z),
							       i => i,
							   	   (a, b) =>
								   {
								       const float border = 0.1f;
									   Gizmos.DrawCube(new Vector3((a.x + b.x) * 0.5f,
																   a.y + (sampleIncrement * 0.5f),
																   a.z),
													   new Vector3(Mathf.Abs(b.x - a.x),
													 			   sampleIncrement *
																       (1.0f - (border * 2.0f)),
																   0.0001f));
								   });
				}
			}
		}

		if (rend_DoPadis)
		{
			if (gen_padiRectsPerXYFarm != null)
			{
				const float border = 0.1f;
				Gizmos.color = rend_Padi_Color;

				for (int z = 0; z < gen_padiRectsPerXYFarm.Length; ++z)
				{
					foreach (var padi in gen_padiRectsPerXYFarm[z])
					{
						Vector3 min = rend_SampleToWorldSpace(padi.Min.x,
															  padi.Min.y,
															  z),
								max = rend_SampleToWorldSpace(padi.Max.x - border,
															  padi.Max.y - border,
															  z);
						Gizmos.DrawCube((max + min) * 0.5f, max - min);
					}
				}
			}
		}
	}
}