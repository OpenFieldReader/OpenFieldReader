#pragma once

#include "Junction.h"

class FormExtraction
{
public:
	FormExtraction();
	~FormExtraction();

	int RunFormExtraction(int* imgData, int row, int col);
	void DrawJunction(int colorCode, Junction* junction, int row);
	int HasBoxes(int * imgData, int row, int col);
	int GetVal(int* imgData, int y, int x, int row);
	Junction* GetJunction(int * imgData, int row, int col, int height, int width, int y, int x);

	// Options.
	int ResizeWidth;
	int JunctionWidth;
	int JunctionHeight;
	int MinNumElements;
	int MaxJunctions;
	int MaxSolutions;
	bool ShowDebugImage;
	int* DebugImg = 0;
};

