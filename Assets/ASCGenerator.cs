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
			Values = new Data[GetNNodes(NSamples)];
		}

		//Below are utility methods that tell you about specific nodes based on their index.
		/// <summary>
		/// The index in the binary tree of the first span of smallest size (i.e. size 2).
		/// This span touches the left-most edge of the sample range,
		///     as does its parent, grandparent, etc. all the way back to the root.
		/// </summary>
		public int IndexOfFirstLeafNode { get { return GetIndexOfFirstLeafNode(Values.Length); } }
		/// <summary>
		/// The index in the binary tree of the given node's parent.
		/// </summary>
		public int IndexOfParentNode(int spanI) { return (spanI - 1) / 2; }
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
			return GetLargestSpan(minSampleI, maxSampleI, Values.Length, NSamples);
		}
		/// <summary>
		/// Gets the largest "acceptable" span covering the given sample.
		/// </summary>
		/// <param name="predicate">
		/// Evaluates the span at the given index with the given value.
		/// </param>
		public int GetLargestSpan(int sampleI, Func<int, Data, bool> predicate)
		{
			int spanI = 0;
			while (!predicate(spanI, Values[spanI]))
			{
				//Move to one of the child nodes.

				int min, max;
				GetEdges(spanI, out min, out max);
				int mid = (max + min) / 2;

				if (sampleI >= mid)
					spanI = IndexOfFirstChildNode(spanI) + 1;
				else
					spanI = IndexOfFirstChildNode(spanI);
			}

			return spanI;
		}

		/// <summary>
		/// Gets the number of elements in a binary tree to cover the given number of samples.
		/// </summary>
		public static int GetNNodes(int nSamples) { return ((nSamples - 1) * 2) - 1; }
		/// <summary>
		/// The index in a SpanTree for the first span of smallest size (i.e. size 2).
		/// This span touches the left-most edge of the sample range,
		///     as does its parent, grandparent, etc. all the way back to the root.
		/// </summary>
		public static int GetIndexOfFirstLeafNode(int nValues) { return nValues / 2; }
		/// <summary>
		/// The index in a SpanTree of the first child of the given node.
		/// The second child is the next index after that.
		/// </summary>
		public static int IndexOfFirstChildNode(int spanI) { return (spanI * 2) + 1; }
		/// <summary>
		/// For some SpanTree, finds the largest span that starts at exactly the given min sample
		///     and ends no later than the given max sample.
		/// </summary>
		/// <param name="nValues">The number of tree nodes in the SpanTree.</param>
		/// <param name="nSamples">The number of samples in the SpanTree.</param>
		public static int GetLargestSpan(int minSampleI, int maxSampleI,
									     int nNodes, int nSamples)
		{
			//Shortcut: if minSample is odd, it's longest span will be the leaf node.
			if (minSampleI % 2 == 1)
				return GetIndexOfFirstLeafNode(nNodes) + minSampleI;

			//Start at the root and work downward until we find it.
			int spanI = 0,
				spanStart = 0,
				spanEnd = nSamples - 1,
				spanMid = spanEnd / 2;
			while (spanStart != minSampleI | spanEnd > maxSampleI)
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

				spanMid = (spanStart + spanEnd) / 2;
			}
			return spanI;
		}

		public override string ToString()
		{
			var str = new System.Text.StringBuilder();
			str.Append('[');
			str.Append(NSamples);
			str.Append(" samples: ");
			for (int i = 0; i < Values.Length; ++i)
			{
				if (i > 0)
					str.Append(',');
				str.Append(Values[i].ToString());
			}
			str.Append(']');
			return str.ToString();
		}
	}


	/// <summary>
	/// Pauses a coroutine until the "shouldResume" field is set to "true" in the Inspector.
	/// </summary>
	private class YieldForResumeField : CustomYieldInstruction
	{
		private ASCGenerator gen;
		public override bool keepWaiting { get { return !gen.shouldResume; } }
		public YieldForResumeField(ASCGenerator _gen) { gen = _gen; gen.shouldResume = false; }
	}
	[SerializeField]
	private bool shouldBePausable = false;
	[SerializeField]
	private bool shouldResume = false;


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

	/// <summary>
	/// Iterates over all largest simple dikes in a lign.
	/// </summary>
	public struct SimpleDikeIterator
	{
		public Lign Lign { get; private set; }
		public SimpleDikeIterator(Lign lign)
		{
			Lign = lign;
			Current = -1;
		}

		//The stuff that makes this struct enumerable:
		public int Current { get; private set; }
		public void Reset() { Current = -1; }
		public bool MoveNext()
		{
			if (Current >= 0 & Lign.IsLastInLayer(Current))
				return false;

			Current = Lign.Values[Current + 1].SimplestIndex;
			return true;
		}
		public void Dispose() { }
		public SimpleDikeIterator GetEnumerator() { return this; }
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

					int childI = Lign.IndexOfFirstChildNode(dikeI);
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
					firstNodeI = Lign.IndexOfFirstChildNode(firstNodeI);
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

	#region Strips

	/// <summary>
	/// Iterates over all largest simple plots in a strip.
	/// </summary>
	public struct SimplePlotIterator
	{
		public Strip Strip { get; private set; }
		public SimplePlotIterator(Strip strip)
		{
			Strip = strip;
			Current = -1;
		}

		//The stuff that makes this struct enumerable:
		public int Current { get; private set; }
		public void Reset() { Current = -1; }
		public bool MoveNext()
		{
			if (Current >= 0 & Strip.IsLastInLayer(Current))
				return false;

			Current = Strip.Values[Current + 1];
			return true;
		}
		public void Dispose() { }
		public SimplePlotIterator GetEnumerator() { return this; }
	}

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

	//TODO: Would my BVH structure work better for building padis?
	private List<Rect2i> gen_padisTentative;
	private int gen_padiConsideringZ;
	private List<Rect2i>[] gen_padiRectsPerXYFarm;
	private Strip[,] gen_padiXSpans,
					 gen_padiYSpans;

	/// <summary>
	/// Gets the padi covering the given sample.
	/// </summary>
	private Rect2i GetPadi(Vector3i samplePos)
	{
		Strip xStrip = gen_padiXSpans[samplePos.y, samplePos.z],
			  yStrip = gen_padiYSpans[samplePos.x, samplePos.z];

		Rect2i padi;
		xStrip.GetEdges(xStrip.GetLargestSpan(samplePos.x, isStripSpanSimple),
						out padi.Min.x, out padi.Max.x);
		yStrip.GetEdges(yStrip.GetLargestSpan(samplePos.y, isStripSpanSimple),
						out padi.Min.y, out padi.Max.y);

		return padi;
	}
	private bool isStripSpanSimple(int i, ushort u) { return i == u; }

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
		gen_padiRectsPerXYFarm = new List<Rect2i>[gen_samples.SizeZ()];
		gen_padisTentative = new List<Rect2i>(gen_samples.SizeX());
		for (int z = 0; z < gen_padiRectsPerXYFarm.Length; ++z)
		{
			gen_padiConsideringZ = z;

			//Allocate the list, with a good first estimate of how many elements it will have.
			var padis = new List<Rect2i>(gen_samples.SizeX() * gen_samples.SizeY() / 2);
			gen_padiRectsPerXYFarm[z] = padis;
			//For every strip in the farm...
			for (int stripI = 0; stripI < gen_stripsAlongXY.SizeX(); ++stripI)
			{
				var strip = gen_stripsAlongXY[stripI, z];

				//For every length-maximal simple plot in the strip...
				foreach (int plotI in new SimplePlotIterator(strip))
				{
					//Start the padi with just the plot's rectangle.
					int minX, maxX;
					strip.GetEdges(plotI, out minX, out maxX);
					if (z == 1 && (stripI == 3 || stripI == 4) && minX == 7)
						Debug.Log(minX.ToString() + "," + maxX);
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
						//TODO: Is it allowable and better to always expand the padi if the plot is simple -- not simple AND length-maximal?
						if (neighborStrip.Values[plotI] == plotI)
							padi = new Rect2i(padi.Min, new Vector2i(maxX, neighborStripI + 1));
						//Otherwise, stop here.
						else
							break;
					}

					//Split up the padi so it conforms to the binary tree structure.
					//Then, see how the split-up padis fit among the other ones we've already discovered.
					gen_padisTentative.Clear();
					gen_padisTentative.Add(padi);

					//Split up each padi so it conforms to the binary tree structure.
					for (int newPadiI = 0; newPadiI < gen_padisTentative.Count; ++newPadiI)
					{
						var padiToSplit = gen_padisTentative[newPadiI];

						//For each vertical lign, see whether it breaks up this padi.
						for (int lignI = padiToSplit.Min.x; lignI <= padiToSplit.Max.x; ++lignI)
						{
							var lign = gen_lignsAlongY[lignI, z];

							//Get the largest dike that starts at the beginning of the padi
							//    and doesn't pass over the end of it.
							int dikeI = lign.GetLargestSpan(padiToSplit.Min.y, padiToSplit.Max.y);
							int dikeMin, dikeMax;
							lign.GetEdges(dikeI, out dikeMin, out dikeMax);

							//If the dike isn't simple, split the padi.
							bool isSimple = lign.Values[dikeI].SimplestIndex <= dikeI;
							if (!isSimple)
							{
								int splitPoint = (dikeMin + dikeMax) / 2;
								Rect2i newPadi1 = new Rect2i(padiToSplit.Min,
															 new Vector2i(padiToSplit.Max.x, splitPoint)),
									   newPadi2 = new Rect2i(new Vector2i(padiToSplit.Min.x, splitPoint),
															 padiToSplit.Max);

								//Replace this padi with the first half and keep checking.
								gen_padisTentative[newPadiI] = newPadi1;
								padiToSplit = newPadi1;
								//The second part of the split padi will be checked later.
								gen_padisTentative.Add(newPadi2);

								//Re-check this new, smaller padi against this lign.
								lignI -= 1;
							}
							//Otherwise, if the padi sticks out past the end of the dike, split the padi.
							else if (dikeMax < padiToSplit.Max.y)
							{
								Rect2i newPadi1 = new Rect2i(padiToSplit.Min,
															 new Vector2i(padiToSplit.Max.x, dikeMax)),
									   newPadi2 = new Rect2i(new Vector2i(padiToSplit.Min.x, dikeMax),
															 padiToSplit.Max);

								//We know the first part of the new padi fits into this simple dike,
								//    so just replace the current padi with it.
								gen_padisTentative[newPadiI] = newPadi1;
								padiToSplit = newPadi1;
								//The second part of the split padi will be checked later.
								gen_padisTentative.Add(newPadi2);
								continue;
							}
						}

						//If the user is viewing this algorithm's progress, pause for user input.
						if (shouldBePausable & rend_SampleRange.ContainsZ(gen_padiConsideringZ))
							yield return new YieldForResumeField(this);
					}

					//For each "tentative" padi, see how it fits among the other padis.
					//If there aren't any problems, add it to the main list.
					//We already know the "tentative" padis don't interfere with each other,
					//    so don't bother counting past the current main list.
					int nOldPadis = padis.Count;
					for (int newPadiI = 0; newPadiI < gen_padisTentative.Count; ++newPadiI)
					{
						var newPadi = gen_padisTentative[newPadiI];
						bool shouldDrop = false;

						//Check each of the current padis.
						for (int oldPadiI = 0; oldPadiI < nOldPadis; ++oldPadiI)
						{
							var oldPadi = padis[oldPadiI];

							//If the other padi totally contains this one,
							//    then this one isn't necessary.
							if (oldPadi.Contains(newPadi))
							{
								shouldDrop = true;
								break;
							}
							//If this new padi totally contains the current one,
							//    remove the current one.
							if (newPadi.Contains(oldPadi))
							{
								padis.RemoveAt(oldPadiI);
								nOldPadis -= 1;
								oldPadiI -= 1;
								continue;
							}
							//If the two padis just touch a bit, cut this one in half.
							else if (newPadi.Touches(oldPadi))
							{
								//If this padi sticks out the left or right side,
								//    cut it in half horizontally.
								if (newPadi.Min.x < oldPadi.Min.x |
									newPadi.Max.x > oldPadi.Max.x)
								{
									int splitX = (newPadi.Min.x + newPadi.Max.x) / 2;
									Rect2i newPadi1 = new Rect2i(newPadi.Min,
																 new Vector2i(splitX, newPadi.Max.y)),
										   newPadi2 = new Rect2i(new Vector2i(splitX, newPadi.Min.y),
																 newPadi.Max);

									//Replace this padi with the two halves.
									gen_padisTentative.Add(newPadi1);
									gen_padisTentative.Add(newPadi2);
									shouldDrop = true;
									break;
								}
								//If this padi sticks out the top or bottom side,
								//    cut it in half vertically.
								else
								{
									UnityEngine.Assertions.Assert.IsTrue(newPadi.Min.y < oldPadi.Min.y |
																		 newPadi.Max.y > oldPadi.Max.y);

									int splitY = (newPadi.Min.y + newPadi.Max.y) / 2;
									Rect2i newPadi1 = new Rect2i(newPadi.Min,
																 new Vector2i(newPadi.Max.x, splitY)),
										   newPadi2 = new Rect2i(new Vector2i(newPadi.Min.x, splitY),
																 newPadi.Max);

									//Replace this padi with the two halves.
									gen_padisTentative.Add(newPadi1);
									gen_padisTentative.Add(newPadi2);
									shouldDrop = true;
									break;
								}
							}
						}

						//If this padi is clear, add it to the main list.
						if (!shouldDrop)
							padis.Add(newPadi);
					}
				}
			}
		}

		yield break; //TODO: Test the rest of this.

		//Convert the padis to a more-efficient SpanTree form.
		//We need one set of SpanTrees for horizontal extents,
		//    and another for vertical extents.
		gen_padiXSpans = new Strip[gen_samples.SizeY(), gen_samples.SizeZ()];
		foreach (var stripIndex in gen_padiXSpans.AllIndices())
		{
			var strip = new Strip(gen_samples.SizeX());
			for (int i = 0; i < strip.Values.Length; ++i)
				strip.Values[i] = ushort.MaxValue;
			gen_padiXSpans.Set(stripIndex, strip);
		}
		gen_padiYSpans = new Strip[gen_samples.SizeX(), gen_samples.SizeZ()];
		foreach (var stripIndex in gen_padiYSpans.AllIndices())
		{
			var strip = new Strip(gen_samples.SizeY());
			for (int i = 0; i < strip.Values.Length; ++i)
				strip.Values[i] = ushort.MaxValue;
			gen_padiYSpans.Set(stripIndex, strip);
		}
		int nTreeNodesX = Strip.GetNNodes(gen_samples.SizeX()),
			nTreeNodesY = Strip.GetNNodes(gen_samples.SizeY());
		for (int padiZ = 0; padiZ < gen_padiRectsPerXYFarm.Length; ++padiZ)
		{
			foreach (var padiRect in gen_padiRectsPerXYFarm[padiZ])
			{
				//Get the SpanTree index of the area this padi covers, along both axes.
				//Record that index in all the Strips this padi spans.

				//Horizontal strips:
				int xSpanI = Strip.GetLargestSpan(padiRect.Min.x, padiRect.Max.x,
												  nTreeNodesX, gen_samples.SizeX());
				for (int horzStripI = padiRect.Min.y; horzStripI < padiRect.Max.y; ++horzStripI)
				{
					var strip = gen_padiXSpans[horzStripI, padiZ];

					//Confirm that no other padi has touched this area before.
					UnityEngine.Assertions.Assert.AreEqual(
						strip.Values[xSpanI], ushort.MaxValue,
						"More than one padi touches on the horz strip at YZ " +
							new Vector2i(horzStripI, padiZ) +
							", padi:" + padiRect);

					//Tell the span and all its parents that it represents a padi.
					int parentSpanI = xSpanI;
					while (true)
					{
						strip.Values[parentSpanI] = (ushort)xSpanI;

						if (parentSpanI <= 0)
							break;
						parentSpanI = strip.IndexOfParentNode(parentSpanI);
					}
				}

				//Vertical strips:
				int ySpanI = Strip.GetLargestSpan(padiRect.Min.y, padiRect.Max.y,
												  nTreeNodesY, gen_samples.SizeY());
				for (int vertStripI = padiRect.Min.x; vertStripI < padiRect.Max.x; ++vertStripI)
				{
					var strip = gen_padiYSpans[vertStripI, padiZ];

					//Confirm that no other padi has touched this area before.
					UnityEngine.Assertions.Assert.AreEqual(
						strip.Values[ySpanI], ushort.MaxValue,
						"More than one padi touches on the vert strip at XZ " +
							new Vector2i(vertStripI, padiZ) +
							", padi:" + padiRect);

					//Tell the span and all its parents that it represents a padi.
					int parentSpanI = ySpanI;
					while (true)
					{
						strip.Values[parentSpanI] = (ushort)ySpanI;

						if (parentSpanI <= 0)
							break;

						parentSpanI = strip.IndexOfParentNode(parentSpanI);
					}
				}
			}

			//Double-check that all strips are fully filled in.
			MyAssert.IsTrue(() =>
			{
				for (int farmI = 0; farmI < gen_padiRectsPerXYFarm.Length; ++farmI)
				{
					for (int stripY = 0; stripY < gen_padiXSpans.GetLength(0); ++stripY)
					{
						var strip = gen_padiXSpans[stripY, farmI];

						foreach (int spanI in new SimplePlotIterator(strip))
						{
							if (spanI == ushort.MaxValue)
							{
								return "Padis don't cover horizontal span of strip at YZ" +
									       new Vector2i(stripY, farmI) +
										   ", strip data:" + strip;
							}
						}
					}
					for (int stripX = 0; stripX < gen_padiYSpans.GetLength(0); ++stripX)
					{
						var strip = gen_padiYSpans[stripX, farmI];

						foreach (int spanI in new SimplePlotIterator(strip))
						{
							if (spanI == ushort.MaxValue)
							{
								return "Padis don't cover vertical span of strip at XZ " +
									       new Vector2i(stripX, farmI) +
										   " , strip data:" + strip;
							}
						}
					}
				}

				return null;
			});
		}
	}


	#region Rendering stuff

	//Flags for rendering different stages of the algorithm.
	public bool rend_DoArea = true,
				rend_DoSamples = false,
				rend_DoLigns = false,
				rend_DoStrips = false,
				rend_DoPadisTemp = false,
				rend_DoPadisPre = false,
				rend_DoPadisPost = false;

	public float rend_Area_Alpha = 0.2f,
				 rend_Sample_Alpha = 0.05f,
				 rend_Lign_Alpha = 0.5f;
	public Color rend_Strip_Color = Color.white,
				 rend_Padi_Color = Color.white;

	public Vector3i rend_Sample_MinI = Vector3i.Zero,
					rend_Sample_MaxI = new Vector3i(2, 2, 2);
	public bool rend_Sample_IgnoreBelowThreshold = false;


	private Rect3i rend_SampleRange { get { return new Rect3i(rend_Sample_MinI, rend_Sample_MaxI + 1); } }
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
			spanI += 1;

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

		//TODO: Am I drawing ligns with the Y flipped? Or padis? Or am I just generating padis incorrectly?

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

		//Padis:
		Action<Rect2i, int> drawPadiAtZ = (padi, z) =>
		{
			const float border = 0.1f;
			Vector3 min = rend_SampleToWorldSpace(padi.Min.x, padi.Min.y, z),
					max = rend_SampleToWorldSpace(padi.Max.x - border,
												  padi.Max.y - border,
												  z);
			Gizmos.DrawCube((max + min) * 0.5f, max - min);
		};
		if (rend_DoPadisTemp)
		{
			if (gen_padisTentative != null &&
				rend_SampleRange.ContainsZ(gen_padiConsideringZ))
			{
				Gizmos.color = rend_Padi_Color;

				foreach (var padi in gen_padisTentative)
					if (rend_SampleRange.Touches(new Rect3i(padi, gen_padiConsideringZ)))
						drawPadiAtZ(padi, gen_padiConsideringZ);
			}
		}
		else if (rend_DoPadisPre)
		{
			if (gen_padiRectsPerXYFarm != null)
			{
				Gizmos.color = rend_Padi_Color;

				Rect3i sampleRange = rend_SampleRange;
				for (int z = 0; z < gen_padiRectsPerXYFarm.Length; ++z)
				{
					if (gen_padiRectsPerXYFarm[z] != null)
					{
						foreach (var padi in gen_padiRectsPerXYFarm[z])
						{
							if (!sampleRange.Touches(new Rect3i(padi, z)))
								continue;

							drawPadiAtZ(padi, z);
						}
					}
				}
			}
		}
		else if (rend_DoPadisPost)
		{
			if (gen_padiXSpans != null && gen_padiYSpans != null)
			{
				Rect3i sampleRange = rend_SampleRange;

				Gizmos.color = rend_Padi_Color;

				//Build a list of all padis based on the spans.
				HashSet<Vector2i> posesLeft = new HashSet<Vector2i>();
				for (int z = 0; z < gen_samples.GetLength(2); ++z)
				{
					//Keep track of which positions do not already have a padi assigned yet.
					UnityEngine.Assertions.Assert.AreEqual(posesLeft.Count, 0);
					for (int y = 0; y < gen_samples.GetLength(1); ++y)
						for (int x = 0; x < gen_samples.GetLength(0); ++x)
							posesLeft.Add(new Vector2i(x, y));

					//For every unassigned position, get its padi.
					while (posesLeft.Count > 0)
					{
						Vector2i pos = posesLeft.First();

						//Find the padi.
						var padi = GetPadi(new Vector3i(pos.x, pos.y, z));
						//All positions in this padi are now "assigned".
						foreach (var padiPos in padi)
							posesLeft.Remove(padiPos);

						//Only draw it if it touches the sample range.
						if (!sampleRange.Touches(new Rect3i(padi, z)))
							continue;
						drawPadiAtZ(padi, z);
					}
				}
			}
		}
	}
}