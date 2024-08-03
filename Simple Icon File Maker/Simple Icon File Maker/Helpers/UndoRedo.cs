using ImageMagick;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;
using System.Drawing;

namespace Simple_Icon_File_Maker;

public class UndoRedo
{
    private readonly Stack<UndoRedoItem> _undoStack = new();
    private readonly Stack<UndoRedoItem> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public string CurrentPath { get; set; } = string.Empty;

    public void AddUndo(UndoRedoItem item)
    {
        _undoStack.Push(item);
        _redoStack.Clear();
    }

    public string Undo()
    {
        if (_undoStack.Count == 0)
            return string.Empty;

        UndoRedoItem item = _undoStack.Pop();
        string newPath = item.Undo();
        _redoStack.Push(item);
        return newPath;
    }

    public string Redo()
    {
        if (_redoStack.Count == 0)
            return string.Empty;

        UndoRedoItem item = _redoStack.Pop();
        string newPath = item.Redo();
        _undoStack.Push(item);
        return newPath;
    }
}

public abstract class UndoRedoItem
{
    public abstract string Undo();
    public abstract string Redo();
}


public class MagickImageUndoRedoItem : UndoRedoItem
{
    private readonly Image _image;
    private readonly string _previous;
    private readonly string _next;

    public MagickImageUndoRedoItem(Image image, string previousPath, string nextPath)
    {
        _image = image;
        _previous = previousPath;
        _next = nextPath;
    }

    public override string Undo()
    {
        MagickImage undoImage = new(_previous);
        _image.Source = undoImage.ToImageSource();
        return _previous;
    }

    public override string Redo()
    {
        MagickImage redoImage = new(_next);
        _image.Source = redoImage.ToImageSource();
        return _next;
    }
}

public class ResizeUndoRedoItem : UndoRedoItem
{
    private readonly Image _image;
    private readonly string _previous;
    private readonly string _next;
    private readonly Grid _grid;
    private readonly Size _oldSize;

    public ResizeUndoRedoItem(Image image, Grid grid, Size oldGridSize, string previousPath, string nextPath)
    {
        _image = image;
        _previous = previousPath;
        _next = nextPath;
        _grid = grid;
        _oldSize = oldGridSize;
    }

    public override string Undo()
    {
        MagickImage undoImage = new(_previous);
        _image.Source = undoImage.ToImageSource();
        _image.Stretch = Stretch.Uniform;
        _grid.Width = 700;
        _grid.Height = double.NaN;
        return _previous;
    }

    public override string Redo()
    {
        MagickImage redoImage = new(_next);
        _image.Source = redoImage.ToImageSource();
        _image.Stretch = Stretch.Fill;
        _grid.Width = _oldSize.Width;
        _grid.Height = _oldSize.Height;
        return _next;
    }
}