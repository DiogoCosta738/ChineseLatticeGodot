using Godot;
using System;

public partial class DraggableWindow : Control
{
	Vector2 prevMouse;
	bool mouseOn = false;
	bool isDragging = false;
	bool canDrag = true;

	public Action<Vector2> OnDrag;
	public Action<bool> OnHover;
	public Action OnStartDrag, OnEndDrag;

	public void EnableDrag()
	{
		canDrag = true;
	}

	public void DisableDrag()
	{
		canDrag = false;
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		MouseEntered += MouseOn;
		MouseExited += MouseOff;
	}

	void MouseOn()
	{
		mouseOn = true;
		OnHover?.Invoke(true);
	}

	void MouseOff()
	{
		mouseOn = false;
		OnHover?.Invoke(false);
	}

	public override void _Input(InputEvent @event)
	{
		if(!canDrag) return;
		
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
		OnStartDrag?.Invoke();
	}

	void StopDrag()
	{
		isDragging = false;
		OnEndDrag?.Invoke();
	}

	void UpdateDrag()
	{
		Vector2 mouseDelta = GetMouse() - prevMouse;
		Position += mouseDelta;
		prevMouse = GetMouse();
		OnDrag?.Invoke(mouseDelta);
	}


	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if(isDragging) UpdateDrag();
	}
}
