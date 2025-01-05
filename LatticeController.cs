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
	[Export] float lineWidth = 5;
	[Export] CameraController camera;
	[Export] Font font;
	[Export] RendererController renderer;

	STState state;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		camera.Position = new Vector2(0,0);
		state = new STState();
		state.Init(width, height, symX, symY);
		renderer.CreateTiles(state, symX, symY);
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
			renderer.UpdateTiles(state, symX, symY);
			QueueRedraw();
		}
		if(Input.IsActionJustPressed("reset"))
		{
			state.Init(width, height, symX, symY);
			renderer.UpdateTiles(state, symX, symY);
			QueueRedraw();
		}
		if(Input.IsActionJustPressed("new"))
		{
			GenerateNew();
			renderer.UpdateTiles(state, symX, symY);
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
	}

	Vector2 GetNodeCenter(int xx, int yy)
	{
		return GetMin() + new Vector2(xx + 0.5f, yy + 0.5f) * cellSize;
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

