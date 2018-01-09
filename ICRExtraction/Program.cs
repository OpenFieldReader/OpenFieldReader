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
			
			foreach (var pathFile in pathFiles.Where(m => m.Contains("form9")))
			{
				Console.WriteLine("Processing: " + Path.GetFileNameWithoutExtension(pathFile));

				try
				{
					GC.Collect();
					FormExtraction.ProcessImage(pathFile);
				}
				catch (Exception ex)
				{
					Console.WriteLine("Something wrong happen: " + ex.Message);
				}
			}

			Console.WriteLine("End");
			Console.ReadLine();
		}
	}
}
