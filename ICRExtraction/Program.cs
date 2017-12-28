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
			
			Cv2.AdaptiveThreshold(image, image, 255, AdaptiveThresholdTypes.MeanC, ThresholdTypes.Binary, 7, 2);

			Cv2.BitwiseNot(image, image);

			Cv2.Blur(image, image, new Size(1, 2));
			Cv2.Threshold(image, image, 0, 255, ThresholdTypes.Otsu | ThresholdTypes.Binary);

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
				int[,] outputImg = null;
				if (Determine(labels, i, out outputImg))
				{
					var img = CreateImage(outputImg, i);
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

		private static bool Determine(int[,] labels, int key, out int[,] outputImg)
		{
			var row = labels.GetLength(0);
			var col = labels.GetLength(1);
			outputImg = new int[row, col];

			int height = 10;
			int width = 10;

			List<int> listX = new List<int>();
			
			for (int y = 0; y < row; y++)
			{
				for (int x = 0; x < col; x++)
				{
					var added = false;

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

						int numBottom = 0;
						for (int yy = y; yy < row; yy++)
						{
							if (labels[yy, x] == val)
							{
								numBottom++;
							}
							else
							{
								break;
							}
						}
						
						if (numTop > height || numBottom > height)
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

							int numLeft = 0;
							if (numRight > width)
							{
								added = true;
								if (!listX.Contains(x))
								{
									listX.Add(x);
								}
							}
							else
							{
								// Let's explore the left direction.
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
									added = true;
									if (!listX.Contains(x))
									{
										listX.Add(x);
									}
								}
							}

							if (added)
							{
								for (int i = 0; i < numTop; i++)
									if (labels[y - i, x] == val)
										outputImg[y - i, x] = val;
								for (int i = 0; i < numBottom; i++)
									if (labels[y + i, x] == val)
										outputImg[y + i, x] = val;
								for (int i = 0; i < numRight; i++)
									if (labels[y, x + i] == val)
										outputImg[y, x + i] = val;
								for (int i = 0; i < numLeft; i++)
									if (labels[y, x - i] == val)
										outputImg[y, x - i] = val;
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

								if (dist <= 2)
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
						if (numElements > 4)
						{
							if (maxElements < numElements && gap > 10 /* && gap < 30*/)
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
