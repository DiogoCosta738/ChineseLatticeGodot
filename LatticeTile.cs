using Godot;
using System;

public partial class LatticeTile : Node2D
{
	float cellSize;
	float lineWidth;
	Color color;

	int motif = 0;
	int angle = 0;

	public void SetupRenderingVariables(float size, float width, Color col)
	{
		cellSize = size;
		lineWidth = width;
		color = col;
		QueueRedraw();
	}

	public void SetMotif(int motif, int angle)
	{
		this.motif = motif;
		this.angle = angle;
		RotationDegrees = -angle;
		QueueRedraw();
	}

    public override void _Draw()
    {
		DrawTemplate();
		switch(motif)
		{
			case 1:
				DrawEndpoint();
				break;
			case 2:
				DrawCorner();
				break;
			case 3:
				DrawBridge();
				break;
			case 4:
				DrawT();
				break;
			case 5:
				DrawCross();
				break;
			default:
				break;
		}
	}

	void DrawTemplate()
	{
		// outer rect
		DrawLine(
				new Vector2(-1, -1) * cellSize / 2 + new Vector2(0, lineWidth / 4), 
				new Vector2( 1, -1) * cellSize / 2 + new Vector2(0, lineWidth / 4),
				color, lineWidth / 2);
		DrawLine(
				new Vector2(-1, 1) * cellSize / 2 - new Vector2(0, lineWidth / 4), 
				new Vector2( 1, 1) * cellSize / 2 - new Vector2(0, lineWidth / 4),
				color, lineWidth / 2);
		DrawLine(
				new Vector2(-1, -1) * cellSize / 2 + new Vector2(lineWidth / 4, 0), 
				new Vector2(-1,  1) * cellSize / 2 + new Vector2(lineWidth / 4, 0),
				color, lineWidth / 2);
		DrawLine(
				new Vector2(1, -1) * cellSize / 2 - new Vector2(lineWidth / 4, 0), 
				new Vector2(1,  1) * cellSize / 2 - new Vector2(lineWidth / 4, 0),
				color, lineWidth / 2);

		// cross in the middle
		DrawLine(
				new Vector2(0, -1) * cellSize / 2, 
				new Vector2(0,  1) * cellSize / 2,
				color, lineWidth);
		DrawLine(
				new Vector2(-1, 0) * cellSize / 2, 
				new Vector2( 1, 0) * cellSize / 2,
				color, lineWidth);
	}

	void DrawEndpoint()
	{
		DrawLine(
			new Vector2(-1, 0.5f) * cellSize / 2, 
			new Vector2(0.5f, 0.5f) * cellSize / 2 + new Vector2(lineWidth / 2, 0),
			color, lineWidth);

		DrawLine(
			new Vector2(-1, -0.5f) * cellSize / 2, 
			new Vector2(0.5f, -0.5f) * cellSize / 2 + new Vector2(lineWidth / 2, 0),
			color, lineWidth);

		DrawLine(
			new Vector2(0.5f, -0.5f) * cellSize / 2 - new Vector2(0, lineWidth / 2), 
			new Vector2(0.5f, 0.5f) * cellSize / 2 + new Vector2(0, lineWidth / 2),
			color, lineWidth);
	}

	void DrawCorner()
	{
		// top left
		DrawLine(
			new Vector2(-1, 0.5f) * cellSize / 2, 
			new Vector2(-0.5f, 0.5f) * cellSize / 2 + new Vector2(lineWidth / 2, 0),
			color, lineWidth);
		DrawLine(
			new Vector2(-0.5f, 0.5f) * cellSize / 2 - new Vector2(0, lineWidth / 2), 
			new Vector2(-0.5f, 1) * cellSize / 2,
			color, lineWidth);

		// bottom bar
		DrawLine(
			new Vector2(-1, -0.5f) * cellSize / 2, 
			new Vector2(0.5f, -0.5f) * cellSize / 2 + new Vector2(lineWidth / 2, 0),
			color, lineWidth);
		
		// right bar
		DrawLine(
			new Vector2(0.5f, -0.5f) * cellSize / 2, 
			new Vector2(0.5f, 1) * cellSize / 2 + new Vector2(0, lineWidth / 2),
			color, lineWidth);
		
	}

