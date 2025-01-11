using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

public class ObjectPool<T> where T: Node
{
	Node parent;
	List<T> availableObjects;
	
	Func<T> constructor;

	public ObjectPool(Node parent, Func<T> constructor, int objCount)
	{
		this.parent = parent;
		this.constructor = constructor;
		availableObjects = new List<T>();
		for(int i = 0; i < objCount; i++) availableObjects.Add(constructor());
	}

	public T GetObject()
	{
		T obj;
		if(availableObjects.Count == 0)
			obj = constructor();
		else
		{
			int last = availableObjects.Count - 1;
			obj = availableObjects[last];
			availableObjects.RemoveAt(last);
		}
		parent.AddChild(obj);
		return obj;
	}

	public void ReturnObject(T obj)
	{
		parent.RemoveChild(obj);
		availableObjects.Add(obj);
	}
}

public partial class RendererController : Node2D
{
	[Export] Camera2D camera;
	[Export] Control parentRect;
	[Export] TextureRect textureRect;
	[Export] SubViewport viewport;

	[Export] int lineWidth = 10;
	[Export] int outlineWidth = 12;
	[Export] int framePadding = 20; 

	[Export] Slider outlineSlider, lineSlider;
	[Export] Label outlineLabel, lineLabel;
	[Export] Button scaleUpButton, scaleDownButton, hideButton;
	[Export] DraggableWindow dragTL, dragTR, dragBL, dragBR;
	[Export] DraggableWindow parentDragWindow;

	Color outlineColor = Colors.Black;
	Color foregroundColor = new Color(178 / 255f, 0 / 255f, 0 / 255f);
	Color backgroundColor = new Color(255 / 255f, 232 / 255f, 165 / 255f);

	ObjectPool<LatticeTile> tilePool;

	int width, height;
	int cellSize = 50;

	const int LEFT = 0;
	const int UP = 1;
	const int RIGHT = 2;
	const int DOWN = 3;

