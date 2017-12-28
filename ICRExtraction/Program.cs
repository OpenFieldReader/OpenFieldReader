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
			Stopwatch watch = new Stopwatch();
			watch.Start();

			var image = new Mat("form12.png", ImreadModes.GrayScale);
			
			Cv2.AdaptiveThreshold(image, image, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 7, 2);
			Cv2.BitwiseNot(image, image);

			// If it's a scan, it can fix missing ink.
			//var element = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2,2));
			//Cv2.MorphologyEx(image, image, MorphTypes.Dilate, element);

			int[,] labels;
			int nbComponents = Cv2.ConnectedComponents(
				image,
				out labels,
				PixelConnectivity.Connectivity4);

			Console.WriteLine("nbComponents: " + nbComponents);

			var row = labels.GetLength(0);
			var col = labels.GetLength(1);
			Mat newImage = new Mat(row, col, MatType.CV_8UC3);
			newImage.SetTo(Scalar.Black);
			for (int i = 1; i < nbComponents; i++)
			{
				// We must determine if it "may" be an interesting blob.
				if (Determine(labels, i))
				{
					var img = CreateImage(labels, i);
					Cv2.BitwiseOr(newImage, img, newImage);
				}
			}
			watch.Stop();
			Console.WriteLine("Duration: " + watch.Elapsed);

			using (new Window("src image", image))
			using (new Window("dst image", newImage))
			{
				Cv2.WaitKey();
			}
		}

		private static bool Determine(int[,] labels, int key)
		{
			var row = labels.GetLength(0);
			var col = labels.GetLength(1);

			int height = 5;
			int width = 20;

			List<int> listX = new List<int>();

			for (int y = 0; y < row; y++)
			{
				for (int x = 0; x < col; x++)
				{
					var val = labels[y, x];

					if (key == val)
					{
						// Let's explore the top direction.
						int numTop = 0;
						for (int yy = y; yy >= 0; yy--)
						{
							if (labels[yy, x] == val)
							{
								numTop++;
							}
							else
							{
								break;
							}
						}

						if (numTop > height)
						{
							// Let's explore the right direction.
							int numRight = 0;
							for (int xx = x; xx < col; xx++)
							{
								if (labels[y, xx] == val)
								{
									numRight++;
								}
								else
								{
									break;
								}
							}

							if (numRight > width)
							{
								if (!listX.Contains(x))
								{
									listX.Add(x);
								}
							}
							else
							{
								// Let's explore the left direction.
								int numLeft = 0;
								for (int xx = x; xx >= 0; xx--)
								{
									if (labels[y, xx] == val)
									{
										numLeft++;
									}
									else
									{
										break;
									}
								}

								if (numLeft > width)
								{
									if (!listX.Contains(x))
									{
										listX.Add(x);
									}
								}
							}
						}
					}
				}
			}

			// ListX must have greater than 3 elements. Otherwise, we waste our time.
			if (listX.Count > 3)
			{
				// Let's check the list of point.

				int maxElements = 0;
				int finalStartX = -1;
				int finalGap = -1;

				for (int iStart = 0; iStart < listX.Count; iStart++)
				{
					var x = listX[iStart];
					
					for (int iGap = 0; iGap < iStart; iGap++)
					{
						var gap = Math.Abs(listX[iGap] - x);

						// Explore if start and gap are valid.
						List<int> remainingListX = new List<int>();
						remainingListX.AddRange(listX);

						int numElements = 1;
						while (remainingListX.Any())
						{
							// Find which element is next one.
							int indexNextElement = -1;

							for (int iNext = 0; iNext < remainingListX.Count; iNext++)
							{
								int curX = remainingListX[iNext];

								int dist = Math.Abs(x + gap - curX);

								if (dist <= 3)
								{
									indexNextElement = iNext;
									numElements++;
									remainingListX.Remove(curX);
									x = curX;
									break;
								}
							}

							if (indexNextElement == -1)
							{
								// No element found.
								remainingListX.Clear();
							}
						}
						if (numElements > 2)
						{
							if (maxElements < numElements && gap > 5 /* && gap < 30*/)
							{
								maxElements = numElements;
								finalGap = gap;
								finalStartX = x;
							}
						}
					}
				}

				if (finalStartX != -1)
				{
					Console.WriteLine("Found! at: " + finalStartX + ", gap: " + finalGap + ", num: " + maxElements);
					return true;
				}
			}
			
			return false;
		}

		private static Random Random = new Random();
		private static Mat CreateImage(int[,] labels, int key)
		{
			var row = labels.GetLength(0);
			var col = labels.GetLength(1);
			var color = (122 + Random.Next(123)) << 16 | (122 + Random.Next(123)) << 8 | (122 + Random.Next(123));

			Mat newImage = new Mat(row, col, MatType.CV_8UC3);
			for (int i = 0; i < row; i++)
			{
				for (int j = 0; j < col; j++)
				{
					var val = labels[i, j];

					if (key == val)
					{
						newImage.Set(i, j, color);
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
