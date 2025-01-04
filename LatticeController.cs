using Godot;
using System;
using System.Text.Json.Serialization.Metadata;
using System.Xml.Schema;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Metadata;

public static class Utils 
{
	private static Random rng = new Random();  
	public static void Shuffle<T>(this IList<T> list)  
	{  
		int n = list.Count;  
		while (n > 1) {  
			n--;  
			int k = rng.Next(n + 1);  
			T value = list[k];  
			list[k] = list[n];  
			list[n] = value;  
		}  
	}
}

public class STState
{
	public int[,] groups;
	public List<Vector4I> availableEdges;
	public List<Vector4I> pickedEdges;
	public List<Vector4I> discardedEdges;

	public void Init(int w, int h, bool symX, bool symY)
	{
		groups = new int[w, h];

		availableEdges = new List<Vector4I>();
		pickedEdges = new List<Vector4I>();
		discardedEdges = new List<Vector4I>();

		int curGroup = 0;
		if(symX || symY) curGroup = 1; // group 0 is reserved for the already picked edges
		for(int i = 0; i < w; i++)
		{
			for(int j = 0; j < h; j++)
			{
				if(symX && i == w - 1) groups[i, j] = 0;
				else if (symY && j == h -1) groups[i, j] = 0;
				else groups[i, j] = curGroup++;
			}
		}

		for(int i = 0; i < w; i++)
		{
			for(int j = 0; j < h; j++)
			{
				if(i != w - 1)
				{
					Vector4I edge = new Vector4I(i, j, i + 1, j);
					if(symY && j == h - 1)
						pickedEdges.Add(edge);
					else 
						availableEdges.Add(edge);
				}

				if(j != h - 1)
				{
					Vector4I edge = new Vector4I(i, j, i, j + 1);
					if(symX && i == w - 1)
						pickedEdges.Add(edge);
					else 
						availableEdges.Add(edge);
				}
			}
		}

		availableEdges.Shuffle();
	}

	public bool IsFinished()
	{
		return availableEdges.Count == 0;
	}

	public void ProcessNextEdge()
	{
		Vector4I edge = availableEdges[availableEdges.Count - 1];
		availableEdges.RemoveAt(availableEdges.Count - 1);
		if(CanPickEdge(edge))
			PickEdge(edge);
		else
			DiscardEdge(edge);
	}

	public bool CanPickEdge(Vector4I edge)
	{
		int group = groups[edge.X, edge.Y];
		int otherGroup = groups[edge.Z, edge.W];
		return group != otherGroup;
	}


	public void PickEdge(Vector4I edge)
	{
		int group = groups[edge.X, edge.Y];
		int otherGroup = groups[edge.Z, edge.W];

		if(group > otherGroup) 
		{
			int tmp = otherGroup;
			otherGroup = group;
			group = tmp;
		}

		for(int i = 0; i < groups.GetLength(0); i++)
		{
			for(int j = 0; j < groups.GetLength(1); j++)
			{
				if(groups[i, j] == otherGroup) groups[i, j] = group;
			}
		}
		pickedEdges.Add(edge);
	}

	public void DiscardEdge(Vector4I edge)
	{
		discardedEdges.Add(edge);
	}
}

public partial class LatticeController : Node2D
{
	[Export] int width = 3, height = 5;
	[Export] bool symX = true, symY = false;
	[Export] float cellSize = 50;
	[Export] float lineWidth = 10;
	[Export] float outlineWidth = 12;
	[Export] CameraController camera;
	[Export] Font font;

	Color outlineColor = Colors.Black;
	Color foregroundColor = new Color(178 / 255f, 0 / 255f, 0 / 255f);
	Color backgroundColor = new Color(255 / 255f, 232 / 255f, 165 / 255f);

