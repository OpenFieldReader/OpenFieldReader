using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ICRExtraction
{
	public class FormExtraction
	{
		public class FormExtractionOptions
		{
			public int ResizeWidth { get; set; }
			
			public int JunctionWidth { get; set; }
			public int JunctionHeight { get; set; }

			public FormExtractionOptions()
			{
				ResizeWidth = 1500;
				JunctionWidth = 25;
				JunctionHeight = 15;
			}
		}

		public class FormExtractionResult
		{
		}

		public static FormExtractionResult ProcessImage(string filename, FormExtractionOptions options = null)
		{
			if (options == null)
			{
				// Assume recommanded parameters.
				options = new FormExtractionOptions();
			}
			
			var orig = new Mat(filename);
			var image = new Mat(filename, ImreadModes.GrayScale);
			
			Cv2.AdaptiveThreshold(image, image, 255, AdaptiveThresholdTypes.MeanC, ThresholdTypes.Binary, 9, 2);
			
			/*using (new Window("dst image", image))
			{
				Cv2.WaitKey();
				Cv2.DestroyAllWindows();
			}*/
			
			// Resize image if too large.
			if (image.Width > options.ResizeWidth)
			{
				var height = options.ResizeWidth * image.Height / image.Width;
				Cv2.Resize(image, image, new Size(options.ResizeWidth, height));
			}
			
			Cv2.BitwiseNot(image, image);
			Cv2.Blur(image, image, new Size(1, 2));
			Cv2.Threshold(image, image, 0, 255, ThresholdTypes.Otsu | ThresholdTypes.Binary);
			
			MatOfByte3 mat3 = new MatOfByte3(image);
			MatIndexer<Vec3b> indexer = mat3.GetIndexer();
			
			var row = image.Height;
			var col = image.Width;
			Mat newImage = new Mat(row, col, MatType.CV_8UC3);
			newImage.SetTo(Scalar.Black);

			// We must determine if it "may" be an interesting blob.
			int[,] outputImg = null;

			Stopwatch watch = new Stopwatch();
			watch.Start();
			var hasBoxes = HasBoxes(indexer, row, col, out outputImg, options);
			watch.Stop();
			Console.WriteLine("Duration: " + watch.Elapsed);
			
			if (hasBoxes)
			{
				var img = CreateImage(outputImg, hasColor: true);
				Cv2.BitwiseOr(newImage, img, newImage);
			}
			
			// Preview
			if (hasBoxes && image.Width != 0)
			{
				Cv2.Dilate(newImage, newImage, Cv2.GetStructuringElement(MorphShapes.Cross, new Size(2, 2)));
				
				Cv2.BitwiseNot(image, image);
				int width = 400;
				var height = width * image.Height / image.Width;
				Cv2.Resize(orig, orig, new Size(width, height));
				Cv2.Resize(image, image, new Size(width, height));
				Cv2.Resize(newImage, newImage, new Size(width, height));
				
				using (new Window("orig", orig))
				using (new Window("pre", image))
				using (new Window("post", newImage))
				{
					Cv2.WaitKey();
					Cv2.DestroyAllWindows();
				}
			}

			// Dispose.
			orig.Dispose();
			image.Dispose();
			newImage.Dispose();
			mat3.Dispose();

			return new FormExtractionResult();

			//newImage.SaveImage("mask.png");

			//var maskImg = new Mat("mask.png", ImreadModes.GrayScale);
			//Cv2.BitwiseNot(maskImg, maskImg);

			//Cv2.Blur(maskImg, maskImg, new Size(3, 3));
			//Cv2.Threshold(maskImg, maskImg, 0, 255, ThresholdTypes.Otsu | ThresholdTypes.Binary);

			//Cv2.BitwiseNot(maskImg, maskImg);
			//Cv2.BitwiseNot(image, image);
			//Cv2.BitwiseOr(image, maskImg, maskImg);

			//using (new Window("src image", image))
			//using (new Window("dst image", maskImg))
			//{
			//	Cv2.WaitKey();
			//}
		}

		private class Junction
		{
			public bool Top { get; set; }
			public bool Bottom { get; set; }
			public bool Left { get; set; }
			public bool Right { get; set; }
			public int NumTop { get; set; }
			public int NumBottom { get; set; }
			public int NumLeft { get; set; }
			public int NumRight { get; set; }
			public int X { get; set; }
			public int Y { get; set; }
			public int GroupId { get; set; }
			public double GapX { get; set; }
		}
		
		private class Line
		{
			public List<Junction> Junctions { get; set; }
			public double GapX { get; set; }
		}

		private class LineCluster
		{
			public int X { get; set; }
			public int Y { get; set; }
			public bool Top { get; set; }
			public bool Bottom { get; set; }
			public List<Junction> Junctions { get; set; }
		}

		private class BoxesCluster
		{
			public LineCluster TopLine { get; set; }
			public LineCluster BottomLine { get; set; }
		}

		private static bool HasBoxes(MatIndexer<Vec3b> labels, int row, int col, out int[,] outputImg, FormExtractionOptions options)
		{
			// Debug image.
			outputImg = new int[row, col];
			for (int y = 0; y < row; y++)
				for (int x = 0; x < col; x++)
					outputImg[y, x] = 0;

			// We are seaching for pattern!
			// We look for junctions.
			// This will help us make a decision.
			// Junction types: T, L, +.
			// Junctions allow us to find boxes contours.

			int width = options.JunctionWidth;
			int height = options.JunctionHeight;

			// Cache per line speed up the creation of various cache.
			Dictionary<int, List<Junction>> cacheListJunctionPerLine = new Dictionary<int, List<Junction>>();
			List<Junction> listJunction = new List<Junction>();

			// If there is too much junction near each other, maybe it's just a black spot.
			// We must ignore it to prevent wasting CPU and spend too much time.
			int maxProximity = 10;

			for (int y = 0; y < row; y++)
			{
				List<Junction> listJunctionX = null;
				int proximityCounter = 0;

				for (int x = 0; x < col; x++)
				{
					var junction = GetJunction(labels, row, col, height, width, y, x);
					if (junction != null)
					{
						if (listJunctionX == null)
						{
							listJunctionX = new List<Junction>();
						}
						listJunctionX.Add(junction);
						proximityCounter++;
					}
					else
					{
						if (listJunctionX != null)
						{
							if (proximityCounter < maxProximity)
							{
								if (!cacheListJunctionPerLine.ContainsKey(y))
								{
									cacheListJunctionPerLine.Add(y, new List<Junction>());
								}
								cacheListJunctionPerLine[y].AddRange(listJunctionX);
								listJunction.AddRange(listJunctionX);
								listJunctionX.Clear();
							}
							else
							{
								listJunctionX.Clear();
							}
						}
						proximityCounter = 0;
					}
				}

				if (proximityCounter < maxProximity && listJunctionX != null)
				{
					if (!cacheListJunctionPerLine.ContainsKey(y))
					{
						cacheListJunctionPerLine.Add(y, new List<Junction>());
					}
					cacheListJunctionPerLine[y].AddRange(listJunctionX);
					listJunction.AddRange(listJunctionX);
				}
			}

			Console.WriteLine("Junction.count: " + listJunction.Count);

			// TODO: add a parameter
			if (listJunction.Count > 30000)
			{
				// Something wrong happen. Too much junction for now.
				// If we continue, we would spend too much time processing the image.
				// Let's suppose we don't know.
				return false;
			}

			// We can skip the footer to focus on a particular part of the image.
			// (Faster to process and may improve accuracy)
			// Some forms contains image in the footer and it prevents extra processing.

			// TODO: add parameter
			/*int pagePercentToConsider = 100;
			int maxRow = row * pagePercentToConsider / 100;
			listJunction = listJunction.Where(m => m.Y < maxRow).ToList();*/

			// Let's check the list of points.

			// Search near same line.

			// Prepare cache to speedup searching algo.
			Dictionary<Junction, List<Junction>> cacheNearJunction = new Dictionary<Junction, List<Junction>>();
			Dictionary<Junction, List<Junction>> cachePossibleNextJunctionRight = new Dictionary<Junction, List<Junction>>();
			Dictionary<Junction, List<Junction>> cachePossibleNextJunctionLeft = new Dictionary<Junction, List<Junction>>();
			int minX = 10;
			int maxX = 70;
			foreach (var junction in cacheListJunctionPerLine.SelectMany(m => m.Value))
			{
				var listJunctionNearJunction = new List<Junction>();

				for (int deltaY = -3; deltaY <= 3; deltaY++)
				{
					if (cacheListJunctionPerLine.ContainsKey(junction.Y - deltaY))
						listJunctionNearJunction.AddRange(cacheListJunctionPerLine[junction.Y - deltaY]);
				}
				
				var list = listJunctionNearJunction
					.Where(m =>
						Math.Abs(m.X - junction.X) <= maxX
						)
					.ToList();
				
				cacheNearJunction.Add(junction, list);

				var possibleNextJunction = list
					.Where(m =>
						Math.Abs(m.X - junction.X) >= minX
						)
					.ToList();

				cachePossibleNextJunctionLeft.Add(junction, possibleNextJunction.Where(m => m.X < junction.X).ToList());
				cachePossibleNextJunctionRight.Add(junction, possibleNextJunction.Where(m => m.X > junction.X).ToList());
			}

			int numSol = 0;
			
			List<Line> possibleSol = new List<Line>();

			// We use a dictionary here because we need a fast way to remove entry.
			// We reduce computation and we also merge solutions.
			var elements = listJunction.OrderBy(m => m.Y).ToDictionary(m => m, m => m);

			int skipSol = 0;
			while (elements.Any())
			{
				var start = elements.First().Key;
				elements.Remove(start);

				List<Line> listSolutions = new List<Line>();
				var junctionsForGap = cacheNearJunction[start];
				
				for (int iGap = 0; iGap < junctionsForGap.Count; iGap++)
				{
					var gap = junctionsForGap[iGap];

					// Useless because it's already done with: cacheNearJunction.
					/*
					var gapY = Math.Abs(gap.Y - start.Y);
					if (gapY > 2)
					{
						continue;
					}*/

					var gapX = Math.Abs(gap.X - start.X);
					if (gapX <= 20 || gapX > 50)
					{
						continue;
					}

					// We will reduce list of solution by checking if the solution is already found.
					if (listSolutions.Any(m => Math.Abs(m.GapX - gapX) < 2 && m.Junctions.Contains(start)))
					{
						skipSol++;
						continue;
					}

					List<Junction> curSolution = new List<Junction>();
					curSolution.Add(start);
					
					int numElementsRight = FindElementsOnDirection(cachePossibleNextJunctionRight, start, gap, gapX, curSolution);
					int numElementsLeft = FindElementsOnDirection(cachePossibleNextJunctionLeft, start, gap, -gapX, curSolution);
					
					int numElements = numElementsLeft + numElementsRight;

					if (numElements > 4)
					{
						// TODO: add a parameter.
						if (numSol == 30000)
						{
							// Something wrong happen. Too much solution for now.
							// If we continue, we would spend too much time processing the image.
							// Let's suppose we don't know.
							return false;
						}
						
						numSol++;
						if (numSol % 1000 == 0)
							Console.WriteLine(numSol + " : Found ");

						listSolutions.Add(new Line
						{
							GapX = gapX,
							Junctions = curSolution
						});
					}
				}
				
				Line bestSol = listSolutions.OrderByDescending(m => m.Junctions.Count).FirstOrDefault();

				if (bestSol != null)
				{
					// Too slow. (faster if we skip removal)
					// But, we have more solutions.
					foreach (var item in bestSol.Junctions)
					{
						elements.Remove(item);
					}

					possibleSol.Add(bestSol);
				}
			}

			Console.WriteLine("Skip: " + skipSol);

			Console.WriteLine(numSol + " : Solution found");
			Console.WriteLine(possibleSol.Count + " Best solution found");
			
			// Let's merge near junctions. (vertical line)
			// We assign a group id for each clusters.

			int nextGroupId = 1;
			foreach (var curSolution in possibleSol)
			{
				if (curSolution.Junctions.First().GroupId == 0)
				{
					curSolution.Junctions.ForEach(m => m.GapX = curSolution.GapX);

					// Not assigned yet.

					// Find near junction.
					int groupId = 0;

					foreach (var item in curSolution.Junctions)
					{
						var alreadyClassified = cacheNearJunction[item]
							.Where(m =>
								m.GroupId != 0 &&
								Math.Abs(m.X - item.X) <= 5 &&
								Math.Abs(m.Y - item.Y) <= 3 &&
								Math.Abs(m.GapX - item.GapX) <= 2
							);
						if (alreadyClassified.Any())
						{
							groupId = alreadyClassified.First().GroupId;
							break;
						}
					}

					if (groupId == 0)
					{
						// Not found.

						// Create a new group.
						nextGroupId++;

						groupId = nextGroupId;
					}

					curSolution.Junctions.ForEach(m => m.GroupId = groupId);
				}
			}
			
			Dictionary<int, List<Junction>> junctionsPerGroup = possibleSol
				.SelectMany(m => m.Junctions)
				.GroupBy(m => m.GroupId)
				.ToDictionary(m => m.Key, m => m.ToList());
			
			// Let's explore the clusters directions and try to interconnect clusters on the horizontal side.

			// Minimum percent of elements to determine the direction.
			int minElementPercent = 50;

			List<LineCluster> lineClusters = new List<LineCluster>();
			
			foreach (var item in junctionsPerGroup)
			{
				int groupId = item.Key;
				List<Junction> junctions = item.Value;

				int minElementDir = minElementPercent * junctions.Count / 100;

				// Determine the general direction.
				var top = junctions.Count(m => m.Top) > minElementDir;
				var bottom = junctions.Count(m => m.Bottom) > minElementDir;
				
				junctions.ForEach(m => m.Top = top);
				junctions.ForEach(m => m.Bottom = bottom);
				
				var x = (int)junctions.Average(m => m.X);
				var y = (int)junctions.Average(m => m.Y);

				lineClusters.Add(new LineCluster
				{
					Bottom = bottom,
					Top = top,
					Junctions = junctions,
					X = x,
					Y = y
				});
			}

			List<BoxesCluster> boxesClusters = new List<BoxesCluster>();
			Dictionary<LineCluster, LineCluster> lineClustersTop = new Dictionary<LineCluster, LineCluster>();
			Dictionary<LineCluster, LineCluster> lineClustersBottom = new Dictionary<LineCluster, LineCluster>();

			foreach (var itemA in lineClusters)
			{
				foreach (var itemB in lineClusters)
				{
					if (itemA != itemB)
					{
						if (itemA.Bottom && itemB.Top || itemA.Top && itemB.Bottom)
						{
							// Compatible.
							var topLine = itemA.Top ? itemA : itemB;
							var bottomLine = itemA.Top ? itemB : itemA;

							if (lineClustersTop.ContainsKey(topLine))
								continue;
							if (lineClustersBottom.ContainsKey(bottomLine))
								continue;
							
							var firstGapX = itemA.Junctions.Average(m => m.GapX);
							var secondGapX = itemB.Junctions.Average(m => m.GapX);
							
							// GapX should be similar. Otherwise, just ignore it.
							if (Math.Abs(firstGapX - secondGapX) <= 2)
							{
								var avgGapX = (firstGapX + secondGapX) / 2;

								var minGapY = Math.Max(10, avgGapX - 10);
								var maxGapY = Math.Max(60, avgGapX + 10);

								// For the majority of element on top line, we should be able to interconnect
								// with the other line.

								// Must have some common element next to each other.
								int commonElementCounter = 0;

								int minPercent = 90;
								int min = Math.Min(topLine.Junctions.Count, bottomLine.Junctions.Count);
								int minimumCommonElements = min * minPercent / 100;

								foreach (var topJunction in topLine.Junctions)
								{
									var hasCommonElement = bottomLine.Junctions.Any(m =>
										Math.Abs(topJunction.X - m.X) <= 5 &&
										topJunction.Y - m.Y >= minGapY &&
										topJunction.Y - m.Y <= maxGapY
									);
									if (hasCommonElement)
									{
										commonElementCounter++;

										if (commonElementCounter >= minimumCommonElements)
										{
											// We can stop now. It's a boxes!
											break;
										}
									}
								}

								if (commonElementCounter >= minimumCommonElements)
								{
									boxesClusters.Add(new BoxesCluster
									{
										TopLine = topLine,
										BottomLine = bottomLine
									});

									lineClustersTop.Add(topLine, topLine);
									lineClustersBottom.Add(bottomLine, bottomLine);
								}
							}
						}
					}
				}
			}

			nextGroupId = 0;
			foreach (var item in boxesClusters)
			{
				nextGroupId++;
				
				// Debug img:
				foreach (var junction in item.TopLine.Junctions)
				{
					DrawJunction(outputImg, nextGroupId, junction);
				}

				foreach (var junction in item.BottomLine.Junctions)
				{
					DrawJunction(outputImg, nextGroupId, junction);
				}
			}
			
			// TODO: returns boxes, debug info, etc.
			return boxesClusters.Any();
		}

		private static void DrawPoint(int[,] outputImg, int colorCode, int x, int y, int size)
		{
			// Must be centered.
			x -= size / 2;
			y -= size / 2;
			
			for (int i = 0; i < size; i++)
				for (int j = 0; j < size; j++)
					outputImg[y + i, x + j] = colorCode;
		}

		private static void DrawJunction(int[,] outputImg, int colorCode, Junction junction)
		{
			var top = junction.Top;
			var bottom = junction.Bottom;
			var right = junction.Right;
			var left = junction.Left;
			var numTop = junction.NumTop;
			var numBottom = junction.NumBottom;
			var numRight = junction.NumRight;
			var numLeft = junction.NumLeft;
			var x = junction.X;
			var y = junction.Y;
			
			if (top)
				for (int i = 0; i < numTop; i++)
					outputImg[y - i, x] = colorCode;
			if (bottom)
				for (int i = 0; i < numBottom; i++)
					outputImg[y + i, x] = colorCode;
			if (right)
				for (int i = 0; i < numRight; i++)
					outputImg[y, x + i] = colorCode;
			if (left)
				for (int i = 0; i < numLeft; i++)
					outputImg[y, x - i] = colorCode;
		}

		private static int FindElementsOnDirection(
			Dictionary<Junction, List<Junction>> cacheNearJunction,
			Junction start,
			Junction gap,
			int gapX,
			List<Junction> curSolution)
		{
			int numElements = 0;
			var x = start.X;
			var y = start.Y;
			List<Junction> remainingList = new List<Junction>();
			remainingList.AddRange(cacheNearJunction[gap]);
			while (remainingList.Any())
			{
				// Find which element is next one.
				int indexNextElement = -1;

				for (int iNext = 0; iNext < remainingList.Count; iNext++)
				{
					var cur = remainingList[iNext];
					int curX = cur.X;
					int curY = cur.Y;

					int distX = Math.Abs(x + gapX - curX);

					if (distX <= 1)
					{
						indexNextElement = iNext;
						iNext--;
						numElements++;
						remainingList.Clear();
						remainingList.AddRange(cacheNearJunction[cur]);
						x = curX;
						y = curY;

						curSolution.Add(cur);
						break;
					}
				}
				if (indexNextElement == -1)
				{
					// No element found.
					return numElements;
				}
			}
			return numElements;
		}

		private static Junction GetJunction(MatIndexer<Vec3b> labels, int row, int col, int height, int width, int y, int x)
		{
			var val = GetVal(labels, y, x);
			if (0 < val)
			{
				// Let's explore the directions.

				int numTop = 0;
				if (y - height >= 0)
					for (int i = 0; i < height; i++)
						if (GetVal(labels, y - i, x) == val)
							numTop++;
						else
							break;

				int numBottom = 0;
				if (y + height < row)
					for (int i = 0; i < height; i++)
						if (GetVal(labels, y + i, x) == val)
							numBottom++;
						else
							break;

				int numRight = 0;
				if (x + width < col)
					for (int i = 0; i < width; i++)
						if (GetVal(labels, y, x + i) == val)
							numRight++;
						else
							break;

				int numLeft = 0;
				if (x - width >= 0)
					for (int i = 0; i < width; i++)
						if (GetVal(labels, y, x - i) == val)
							numLeft++;
						else
							break;

				var top = numTop >= height;
				var bottom = numBottom >= height;
				var left = numLeft >= width;
				var right = numRight >= width;

				if ((top || bottom) && (left || right))
				{
					return new Junction
					{
						Bottom = bottom,
						Left = left,
						Right = right,
						Top = top,
						NumBottom = numBottom,
						NumLeft = numLeft,
						NumRight = numRight,
						NumTop = numTop,
						X = x,
						Y = y
					};
				}
			}
			return null;
		}

		private static int GetVal(MatIndexer<Vec3b> labels, int y, int x)
		{
			return (labels[y, x].Item0 | labels[y, x].Item1 | labels[y, x].Item2);
		}

		private static Random Random = new Random();
		private static Mat CreateImage(int[,] labels, bool hasColor)
		{
			var row = labels.GetLength(0);
			var col = labels.GetLength(1);

			Dictionary<int, int> cacheColor = new Dictionary<int, int>();
			int color = 0;

			Mat newImage = new Mat(row, col, MatType.CV_8UC3);
			for (int i = 0; i < row; i++)
			{
				for (int j = 0; j < col; j++)
				{
					var val = labels[i, j];

					if (val > 0)
					{
						if (hasColor)
						{
							if (!cacheColor.ContainsKey(val))
							{
								// Generate a random color
								color = (122 + Random.Next(123)) << 16 | (122 + Random.Next(123)) << 8 | (122 + Random.Next(123));
								cacheColor.Add(val, color);
							}
							else
							{
								color = cacheColor[val];
							}
							newImage.Set(i, j, color);
						}
						else
						{
							newImage.Set(i, j, 0xFFFFFF);
						}
					}
					else
					{
						newImage.Set(i, j, 0);
					}
				}
			}
			return newImage;
		}
	}
}
