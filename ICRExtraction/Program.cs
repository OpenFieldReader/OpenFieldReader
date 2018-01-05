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
			var pathFiles = Directory.EnumerateFiles(projectDir + @"\Samples").ToList();

			foreach (var pathFile in pathFiles)
			{
				Console.WriteLine("Processing: " + Path.GetFileNameWithoutExtension(pathFile));

				try
				{
					GC.Collect();
					FormExtraction.ProcessImage(pathFile);
				}
				catch
				{
					Console.WriteLine("Something wrong happen..");
				}
			}
			Console.WriteLine("End");
			Console.ReadLine();
		}
	}
}
