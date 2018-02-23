using CommandLine;
using ICRExtraction;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FindBoxes
{
	class Program
    {
        static void Main(string[] args)
		{
			Parser.Default.ParseArguments<FormExtractionOptions>(args)
				.WithParsed(opts => Run(opts))
				.WithNotParsed(errs => HandleErrors(errs));
		}

		private static object HandleErrors(IEnumerable<Error> errs)
		{
			Environment.Exit(1);
			return 1;
		}
		
		private static void Run(FormExtractionOptions options)
		{
			try
			{
				using (Image<Rgba32> image = Image.Load<Rgba32>(options.InputFile))
				{
					int row = image.Width;
					int col = image.Height;
					int[] imgData = new int[row * col];
					for (int y = 0; y < row; y++)
					{
						for (int x = 0; x < col; x++)
						{
							int val = image[x, y].R | image[x, y].B | image[x, y].G;
							imgData[y + x * row] = val < 122 ? 0 : 255;
						}
					}

					var result = FormExtraction.FindBoxes(imgData, row, col, options);

					if (result.ReturnCode != 0)
					{
						Environment.Exit(result.ReturnCode);
					}
					
					if (options.Output == "std")
					{
						// Show result on the console.

						Console.WriteLine("Boxes: " + result.Boxes.Count);
						Console.WriteLine("Duration: " + result.Duration);
						Console.WriteLine();

						int iBox = 1;
						foreach (var box in result.Boxes)
						{
							Console.WriteLine("Box #" + iBox);

							foreach (var element in box)
							{
								Console.WriteLine("  Element: " +
									element.TopLeft + "; " +
									element.TopRight + "; " +
									element.BottomRight + "; " +
									element.BottomLeft);
							}

							iBox++;
						}
					}
					else
					{
						// Write result to output file.
						var outputPath = options.Output;

						throw new NotImplementedException();
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("Something wrong happen: " + ex.Message + Environment.NewLine + ex.StackTrace);
			}
		}
	}
}
