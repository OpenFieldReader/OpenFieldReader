using CommandLine;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Utf8Json;

namespace OpenFieldReader
{
	class Program
    {
        static void Main(string[] args)
		{
			Parser.Default.ParseArguments<OpenFieldReaderOptions>(args)
				.WithParsed(opts => Run(opts))
				.WithNotParsed(errs => HandleErrors(errs));
		}

		private static object HandleErrors(IEnumerable<Error> errs)
		{
			Environment.Exit(1);
			return 1;
		}
		
		private static void Run(OpenFieldReaderOptions options)
		{
			try
			{
				using (var image = Image.Load(options.InputFile))
				{
					try
					{
						int row = image.Height;
						int col = image.Width;
						
						int[] imgData = new int[row * col];
						for (int y = 0; y < row; y++)
						{
							for (int x = 0; x < col; x++)
							{
								var pixel = image[x, y];
								var val = pixel.R | pixel.G | pixel.B;
								imgData[y + x * row] = val > 122 ? 0 : 255;
							}
						}

						var result = OpenFieldReader.FindBoxes(imgData, row, col, options);

						if (result.ReturnCode != 0)
						{
							if (options.Verbose) {
								Console.WriteLine("Exit with code: " + result.ReturnCode);
							}
							Environment.Exit(result.ReturnCode);
						}

						if (options.OutputFile == "std")
						{
							// Show result on the console.

							Console.WriteLine("Boxes: " + result.Boxes.Count);
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
							Console.WriteLine("Press any key to continue...");
							Console.ReadLine();
						}
						else
						{
							// Write result to output file.
							var outputPath = options.OutputFile;
							var json = JsonSerializer.ToJsonString(result);
							File.WriteAllText(outputPath, json);
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine("File: " + options.InputFile);
						Console.WriteLine("Something wrong happen: " + ex.Message + Environment.NewLine + ex.StackTrace);
						Environment.Exit(3);
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("File: " + options.InputFile);
				Console.WriteLine("Something wrong happen: " + ex.Message + Environment.NewLine + ex.StackTrace);
				Environment.Exit(2);
			}
		}
	}
}
