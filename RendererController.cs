using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

public partial class RendererController : Node2D
{
	[Export] Camera2D camera;
	[Export] Control targetRect;
	[Export] TextureRect textureRect;
	[Export] SubViewport viewport;

	[Export] int lineWidth = 10;
	[Export] int outlineWidth = 12;
	[Export] int framePadding = 20; 
	Color outlineColor = Colors.Black;
	Color foregroundColor = new Color(178 / 255f, 0 / 255f, 0 / 255f);
	Color backgroundColor = new Color(255 / 255f, 232 / 255f, 165 / 255f);


	int width, height;
	int cellSize = 50;

	const int LEFT = 0;
	const int UP = 1;
	const int RIGHT = 2;
	const int DOWN = 3;
	
	Dictionary<string, Vector2I> motifToTile = new Dictionary<string, Vector2I>
	{
		{ "0000", new Vector2I(0,0) },

		{ "1000", new Vector2I(1,0) },
		{ "0100", new Vector2I(1,1) },
		{ "0010", new Vector2I(1,2) },
		{ "0001", new Vector2I(1,3) },
		
		{ "1100", new Vector2I(2,0) },
		{ "0110", new Vector2I(2,1) },
		{ "0011", new Vector2I(2,2) },
		{ "1001", new Vector2I(2,3) },

		{ "1010", new Vector2I(3,0) },
		{ "0101", new Vector2I(3,1) },
		
		{ "1011", new Vector2I(4,0) },
		{ "1101", new Vector2I(4,1) },
		{ "1110", new Vector2I(4,2) },
		{ "0111", new Vector2I(4,3) },

		{ "1111", new Vector2I(5,0) },
	};

	static int ReflectX(int xx, int width)
	{
		return width - 1 - xx;
	}

	static int ReflectY(int yy, int height)
	{
		return height - 1 - yy;
	}

	static RenderingMotif[,] GenerateRenderingPattern(STState state, bool symX, bool symY)
	{
		int totalWidth = state.groups.GetLength(0);
		if(symX) totalWidth = totalWidth * 2 - 1;
		int totalHeight = state.groups.GetLength(1);
		if(symY) totalHeight = totalHeight * 2 - 1;

		RenderingMotif[,] motifs = new RenderingMotif[totalWidth, totalHeight];
		for(int i = 0; i < totalWidth; i++)
		{
			for(int j = 0; j < totalHeight; j++)
			{
				motifs[i, j] = new RenderingMotif();
			}
		}

		foreach(Vector4I edge in state.pickedEdges)
		{
			if(edge.X != edge.Z)
			{
				motifs[edge.X, edge.Y].SetConnection(RIGHT, true);
				motifs[edge.Z, edge.W].SetConnection(LEFT, true);

				if(symX)
				{
					motifs[ReflectX(edge.X, totalWidth), edge.Y].SetConnection(LEFT, true);
					motifs[ReflectX(edge.Z, totalWidth), edge.W].SetConnection(RIGHT, true);
				}

				if(symY)
				{
					motifs[edge.X, ReflectY(edge.Y, totalHeight)].SetConnection(RIGHT, true);
					motifs[edge.Z, ReflectY(edge.W, totalHeight)].SetConnection(LEFT, true);
				}

				if(symY && symX)
				{
					motifs[ReflectX(edge.X, totalWidth), ReflectY(edge.Y, totalHeight)].SetConnection(LEFT, true);
					motifs[ReflectX(edge.Z, totalWidth), ReflectY(edge.W, totalHeight)].SetConnection(RIGHT, true);
				}
			}
			else if(edge.Y != edge.W)
			{
				motifs[edge.X, edge.Y].SetConnection(UP, true);
				motifs[edge.Z, edge.W].SetConnection(DOWN, true);

				if(symX)
				{
					motifs[ReflectX(edge.X, totalWidth), edge.Y].SetConnection(UP, true);
					motifs[ReflectX(edge.Z, totalWidth), edge.W].SetConnection(DOWN, true);
				}

				if(symY)
				{
					motifs[edge.X, ReflectY(edge.Y, totalHeight)].SetConnection(DOWN, true);
					motifs[edge.Z, ReflectY(edge.W, totalHeight)].SetConnection(UP, true);
				}

				if(symY && symX)
				{
					motifs[ReflectX(edge.X, totalWidth), ReflectY(edge.Y, totalHeight)].SetConnection(DOWN, true);
					motifs[ReflectX(edge.Z, totalWidth), ReflectY(edge.W, totalHeight)].SetConnection(UP, true);
				}
			}
			else
			{
				Debug.Assert(false, "IMPOSSIBLE EDGE");
			}
		}
		
		return motifs;
	}

	Vector2 GetRenderingOffset(int w, int h)
	{
		return Vector2.Zero;
	}

