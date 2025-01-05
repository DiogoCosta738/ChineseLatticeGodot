using Godot;
using System;

public partial class DraggableWindow : Control
{
	Vector2 prevMouse;
	bool mouseOn = false;
	bool isDragging = false;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		MouseEntered += MouseOn;
		MouseExited += MouseOff;
	}

	void MouseOn()
	{
		mouseOn = true;
	}

	void MouseOff()
	{
		mouseOn = false;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent)
		{
			if (mouseEvent.ButtonIndex == MouseButton.Left)
			{
				if(mouseOn && mouseEvent.IsPressed() && !isDragging)
				{
					StartDrag();
				}
				else if(mouseEvent.IsReleased())
				{
					StopDrag();
				}
			}
		}
	}

	Vector2 GetMouse()
	{
		return GetGlobalMousePosition();
	}

	void StartDrag()
	{
		prevMouse = GetMouse();
		isDragging = true;
	}

	void StopDrag()
	{
		isDragging = false;
	}

	void UpdateDrag()
	{
		Vector2 mouseDelta = GetMouse() - prevMouse;
		Position += mouseDelta;
		prevMouse = GetMouse();
	}


	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if(isDragging) UpdateDrag();
	}
}
