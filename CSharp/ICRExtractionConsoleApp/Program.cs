using ICRExtractionCore;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ICRExtractionConsoleApp
{
	class Program
	{
		static void Main(string[] args)
		{
			// Used to check memory leak
			//for (int i = 0; i < 1000; i++)
			using (var state = new ThreadLocal<FormExtractionHandle>(NativeFormExtraction.CreateFormExtraction))
			{
				GC.Collect();
				List<string> pathFiles = GetSamplesAndCleanUpResults();

				// For testing:
				pathFiles = pathFiles.Where(m => m.Contains("form9")).ToList();

				int numThread = 1; // Environment.ProcessorCount;
				var showDebugImage = true; // If true, you may want to use: numThread = 1.

				Parallel.ForEach(pathFiles, new ParallelOptions { MaxDegreeOfParallelism = numThread }, pathFile =>
				{
					FormExtractionHandle handle = state.Value;

					NativeFormExtraction.SetOptions(handle, 800, 25, 15, 5, 20000, 50000, showDebugImage);

					var resizeWidth = 800;
					var orig = new Mat(pathFile);
					var image = new Mat(pathFile, ImreadModes.GrayScale);

					Cv2.AdaptiveThreshold(image, image, 255, AdaptiveThresholdTypes.MeanC, ThresholdTypes.Binary, 9, 4);

					// Resize image if too large.
					if (image.Width > resizeWidth)
					{
						var height = resizeWidth * image.Height / image.Width;
						Cv2.Resize(image, image, new Size(resizeWidth, height));
					}

					Cv2.BitwiseNot(image, image);
					Cv2.Dilate(image, image, Cv2.GetStructuringElement(MorphShapes.Cross, new Size(2, 2)));

					MatOfByte mat = new MatOfByte(image);
					MatIndexer<byte> indexer = mat.GetIndexer();

					var row = image.Height;
					var col = image.Width;
					Mat newImage = new Mat(row, col, MatType.CV_8UC3);
					newImage.SetTo(Scalar.Black);

					// We must determine if it "may" be an interesting blob.
					Stopwatch watch = new Stopwatch();
					watch.Start();

					int[] imgData = new int[row * col];
					for (int y = 0; y < row; y++)
						for (int x = 0; x < col; x++)
							imgData[y + x * row] = indexer[y, x];

					var result = NativeFormExtraction.RunFormExtraction(handle, imgData, row, col);
					watch.Stop();
					Console.WriteLine("Duration: " + watch.Elapsed);

					if (showDebugImage)
					{
						var debugImg = NativeFormExtraction.GetDebugImage(handle, row * col);

						var img = CreateImage(debugImg, row, col, hasColor: true);
						Cv2.BitwiseOr(newImage, img, newImage);

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
					mat.Dispose();
				});
			}

			Console.WriteLine("End");
			Console.ReadLine();
		}

		private static List<string> GetSamplesAndCleanUpResults()
		{
			var projectDir = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;
			var pathFiles = Directory.EnumerateFiles(
				projectDir + Path.DirectorySeparatorChar + @"..\.." + Path.DirectorySeparatorChar + "Samples")
				.ToList();
			var resultDir = projectDir + Path.DirectorySeparatorChar + @"..\Results";
			if (!Directory.Exists(resultDir))
			{
				Directory.CreateDirectory(resultDir);
			}
			foreach (var pathFile in Directory.EnumerateFiles(resultDir))
			{
				File.Delete(pathFile);
			}
			return pathFiles;
		}

		private static Random Random = new Random();
		private static Mat CreateImage(int[] labels, int row, int col, bool hasColor)
		{
			Dictionary<int, int> cacheColor = new Dictionary<int, int>();
			int color = 0;

			Mat newImage = new Mat(row, col, MatType.CV_8UC3);
			for (int y = 0; y < row; y++)
			{
				for (int x = 0; x < col; x++)
				{
					var val = labels[y + x * row];

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
							newImage.Set(y, x, color);
						}
						else
						{
							newImage.Set(y, x, 0xFFFFFF);
						}
					}
					else
					{
						newImage.Set(y, x, 0);
					}
				}
			}
			return newImage;
		}
	}
}
