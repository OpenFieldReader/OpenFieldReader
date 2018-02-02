using ICRExtractionCore;
using System;
using System.Collections.Generic;
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
			for (int i = 0; i < 100; i++)
			{
				using (var state = new ThreadLocal<FormExtractionHandle>(NativeFormExtraction.CreateFormExtraction))
				{
					Parallel.For(0, 100, j =>
					{
						FormExtractionHandle handle = state.Value;
						
						NativeFormExtraction.SetOptions(handle, 800, i, j, 10, 10, 10, false);
						var result = NativeFormExtraction.RunFormExtraction(handle);
						Console.WriteLine(i + "\t" + j + "\t" + result);
					});
				}
			}

			Console.WriteLine("End");
			Console.ReadLine();
		}
	}
}
