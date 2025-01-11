using Godot;
using System;
using System.Text.Json.Serialization.Metadata;
using System.Xml.Schema;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.ComponentModel.DataAnnotations;
using System.Linq;

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

	public void RecomputeState()
	{
		int count = 1;
		for(int i = 0; i < groups.GetLength(0); i++)
		{
			for(int j = 0; j < groups.GetLength(1); j++)
			{
				groups[i, j] = count++;
			}
		}
		
		List<Vector4I> prevPicked = pickedEdges;
		pickedEdges = new List<Vector4I>();
		foreach(var edge in prevPicked) PickEdge(edge);

		foreach(var edge in discardedEdges) availableEdges.Add(edge);
		discardedEdges.Clear();
		DiscardEdges();
	}

	public void DiscardEdges()
	{
		for(int i = availableEdges.Count - 1; i >= 0; i--)
		{
			if(!CanPickEdge(availableEdges[i]))
			{
				Vector4I edge = availableEdges[i];
				availableEdges.RemoveAt(i);
				discardedEdges.Add(edge);
			}
		}
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

	[Export] Slider widthSlider, heightSlider;
	[Export] Label widthLabel, heightLabel;
	[Export] Button exportButton, cameraZoomButton;

	STState state;
	Vector2 mousePos;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		camera.Position = new Vector2(0,0);
		state = new STState();
		state.Init(width, height, symX, symY);
		renderer.CreateTiles(state, symX, symY);
		QueueRedraw();

		widthSlider.ValueChanged += (new_width) => Resize((int) new_width, height);
		heightSlider.ValueChanged += (new_height) => Resize(width, (int) new_height);

		widthSlider.Value = width;
		heightSlider.Value = height;
		Resize(width, height);

		exportButton.Pressed += () => renderer.ExportToPNG();
		cameraZoomButton.Pressed += () => camera.Zoom = Vector2.One * Mathf.Max(1, Mathf.RoundToInt(camera.Zoom.X));
	}

	public void GenerateNew()
	{
		state.Init(width, height, symX, symY);
		while(!state.IsFinished()) state.ProcessNextEdge();
	}

	public void Resize(int w, int h)
	{
		width = w;
		height = h;
		widthLabel.Text = "Width: " + width.ToString();
		heightLabel.Text = "Height: " + height.ToString();
		state.Init(width, height, symX, symY);
		renderer.UpdateTiles(state, symX, symY);
		QueueRedraw();
	}

	Vector4I closestEdge = Vector4I.Zero;

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		Vector2 newMouse = GetGlobalMousePosition();
		if(mousePos != newMouse)
		{
			mousePos = newMouse;
			QueueRedraw();
		}

		if(Input.IsActionJustPressed("advance") && !state.IsFinished())
		{
			state.ProcessNextEdge();
			state.DiscardEdges();
			renderer.UpdateTiles(state, symX, symY);
			QueueRedraw();
		}
		if(Input.IsActionJustPressed("complete") && !state.IsFinished())
		{
			while(!state.IsFinished()) state.ProcessNextEdge();
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

		
		void CheckEdge(Vector2 mousePos, ref Vector4I closestEdge, ref float closestDist2, Vector4I candidateEdge)
		{
			Vector2 edgeCenter = GetEdgeCenter(candidateEdge);
			// Vector2 edgeCenter = new Vector2(candidateEdge.X + candidateEdge.Z, candidateEdge.Y + candidateEdge.W) / 2;
			float dist2 = mousePos.DistanceSquaredTo(edgeCenter);
			if(closestDist2 < 0 || dist2 < closestDist2)
			{
				closestEdge = candidateEdge;
				closestDist2 = dist2;
			}
		}

		closestEdge = Vector4I.Zero;
		float closestDist2 = -1;
		foreach(var edge in state.pickedEdges)
		{
			CheckEdge(mousePos, ref closestEdge, ref closestDist2, edge);
		}
		foreach(var edge in state.availableEdges)
		{
			CheckEdge(mousePos, ref closestEdge, ref closestDist2, edge);
		}
		foreach(var edge in state.discardedEdges)
		{
			CheckEdge(mousePos, ref closestEdge, ref closestDist2, edge);
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

	Vector2 GetEdgeCenter(Vector4I edge)
	{
		Vector2 edgeCenter = GetNodeCenter(edge.X, edge.Y) + GetNodeCenter(edge.Z, edge.W);
		edgeCenter /= 2;
		return edgeCenter;
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
			DrawLine(GetNodeCenter(edge.X, edge.Y), GetNodeCenter(edge.Z, edge.W), Colors.Green, lineWidth / 2);
		}

		if(!state.IsFinished())
		{
			foreach(Vector4I edge in state.availableEdges)
			{
				DrawLine(GetNodeCenter(edge.X, edge.Y), GetNodeCenter(edge.Z, edge.W), Colors.Yellow, lineWidth / 4);
			}

			foreach(Vector4I edge in state.discardedEdges)
			{
				DrawLine(GetNodeCenter(edge.X, edge.Y), GetNodeCenter(edge.Z, edge.W), Colors.Red, lineWidth / 4);
			}
		}

		if(HasEdgeSelected())
		{
			DrawLine(
				GetNodeCenter(closestEdge.X, closestEdge.Y), 
				GetNodeCenter(closestEdge.Z, closestEdge.W), 
				Colors.White, lineWidth);
		}

		for(int i = 0; i < width; i++)
		{
			for(int j = 0; j < height; j++)
			{
				// DrawString(font, GetNodeCenter(i, j), state.groups[i, j].ToString(), HorizontalAlignment.Center);
			}
		}

		DrawMouse();
	}

	bool HasEdgeSelected()
	{
		return GetEdgeCenter(closestEdge).DistanceTo(mousePos) < cellSize / 2f;
	}

    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);
		if (@event is InputEventMouseButton mouseEvent)
		{
			if (mouseEvent.ButtonIndex == MouseButton.Left && HasEdgeSelected() && mouseEvent.IsPressed())
			{
				bool discarded = state.discardedEdges.Contains(closestEdge);
				bool available = state.availableEdges.Contains(closestEdge);
				bool used = state.pickedEdges.Contains(closestEdge);

				bool change = false;
				if(used)
				{
					state.pickedEdges.Remove(closestEdge);
					state.availableEdges.Add(closestEdge);
					change = true;
				}
				else if(available)
				{
					state.pickedEdges.Add(closestEdge);
					state.availableEdges.Remove(closestEdge);
					change = true;
				}
				else
				{
					
				}
				if(change)
				{
					state.RecomputeState();
					renderer.UpdateTiles(state, symX, symY);
					QueueRedraw();
				}
			}
		}
    }

    Vector2 GetNodeCenter(int xx, int yy)
	{
		return GetMin() + new Vector2(xx + 0.5f, yy + 0.5f) * cellSize;
	}

	void DrawMouse()
	{
		DrawCircle(mousePos, 4, Colors.Yellow);

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

