using System;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;

namespace ICRExtractionCore
{
	public class NativeFormExtraction
	{
		private const string DllFilePath = "ICRExtractionCpp.dll";

		[DllImport(DllFilePath, CallingConvention = CallingConvention.Cdecl)]
		public extern static FormExtractionHandle CreateFormExtraction();

		[DllImport(DllFilePath, CallingConvention = CallingConvention.Cdecl)]
		public extern static void SetOptions(
			FormExtractionHandle obj,
			int resizeWidth,
			int junctionWidth,
			int junctionHeight,
			int minNumElements,
			int maxJunctions,
			int maxSolutions,
			bool showDebugImage);

		[DllImport(DllFilePath, CallingConvention = CallingConvention.Cdecl)]
		public extern static int RunFormExtraction(FormExtractionHandle obj, int[] imgData, int row, int col);

		[DllImport(DllFilePath, CallingConvention = CallingConvention.Cdecl)]
		private extern static IntPtr GetDebugImage(FormExtractionHandle obj);

		public static int[] GetDebugImage(FormExtractionHandle obj, int size)
		{
			int[] destination = new int[size];
			Marshal.Copy(GetDebugImage(obj), destination, 0, size);
			return destination;
		}

		[DllImport(DllFilePath, CallingConvention = CallingConvention.Cdecl)]
		public extern static int ReleaseFormExtraction(IntPtr obj);
	}

	[SecurityPermission(SecurityAction.InheritanceDemand, UnmanagedCode = true)]
	[SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
	public class FormExtractionHandle : SafeHandle
	{
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
		public FormExtractionHandle()
			: base(IntPtr.Zero, true)
		{
		}

		public override bool IsInvalid => handle == IntPtr.Zero;

		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
		protected override bool ReleaseHandle()
		{
			return NativeFormExtraction.ReleaseFormExtraction(handle) == 0;
		}
	}
}
