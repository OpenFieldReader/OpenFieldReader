using ICRExtraction;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ICRExtractionConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
			var projectDir = Directory.GetParent(Directory.GetCurrentDirectory()).FullName;
			var pathFiles = Directory.EnumerateFiles(
				projectDir + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + "Samples")
				.ToList();

			var resultDir = projectDir + Path.DirectorySeparatorChar + "Results";
			if (!Directory.Exists(resultDir))
			{
				Directory.CreateDirectory(resultDir);
			}
			foreach (var pathFile in Directory.EnumerateFiles(resultDir))
			{
				File.Delete(pathFile);
			}

			foreach (var pathFile in pathFiles)
			{
				FormExtraction.ExtractCharacters(
					pathFile,
					resultDir,
					removeEmptyBoxes: true);
			}
			
			Console.WriteLine("End");
			Console.ReadLine();
		}
    }
}