	int internalResolutionScale = 1;
	float renderMinX, renderMaxX, renderMinY, renderMaxY;
	float renderScale = 1;
	
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
		static LatticeTile create_tile(ObjectPool<LatticeTile> pool, LatticeTile[,] tiles, Vector2I motifTile, int xx, int yy, Vector2 pos, float size, float width, Color color, int zindex)
		{
			LatticeTile tile = pool.GetObject();
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
				create_tile(tilePool, blackTiles, motifTile, i, j, pos, cellSize, outlineWidth, outlineColor, 0);
				create_tile(tilePool, redTiles, motifTile, i, j, pos, cellSize, lineWidth, foregroundColor, 1);
			}
		}
	}

	public void FreeTiles()
	{
		int w = redTiles.GetLength(0);
		int h = redTiles.GetLength(1);
		for(int i = 0; i < w; i++)
		{
			for(int j = 0; j < h; j++)
			{
				tilePool.ReturnObject(redTiles[i, j]);
				tilePool.ReturnObject(blackTiles[i, j]);
			}
		}
	}

	public void UpdateTiles(STState state, bool symX, bool symY)
	{
		RenderingMotif[,] motifs = GenerateRenderingPattern(state, symX, symY);
		int w = motifs.GetLength(0), h = motifs.GetLength(1);

		if(width != w || height != h)
		{
			FreeTiles();
			CreateTiles(state, symX, symY);			
		}

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

	Vector2I GetBaseTextureSize()
	{
		Vector2I size = new Vector2I(width, height) * cellSize;
		Vector2I padding = Vector2I.One * framePadding;

		size += new Vector2I(1, 1) * outlineWidth;
		size += padding;

		return size;
	}

	void UpdateRendering()
	{
		Vector2I size = GetBaseTextureSize();

		Vector2I textureSize = size * internalResolutionScale;
		viewport.Size = textureSize;
		camera.Zoom = Vector2.One * internalResolutionScale;

		UpdateRenderSize();
	}

	void ResetRenderSize()
	{
		Vector2I baseSize = GetBaseTextureSize();
		renderMinX = 440;
		renderMinY = 440;
		renderMaxX = renderMinX + baseSize.X;
		renderMaxY = renderMinY + baseSize.Y;
		UpdateRenderSize();
	}

	void UpdateRenderSize()
	{
		Vector2I baseSize = GetBaseTextureSize();
		float width = renderMaxX - renderMinX;
		float height = renderMaxY - renderMinY;

		float newAR = width / height;
		float prevAR = baseSize.X / (baseSize.Y + 0.0f);
		if(newAR > prevAR)
		{
			height = Mathf.Max(minHeight, height);
			width = height * prevAR;
		}
		else
		{
			width = Mathf.Max(minWidth, width);
			height = width / prevAR;
		}
		// if(newAR > 0 && prevAR > 0)

		Vector2 renderSize = new Vector2(Mathf.Max(minWidth, width), Mathf.Max(minHeight, height));
		Vector2 pos = new Vector2(renderMinX, renderMinY);
		parentRect.Position = pos;
		// textureRect.Position = pos;
		parentRect.Size = renderSize;
		textureRect.Size = renderSize;

		dragBL.Position = new Vector2(0, height) - new Vector2(dragBL.Size.X, 0);
		dragBR.Position = new Vector2(width, height);
		dragTL.Position = new Vector2(0,0) - new Vector2(dragBL.Size.X, 0);
		dragTR.Position = new Vector2(width, 0);
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

	public void ExportToPNG()
	{
		ViewportTexture texture = viewport.GetTexture();
		Image image = texture.GetImage();
		image.Resize(image.GetWidth(), image.GetHeight(), Image.Interpolation.Nearest);

		// Save the image as a PNG
		string save_path = "user://exported_viewport.png";
		Error error = image.SavePng(save_path);
		if(error == Error.Ok)
			GD.Print("Viewport exported successfully to: ", save_path);
		else
			GD.Print("Failed to save image. Error code: ", error);
	}

	void UpdateThickness(LatticeTile[,] tiles, int thickness, Color color)
	{
		for(int x = 0; x < tiles.GetLength(0); x++)
			for(int y = 0; y < tiles.GetLength(1); y++)
				tiles[x,y].SetupRenderingVariables(cellSize, thickness, color);
	}

	void SetupCornerVisibility(DraggableWindow corner)
	{
		/*
		ColorRect cr = corner as ColorRect;
		Color col = cr.Color;
		corner.OnHover += (hover) => cr.Color = new Color(col.R, col.G, col.B, hover ? 1 : 0);
		*/
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		tilePool = new ObjectPool<LatticeTile>(this, () => new LatticeTile(), 5);
		
		outlineSlider.Value = outlineWidth - lineWidth;
		lineSlider.Value = lineWidth;
		outlineLabel.Text = "Outline thickness: " + outlineSlider.Value.ToString();
		lineLabel.Text = "Line thickness: " + lineSlider.Value.ToString();
		
		outlineSlider.ValueChanged += (thickness) => {
			outlineWidth = (int) lineSlider.Value + (int)thickness;
			outlineLabel.Text = "Outline thickness: " + thickness.ToString();
			UpdateThickness(blackTiles, outlineWidth, outlineColor);
			UpdateRendering();
		};
		lineSlider.ValueChanged += (thickness) => {
			lineWidth = (int) thickness;
			outlineWidth = (int) outlineSlider.Value + lineWidth;
			lineLabel.Text = "Line thickness: " + thickness.ToString();
			UpdateThickness(redTiles, lineWidth, foregroundColor);
			UpdateThickness(blackTiles, outlineWidth, outlineColor);
			UpdateRendering();
		};

		scaleDownButton.Pressed += () => {
			renderScale /= 1.5f;
			UpdateRenderSize();
		};

		scaleUpButton.Pressed += () => {
			renderScale *= 1.5f;
			UpdateRenderSize();
		};

		SetupCornerVisibility(dragBL);
		SetupCornerVisibility(dragTL);
		SetupCornerVisibility(dragTR);
		SetupCornerVisibility(dragBR);

		dragBL.OnDrag += (delta) => {
			ResizeLeft(delta.X);
			ResizeBottom(delta.Y);
			UpdateRenderSize();
		};
		dragTL.OnDrag += (delta) => {
			ResizeLeft(delta.X);
			ResizeTop(delta.Y);
			UpdateRenderSize();
		};
		dragBR.OnDrag += (delta) => {
			ResizeRight(delta.X);
			ResizeBottom(delta.Y);
			UpdateRenderSize();
		};
		dragTR.OnDrag += (delta) => {
			ResizeRight(delta.X);
			ResizeTop(delta.Y);
			UpdateRenderSize();
		};

		parentDragWindow.OnDrag += (delta) => {
			renderMinX += delta.X;
			renderMaxX += delta.X;
			renderMinY += delta.Y;
			renderMaxY += delta.Y;
			UpdateRenderSize();
		};

		hideButton.Pressed += () => {
			textureRect.Visible = !textureRect.Visible;
			if(textureRect.Visible) parentDragWindow.EnableDrag();
			else parentDragWindow.DisableDrag();
		};

		ResetRenderSize();
	}

	int minWidth = 100;
	int minHeight = 100;
	void ResizeLeft(float delta)
	{
		renderMinX += delta;
		renderMinX = Mathf.Min(renderMinX, renderMaxX - minWidth);
	}

	void ResizeRight(float delta)
	{
		renderMaxX += delta;
		renderMaxX = Mathf.Max(renderMaxX, renderMinX + minWidth);
	}

	void ResizeTop(float delta)
	{
		renderMinY += delta;
		renderMinY = Mathf.Min(renderMinY, renderMaxY - minHeight);
	}

	void ResizeBottom(float delta)
	{
		renderMaxY += delta;
		renderMaxY = Mathf.Max(renderMaxY, renderMinY + minHeight);
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
