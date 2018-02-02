#pragma once

class FormExtraction
{
public:
	FormExtraction();
	~FormExtraction();
	int RunFormExtraction();

	// Options.
	int ResizeWidth;
	int JunctionWidth;
	int JunctionHeight;
	int MinNumElements;
	int MaxJunctions;
	int MaxSolutions;
	bool ShowDebugImage;
};

