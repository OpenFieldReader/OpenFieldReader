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
	if (this->DebugImg != NULL)
	{
		delete this->DebugImg;
		this->DebugImg = NULL;
	}
}

int FormExtraction::RunFormExtraction(int* imgData, int row, int col)
{
	return this->HasBoxes(imgData, row, col);
}

void FormExtraction::DrawJunction(int colorCode, Junction* junction, int row)
{
	bool top = junction->Top;
	bool bottom = junction->Bottom;
	bool right = junction->Right;
	bool left = junction->Left;
	int numTop = junction->NumTop;
	int numBottom = junction->NumBottom;
	int numRight = junction->NumRight;
	int numLeft = junction->NumLeft;
	int x = junction->X;
	int y = junction->Y;

	if (top)
		for (int i = 0; i < numTop; i++)
			this->DebugImg[y + x * row] = colorCode;
	if (bottom)
		for (int i = 0; i < numBottom; i++)
			this->DebugImg[y + x * row] = colorCode;
	if (right)
		for (int i = 0; i < numRight; i++)
			this->DebugImg[y + x * row] = colorCode;
	if (left)
		for (int i = 0; i < numLeft; i++)
			this->DebugImg[y + x * row] = colorCode;
}

int FormExtraction::HasBoxes(int* imgData, int row, int col)
{
	// Debug image.
	if (this->ShowDebugImage)
	{
		if (this->DebugImg != NULL)
		{
			// Remove previous debug img.
			delete this->DebugImg;
			this->DebugImg = NULL;
		}
		this->DebugImg = new int[row * col];
		for (int y = 0; y < row; y++)
			for (int x = 0; x < col; x++)
				this->DebugImg[y + x * row] = 0;
	}

	// We are seaching for pattern!
	// We look for junctions.
	// This will help us make a decision.
	// Junction types: T, L, +.
	// Junctions allow us to find boxes contours.

	int width = this->JunctionWidth;
	int height = this->JunctionHeight;

	// Cache per line speed up the creation of various cache.
	map<int, list<Junction*>*> cacheListJunctionPerLine;
	list<Junction*> listJunction;

	// If there is too much junction near each other, maybe it's just a black spot.
	// We must ignore it to prevent wasting CPU and spend too much time.
	int maxProximity = 10;

	list<Junction*> allJunctions;
	list<list<Junction*>*> allListJunction;

	for (int y = 1; y < row - 1; y++)
	{
		list<Junction*>* listJunctionX = new list<Junction*>();
		allListJunction.push_back(listJunctionX);

		int proximityCounter = 0;

		for (int x = 1; x < col - 1; x++)
		{
			Junction* junction = GetJunction(imgData, row, col, height, width, y, x);
			if (junction != NULL)
			{
				allJunctions.push_front(junction);
				listJunctionX->push_front(junction);
				proximityCounter++;
			}
			else
			{
				if (listJunctionX->size() > 0)
				{
					if (proximityCounter < maxProximity)
					{
						if (cacheListJunctionPerLine.find(y) == cacheListJunctionPerLine.end())
						{
							// Not found.
							auto newListJunctionX = new list<Junction*>();
							allListJunction.push_back(newListJunctionX);
							cacheListJunctionPerLine.insert(pair<int, list<Junction*>*>(y, newListJunctionX));
						}

						auto cacheListJunction = cacheListJunctionPerLine.at(y);
						
						for each (auto junction in *listJunctionX)
						{
							cacheListJunction->push_back(junction);
							listJunction.push_back(junction);
							listJunctionX = new list<Junction*>();
							allListJunction.push_back(listJunctionX);
						}
					}
					else
					{
						listJunctionX->clear();
					}
				}
				proximityCounter = 0;
			}
		}

		if (proximityCounter < maxProximity && listJunctionX != NULL)
		{
			if (cacheListJunctionPerLine.find(y) == cacheListJunctionPerLine.end())
			{
				// Not found.
				auto newListJunctionX = new list<Junction*>();
				allListJunction.push_back(newListJunctionX);
				cacheListJunctionPerLine.insert(pair<int, list<Junction*>*>(y, newListJunctionX));
			}

			for each (auto junction in *listJunctionX)
			{
				cacheListJunctionPerLine.insert(pair<int, list<Junction*>*>(y, listJunctionX));
				listJunction.push_back(junction);
				listJunctionX = new list<Junction*>();
				allListJunction.push_back(listJunctionX);
			}
		}
	}

	cout << "Junction.count: " << listJunction.size() << endl;

	if (this->ShowDebugImage)
	{
		for each (auto junction in listJunction)
		{
			this->DrawJunction(1, junction, row);
		}
	}

	// Dispose.
	for each (auto junction in allJunctions)
	{
		delete junction;
	}
	for each (auto listJunction in allListJunction)
	{
		delete listJunction;
	}

	return 0;
}

int FormExtraction::GetVal(int* imgData, int y, int x, int row)
{
	return imgData[y + (x - 1) * row] | imgData[y + x * row] | imgData[y + (x + 1) * row];
}

Junction* FormExtraction::GetJunction(int* imgData, int row, int col, int height, int width, int y, int x)
{
	int val = this->GetVal(imgData, y, x, row);
	if (0 < val)
	{
		// Let's explore the directions.

		int numTop = 0;
		if (y - height >= 1)
			for (int i = 0; i < height; i++)
				if (GetVal(imgData, y - i, x, row) == val)
					numTop++;
				else
					break;

		int numBottom = 0;
		if (y + height < row - 1)
			for (int i = 0; i < height; i++)
				if (GetVal(imgData, y + i, x, row) == val)
					numBottom++;
				else
					break;

		int numRight = 0;
		if (x + width < col - 1)
			for (int i = 0; i < width; i++)
				if (GetVal(imgData, y, x + i, row) == val)
					numRight++;
				else
					break;

		int numLeft = 0;
		if (x - width >= 1)
			for (int i = 0; i < width; i++)
				if (GetVal(imgData, y, x - i, row) == val)
					numLeft++;
				else
					break;

		bool top = numTop >= height;
		bool bottom = numBottom >= height;
		bool left = numLeft >= width;
		bool right = numRight >= width;

		if ((top || bottom) && (left || right))
		{
			Junction* junction = new Junction();
			junction->Bottom = bottom;
			junction->Left = left;
			junction->Right = right;
			junction->Top = top;
			junction->NumBottom = numBottom;
			junction->NumLeft = numLeft;
			junction->NumRight = numRight;
			junction->NumTop = numTop;
			junction->X = x;
			junction->Y = y;
			return junction;
		}
	}
	return NULL;
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

	__declspec(dllexport) int __cdecl RunFormExtraction(FormExtraction* obj, int imgData[], int row, int col)
	{
		return obj->RunFormExtraction(imgData, row, col);
	}

	__declspec(dllexport) int* __cdecl GetDebugImage(FormExtraction* obj)
	{
		return obj->DebugImg;
	}

	__declspec(dllexport) int __cdecl ReleaseFormExtraction(FormExtraction* obj)
	{
		delete obj;
		return 0;
	}
}
