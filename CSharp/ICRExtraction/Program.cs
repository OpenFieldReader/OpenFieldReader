using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICRExtraction
{
	class Program
	{
		static void Main(string[] args)
		{
			var projectDir = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;
			var pathFiles = Directory.EnumerateFiles(projectDir + @"\Samples")
				//.Where(m => m.Contains("form9"))
				.ToList();
			
			int i = 0;
			object newIdLock = new object();

			var resultDir = projectDir + @"\Results";
			if (!Directory.Exists(resultDir))
			{
				Directory.CreateDirectory(resultDir);
			}

			foreach (var pathFile in Directory.EnumerateFiles(resultDir))
			{
				File.Delete(pathFile);
			}

			Parallel.ForEach(pathFiles, new ParallelOptions { MaxDegreeOfParallelism = 2 }, pathFile =>
			{
				Console.WriteLine("Processing: " + Path.GetFileNameWithoutExtension(pathFile));

				try
				{
					var filename = Path.GetFileNameWithoutExtension(pathFile);
					var result = FormExtraction.ProcessImage(pathFile);
					
					using (var image = new Mat(pathFile, ImreadModes.GrayScale))
					{
						Cv2.AdaptiveThreshold(image, image, 255, AdaptiveThresholdTypes.MeanC, ThresholdTypes.Binary, 9, 4);

						// TODO: we should not resize. (keep maximum quality)
						if (image.Width > 800)
						{
							var height = 800 * image.Height / image.Width;
							Cv2.Resize(image, image, new Size(800, height));
						}

						var numGroup = 1;

						// You may want to use ".OrderBy(m => m.Min(x => x.TopLeft.Y)).Take(1)" to select the first box on top.
						foreach (var group in result.Boxes)
						{
							Console.WriteLine("\nGroup #" + numGroup + " (" + group.Count + ")");

							int id = 0;
							lock (newIdLock)
							{
								i++;
								id = i;
							}

							int characterNum = 1;
							foreach (var box in group)
							{
								Console.WriteLine(box.TopLeft + " " + box.TopRight + "\n" + box.BottomLeft + " " + box.BottomRight + "\n");

								var xTopLeft = Math.Min(box.TopLeft.X, box.BottomLeft.X);
								var yTopLeft = Math.Min(box.TopLeft.Y, box.TopRight.Y);

								var xBottomRight = Math.Max(box.TopRight.X, box.BottomRight.X);
								var yBottomRight = Math.Max(box.BottomLeft.Y, box.BottomRight.Y);

								var estimatedWidth = xBottomRight - xTopLeft;
								var estimatedHeight = yBottomRight - yTopLeft;

								try
								{
									using (var subImg = new Mat(image, new Rect(xTopLeft, yTopLeft, estimatedWidth, estimatedHeight)))
									{
										MatOfByte3 mat3 = new MatOfByte3(subImg);
										MatIndexer<Vec3b> indexer = mat3.GetIndexer();

										int borderPixelX = 4;
										int borderPixelY = 4;
										var minY = Math.Min(borderPixelX, subImg.Height);
										var maxY = Math.Max(0, subImg.Height - borderPixelX);
										var minX = Math.Min(borderPixelX, subImg.Width);
										var maxX = Math.Max(0, subImg.Width - borderPixelY);
										
										// Basic empty box detection.
										int whitePixelCounter = 0;
										int pixelCounter = 0;
										for (int y = minY; y <= maxY; y++)
										{
											for (int x = minX; x <= maxX; x++)
											{
												var pixel = indexer[y, x].Item0; // Grayscale only

												if (pixel == 255)
												{
													whitePixelCounter++;
												}
												pixelCounter++;
											}
										}
										mat3.Dispose();

										int percentRatio = 100 * whitePixelCounter / pixelCounter;

										var outputFilename = filename + "_" + id + "_" + characterNum + "_" + percentRatio;

										// Exclude empty boxes.
										if (percentRatio < 95)
										{
											Cv2.ImWrite(resultDir + @"\" + outputFilename + ".jpg", subImg);
										}
									}
								}
								catch (Exception ex)
								{
									Console.WriteLine("Can't generate subImg: " + ex);
								}

								characterNum++;
							}
							numGroup++;
						}
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine("Something wrong happen: " + ex.Message);
					Console.WriteLine(ex.StackTrace);
				}
			});

			Console.WriteLine("End");
			Console.ReadLine();
		}
	}
}