	STState state;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		camera.Position = new Vector2(0,0);
		state = new STState();
		state.Init(width, height, symX, symY);
		CreateTiles();
		QueueRedraw();
	}

	public void GenerateNew()
	{
		state.Init(width, height, symX, symY);
		while(!state.IsFinished()) state.ProcessNextEdge();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if(Input.IsActionJustPressed("advance") && !state.IsFinished())
		{
			state.ProcessNextEdge();
			UpdateTiles();
			QueueRedraw();
		}
		if(Input.IsActionJustPressed("reset"))
		{
			state.Init(width, height, symX, symY);
			UpdateTiles();
			QueueRedraw();
		}
		if(Input.IsActionJustPressed("new"))
		{
			GenerateNew();
			UpdateTiles();
			QueueRedraw();
		}
	}

	public Vector2 GetMin()
	{
		float minX = - (width / 2f) * cellSize;
		float minY = - (height / 2f) * cellSize;
		return new Vector2(minX, minY);
	}

	public Vector2 GetRenderingOffset(int totalWidth, int totalHeight)
	{
		return new Vector2(1, 0) * (totalWidth * cellSize + 50);
	}

	public override void _Draw()
	{
		for(int xx = 0; xx < width; xx++)
		{
			for(int yy = 0; yy < height; yy++)
			{
				DrawRect(new Rect2(GetNodeCenter(xx, yy) - Vector2.One * cellSize * 0.5f, Vector2.One * cellSize), Colors.Black, false, lineWidth);
			}
		}

		foreach(Vector4I edge in state.pickedEdges)
		{
			DrawLine(GetNodeCenter(edge.X, edge.Y), GetNodeCenter(edge.Z, edge.W), Colors.Green, lineWidth / 2, true);
		}

		if(!state.IsFinished())
		{
			foreach(Vector4I edge in state.availableEdges)
			{
				DrawLine(GetNodeCenter(edge.X, edge.Y), GetNodeCenter(edge.Z, edge.W), Colors.Yellow, lineWidth / 4, true);
			}

			foreach(Vector4I edge in state.discardedEdges)
			{
				DrawLine(GetNodeCenter(edge.X, edge.Y), GetNodeCenter(edge.Z, edge.W), Colors.Red, lineWidth / 4, true);
			}
		}

		for(int i = 0; i < width; i++)
		{
			for(int j = 0; j < height; j++)
			{
				// DrawString(font, GetNodeCenter(i, j), state.groups[i, j].ToString(), HorizontalAlignment.Center);
			}
		}

		DrawFrame();
	}

	void DrawFrame()
	{
		RenderingMotif[,] motifs = GenerateRenderingPattern(state, symX, symY);
		int w = motifs.GetLength(0), h = motifs.GetLength(1);

		Vector2 offset = GetRenderingOffset(width, height) - new Vector2(0.5f, 0.5f) * cellSize + GetNodeCenter(0,0);
		Rect2 rect = new Rect2(offset, new Vector2(w, h) * cellSize);
		
		float framePadding = 0.4f;
		Rect2 outerRect = new Rect2( - new Vector2(framePadding / 2, framePadding / 2) * cellSize + offset, new Vector2(w + framePadding, h + framePadding) * cellSize);
		DrawRect(outerRect, backgroundColor, true, outlineWidth);
		DrawRect(outerRect, outlineColor, false, outlineWidth);
		DrawRect(outerRect, foregroundColor, false, lineWidth);

		DrawRect(rect, outlineColor, false, outlineWidth);
		DrawRect(rect, foregroundColor, false, lineWidth);
	}

	Vector2 GetNodeCenter(int xx, int yy)
	{
		return GetMin() + new Vector2(xx + 0.5f, yy + 0.5f) * cellSize;
	}

	const int LEFT = 0;
	const int UP = 1;
	const int RIGHT = 2;
	const int DOWN = 3;

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

	LatticeTile[,] redTiles;
	LatticeTile[,] blackTiles;
	void CreateTiles()
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

	void UpdateTiles()
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
	}
}

public class RenderingMotif
{
	//lurd
	bool[] connections = new bool[4]{ false, false, false, false};

	public void SetConnection(int idx, bool state)
	{
		connections[idx] = state;
	}

	public bool HasConnection(int idx)
	{
		return connections[idx];
	}

	public string ToTileString()
	{
		string str = "";
		for(int i = 0; i < 4; i++)
		{
			str += connections[i] ? "1" : "0";
		}
		return str;
	}
}

