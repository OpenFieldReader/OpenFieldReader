#include "FormExtraction.h"

#include <cmath>
#include <iostream>
#include <cstring>
#include <cstdlib>
#include <string>
#include "cpplinq.hpp"

using namespace std;
using namespace cpplinq;

FormExtraction::FormExtraction()
{
}

FormExtraction::~FormExtraction()
{
}

int FormExtraction::RunFormExtraction()
{
	return 0;
}

// Define C functions for the C++ class - as ctypes can only talk to C...
extern "C"
{
	__declspec(dllexport) void* __cdecl CreateFormExtraction()
	{
		FormExtraction* obj = new FormExtraction();
		return obj;
	}

	__declspec(dllexport) void __cdecl SetOptions(
		FormExtraction* obj,
		int resizeWidth,
		int junctionWidth,
		int junctionHeight,
		int minNumElements,
		int maxJunctions,
		int maxSolutions,
		bool showDebugImage)
	{
		obj->ResizeWidth = resizeWidth;
		obj->JunctionWidth = junctionWidth;
		obj->JunctionHeight = junctionHeight;
		obj->MinNumElements = minNumElements;
		obj->MaxJunctions = maxJunctions;
		obj->MaxSolutions = maxSolutions;
		obj->ShowDebugImage = showDebugImage;
	}

	__declspec(dllexport) int __cdecl RunFormExtraction(FormExtraction* obj)
	{
		return obj->JunctionWidth + obj->JunctionHeight;
	}

	__declspec(dllexport) int __cdecl ReleaseFormExtraction(FormExtraction* obj)
	{
		delete obj;
		return 0;
	}
}
