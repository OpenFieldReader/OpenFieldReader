#pragma once
class Junction
{
public:
	Junction();
	~Junction();

	bool Bottom;
	bool Left;
	bool Right;
	bool Top;
	int NumBottom;
	int NumLeft;
	int NumRight;
	int NumTop;
	int X;
	int Y;
};