	void DrawBridge()
	{
		// top bar
		DrawLine(
			new Vector2(-1, 0.5f) * cellSize / 2, 
			new Vector2(1, 0.5f) * cellSize / 2,
			color, lineWidth);

		// bottom bar
		DrawLine(
			new Vector2(-1, -0.5f) * cellSize / 2, 
			new Vector2(1, -0.5f) * cellSize / 2,
			color, lineWidth);
	}

	void DrawT()
	{
		// top bar
		DrawLine(
			new Vector2(-1, 0.5f) * cellSize / 2, 
			new Vector2(1, 0.5f) * cellSize / 2,
			color, lineWidth);

		// bottom left
		DrawLine(
			new Vector2(-1, -0.5f) * cellSize / 2, 
			new Vector2(-0.5f, -0.5f) * cellSize / 2 + new Vector2(lineWidth / 2, 0),
			color, lineWidth);
		DrawLine(
			new Vector2(-0.5f, -0.5f) * cellSize / 2 - new Vector2(0, lineWidth / 2), 
			new Vector2(-0.5f, -1) * cellSize / 2,
			color, lineWidth);

		// bottom right
		DrawLine(
			new Vector2(0.5f, -0.5f) * cellSize / 2, 
			new Vector2(1, -0.5f) * cellSize / 2 + new Vector2(lineWidth / 2, 0),
			color, lineWidth);
		DrawLine(
			new Vector2(0.5f, -0.5f) * cellSize / 2 + new Vector2(0, lineWidth / 2), 
			new Vector2(0.5f, -1) * cellSize / 2,
			color, lineWidth);
	}

	void DrawCross()
	{
		// top left
		DrawLine(
			new Vector2(-1, 0.5f) * cellSize / 2, 
			new Vector2(-0.5f, 0.5f) * cellSize / 2 + new Vector2(lineWidth / 2, 0),
			color, lineWidth);
		DrawLine(
			new Vector2(-0.5f, 0.5f) * cellSize / 2 - new Vector2(0, lineWidth / 2), 
			new Vector2(-0.5f, 1) * cellSize / 2,
			color, lineWidth);
		
		// top right
		DrawLine(
			new Vector2(0.5f, 0.5f) * cellSize / 2, 
			new Vector2(1, 0.5f) * cellSize / 2 + new Vector2(lineWidth / 2, 0),
			color, lineWidth);
		DrawLine(
			new Vector2(0.5f, 0.5f) * cellSize / 2 - new Vector2(0, lineWidth / 2), 
			new Vector2(0.5f, 1) * cellSize / 2,
			color, lineWidth);

		// bottom left
		DrawLine(
			new Vector2(-1, -0.5f) * cellSize / 2, 
			new Vector2(-0.5f, -0.5f) * cellSize / 2 + new Vector2(lineWidth / 2, 0),
			color, lineWidth);
		DrawLine(
			new Vector2(-0.5f, -0.5f) * cellSize / 2 - new Vector2(0, lineWidth / 2), 
			new Vector2(-0.5f, -1) * cellSize / 2,
			color, lineWidth);

		// bottom right
		DrawLine(
			new Vector2(0.5f, -0.5f) * cellSize / 2, 
			new Vector2(1, -0.5f) * cellSize / 2 + new Vector2(lineWidth / 2, 0),
			color, lineWidth);
		DrawLine(
			new Vector2(0.5f, -0.5f) * cellSize / 2 + new Vector2(0, lineWidth / 2), 
			new Vector2(0.5f, -1) * cellSize / 2,
			color, lineWidth);
	}
}
