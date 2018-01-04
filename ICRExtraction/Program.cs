using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICRExtraction
{
	class Program
	{
		static void Main(string[] args)
		{
			ProcessImage("form15.jpg");
		}

		public static void ProcessImage(string filename)
		{
			Stopwatch watch = new Stopwatch();
			watch.Start();

			var orig = new Mat(filename);
			var image = new Mat(filename, ImreadModes.GrayScale);

			Cv2.AdaptiveThreshold(image, image, 255, AdaptiveThresholdTypes.MeanC, ThresholdTypes.Binary, 13, 8);

			Cv2.BitwiseNot(image, image);

			Cv2.Blur(image, image, new Size(1, 2));
			Cv2.Threshold(image, image, 0, 255, ThresholdTypes.Otsu | ThresholdTypes.Binary);

			MatOfByte3 mat3 = new MatOfByte3(image); // cv::Mat_<cv::Vec3b>
			MatIndexer<Vec3b> indexer = mat3.GetIndexer();

			var row = image.Height;
			var col = image.Width;
			Mat newImage = new Mat(row, col, MatType.CV_8UC3);
			newImage.SetTo(Scalar.Black);

			// We must determine if it "may" be an interesting blob.
			int[,] outputImg = null;
			if (HasBoxes(indexer, row, col, out outputImg))
			{
				var img = CreateImage(outputImg, hasColor: true);
				Cv2.BitwiseOr(newImage, img, newImage);
			}

			watch.Stop();
			Console.WriteLine("Duration: " + watch.Elapsed);
			
			Cv2.BitwiseNot(image, image);
			
			using (new Window("src image", image))
			using (new Window("dst image", newImage))
			{
				Cv2.WaitKey();
			}

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

		public class Junction
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
		}

		public class LineCluster
		{
			public int X { get; set; }
			public int Y { get; set; }
			public bool Top { get; set; }
			public bool Bottom { get; set; }
			public List<Junction> Junctions { get; set; }
		}

		public class BoxesCluster
		{
			public LineCluster TopLine { get; set; }
			public LineCluster BottomLine { get; set; }
		}

		private static bool HasBoxes(MatIndexer<Vec3b> labels, int row, int col, out int[,] outputImg)
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

			int height = 15;
			int width = 25;

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
					listJunction.AddRange(listJunctionX);
			}

			Console.WriteLine("Junction.count: " + listJunction.Count);

			// Let's check the list of points.

			// Search near same line.
			Dictionary<Junction, List<Junction>> cacheNearJunction = new Dictionary<Junction, List<Junction>>();
			int maxY = 3;
			int maxX = 60;
			foreach (var junction in listJunction)
			{
				cacheNearJunction.Add(junction, listJunction.Where(m => Math.Abs(m.Y - junction.Y) <= maxY && Math.Abs(m.X - junction.X) <= maxX).ToList());
			}

			int numSol = 0;

			listJunction = listJunction.OrderBy(m => m.Y).ToList();

			List<List<Junction>> possibleSol = new List<List<Junction>>();
			while (listJunction.Any())
			{
				var start = listJunction[0];
				listJunction.RemoveAt(0);

				List<List<Junction>> listSolutions = new List<List<Junction>>();
				for (int iGap = 0; iGap < listJunction.Count; iGap++)
				{
					var gap = listJunction[iGap];

					var gapY = Math.Abs(gap.Y - start.Y);
					if (gapY > 2)
					{
						continue;
					}

					var gapX = Math.Abs(gap.X - start.X);
					if (gapX <= 10 || gapX > 50)
					{
						continue;
					}

					List<Junction> curSolution = new List<Junction>();
					curSolution.Add(start);

					int numElementsRight = FindElementsOnDirection(cacheNearJunction, start, gap, gapX, curSolution);
					int numElementsLeft = FindElementsOnDirection(cacheNearJunction, start, gap, -gapX, curSolution);

					int numElements = numElementsLeft + numElementsRight;

					if (numElements > 4)
					{
						numSol++;
						if (numSol % 1000 == 0)
							Console.WriteLine(numSol + " : Found ");

						listSolutions.Add(curSolution);
					}
				}

				var bestSol = listSolutions.OrderByDescending(m => m.Count).FirstOrDefault();

				if (bestSol != null)
				{
					foreach (var item in bestSol)
					{
						listJunction.Remove(item);
					}
					possibleSol.Add(bestSol);
				}
			}
			Console.WriteLine(numSol + " : Found ");

			// Let's merge near junctions. (vertical line)
			// We assign a group id for each clusters.

			int nextGroupId = 1;
			foreach (var curSolution in possibleSol)
			{
				if (curSolution.First().GroupId == 0)
				{
					// Not assigned yet.

					// Find near junction.
					int groupId = 0;

					foreach (var item in curSolution)
					{
						var alreadyClassified = cacheNearJunction[item]
							.Where(m =>
								m.GroupId != 0 &&
								Math.Abs(m.X - item.X) <= 5 &&
								Math.Abs(m.Y - item.Y) <= 3
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

					curSolution.ForEach(m => m.GroupId = groupId);
				}
			}

			Dictionary<int, List<Junction>> junctionsPerGroup = possibleSol
				.SelectMany(m => m)
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

				//DrawPoint(outputImg, 100000 + groupId, x, y, 5);
			}

			List<BoxesCluster> boxesClusters = new List<BoxesCluster>();

			foreach (var item in lineClusters)
			{
				if (item.Bottom)
				{
					// Search.
					var result = lineClusters
						.Where(m => m != item)
						.Where(m => m.Top)
						.Where(m => Math.Abs(m.X - item.X) < 20 && Math.Abs(m.Y - item.Y) > 10 && Math.Abs(m.Y - item.Y) < 60)
						.ToList();

					if (result.Count == 1)
					{
						// We found the top line.
						boxesClusters.Add(new BoxesCluster
						{
							TopLine = item,
							BottomLine = result[0]
						});
					}
				}
			}

			nextGroupId = 0;
			foreach (var item in boxesClusters)
			{
				nextGroupId++;

				item.TopLine.Junctions.ForEach(m => m.GroupId = nextGroupId);
				item.BottomLine.Junctions.ForEach(m => m.GroupId = nextGroupId);

				foreach (var junction in item.TopLine.Junctions)
				{
					DrawJunction(labels, outputImg, junction.GroupId, junction);
				}

				foreach (var junction in item.BottomLine.Junctions)
				{
					DrawJunction(labels, outputImg, junction.GroupId, junction);
				}
			}
			
			// TODO
			return true;
		}

		private static void DrawPoint(int[,] outputImg, int color, int x, int y, int size)
		{
			// Must be centered.
			x -= size / 2;
			y -= size / 2;
			
			for (int i = 0; i < size; i++)
				for (int j = 0; j < size; j++)
					outputImg[y + i, x + j] = color;
		}

		private static void DrawJunction(MatIndexer<Vec3b> labels, int[,] outputImg, int color, Junction junction)
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
			var val = GetVal(labels, y, x);

			if (top)
				for (int i = 0; i < numTop; i++)
					if (GetVal(labels, y - i, x) == val)
						outputImg[y - i, x] = color;
			if (bottom)
				for (int i = 0; i < numBottom; i++)
					if (GetVal(labels, y + i, x) == val)
						outputImg[y + i, x] = color;
			if (right)
				for (int i = 0; i < numRight; i++)
					if (GetVal(labels, y, x + i) == val)
						outputImg[y, x + i] = color;
			if (left)
				for (int i = 0; i < numLeft; i++)
					if (GetVal(labels, y, x - i) == val)
						outputImg[y, x - i] = color;
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
					int distY = Math.Abs(y - curY);

					if (distX <= 1 && distY <= 3)
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
					remainingList.Clear();
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