	Vector2 GetNodeCenter(int xx, int yy)
	{
		return GetMin() + new Vector2(xx + 0.5f, yy + 0.5f) * cellSize;
	}

	public Vector2 GetMin()
	{
		float minX = - (width / 2f) * cellSize;
		float minY = - (height / 2f) * cellSize;
		return new Vector2(minX, minY);
	}

	LatticeTile[,] redTiles;
	LatticeTile[,] blackTiles;
	public void CreateTiles(STState state, bool symX, bool symY)
	{
		static LatticeTile create_tile(LatticeTile[,] tiles, Vector2I motifTile, int xx, int yy, Vector2 pos, float size, float width, Color color, int zindex)
		{
			LatticeTile tile = new LatticeTile();
			tile.Position = pos;
			tile.ZIndex = zindex;
			tiles[xx, yy] = tile;
			
			tile.SetMotif(motifTile.X, motifTile.Y * 90);
			tile.SetupRenderingVariables(size, width, color);
			return tile;
		}

		RenderingMotif[,] motifs = GenerateRenderingPattern(state, symX, symY);
		int w = motifs.GetLength(0), h = motifs.GetLength(1);
		width = w;
		height = h;
		redTiles = new LatticeTile[w, h];
		blackTiles = new LatticeTile[w, h];
		for(int i = 0; i < w; i++)
		{
			for(int j = 0; j < h; j++)
			{
				Vector2I motifTile = motifToTile[motifs[i, j].ToTileString()];
				Vector2 offset = GetRenderingOffset(width, height);
				Vector2 pos = GetNodeCenter(i, j) + offset;
				AddChild(create_tile(blackTiles, motifTile, i, j, pos, cellSize, outlineWidth, outlineColor, 0));
				AddChild(create_tile(redTiles, motifTile, i, j, pos, cellSize, lineWidth, foregroundColor, 1));
			}
		}
	}

	public void UpdateTiles(STState state, bool symX, bool symY)
	{
		RenderingMotif[,] motifs = GenerateRenderingPattern(state, symX, symY);
		int w = motifs.GetLength(0), h = motifs.GetLength(1);
		for(int i = 0; i < w; i++)
		{
			for(int j = 0; j < h; j++)
			{
				Vector2I motifTile = motifToTile[motifs[i, j].ToTileString()];
				redTiles[i,j].SetMotif(motifTile.X, motifTile.Y * 90);
				redTiles[i,j].QueueRedraw();

				blackTiles[i,j].SetMotif(motifTile.X, motifTile.Y * 90);
				blackTiles[i,j].QueueRedraw();
			}
		}

		width = w;
		height = h;

		UpdateRendering();
	}

	void UpdateRendering()
	{
		Vector2I size = new Vector2I(width, height) * cellSize;
		Vector2I padding = Vector2I.One * framePadding;

		size += new Vector2I(1, 1) * outlineWidth;
		size += padding;

		Vector2I textureSize = size;
		targetRect.Size = textureSize;
		viewport.Size = textureSize;
		camera.Zoom = Vector2.One;
		// camera.Position = new Vector2(0, outlineWidth * 0.5f);
		textureRect.Size = textureSize;
		GD.Print(targetRect.Size);
	}

	void DrawFrame()
	{
		Vector2 offset = GetRenderingOffset(width, height) - new Vector2(0.5f, 0.5f) * cellSize + GetNodeCenter(0,0);
		Rect2 rect = new Rect2(offset, new Vector2(width, height) * cellSize);
		Rect2 outerRect = new Rect2( - Vector2.One * framePadding / 2f + offset, new Vector2(width, height) * cellSize + Vector2.One * framePadding);
		DrawRect(outerRect, backgroundColor, true);
		DrawRect(outerRect, outlineColor, false, outlineWidth);
		DrawRect(outerRect, foregroundColor, false, lineWidth);

		DrawRect(rect, outlineColor, false, outlineWidth);
		DrawRect(rect, foregroundColor, false, lineWidth);
	}

	void ExportToPNG()
	{
		ViewportTexture texture = viewport.GetTexture();
		Image image = texture.GetImage();
		// image.Resize(image.GetWidth(), image.GetHeight(), Image.Interpolation.Nearest);

		// Save the image as a PNG
		string save_path = "user://exported_viewport.png";
		Error error = image.SavePng(save_path);
		if(error == Error.Ok)
			GD.Print("Viewport exported successfully to: ", save_path);
		else
			GD.Print("Failed to save image. Error code: ", error);
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		QueueRedraw();
		if(Input.IsActionJustPressed("screenshot")) ExportToPNG();
	}

    public override void _Draw()
    {
        base._Draw();
		DrawFrame();
	}
}
