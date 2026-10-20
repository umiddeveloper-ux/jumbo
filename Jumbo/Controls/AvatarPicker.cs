using Jumbo.Shared;
using Microsoft.Maui.Controls.Shapes;

namespace Jumbo.Controls;

/// <summary>A grid of the preset avatars (the same twelve as on the website); the chosen one has a coloured frame.</summary>
public class AvatarPicker : ContentView
{
    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
        nameof(SelectedIndex), typeof(int), typeof(AvatarPicker), -1, BindingMode.TwoWay, propertyChanged: (b, _, _) => ((AvatarPicker)b).ShowSelection());

    /// <summary>Index in <see cref="PresetAvatars.All"/>; -1 when the learner's picture is not one of them (an uploaded photo).</summary>
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    private const int Columns = 4;
    private const double Gap = 8;
    private readonly Grid _grid = new() { ColumnSpacing = Gap, RowSpacing = Gap };
    private readonly List<Border> _cells = [];

    public AvatarPicker()
    {
        for (int c = 0; c < Columns; c++) _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        for (int i = 0; i < PresetAvatars.All.Count; i++)
        {
            int index = i;
            var cell = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 18 },
                StrokeThickness = 3,
                Padding = 3,
                BackgroundColor = Colors.Transparent,
                HeightRequest = 64,
                Content = new SvgPicture { Svg = PresetAvatars.All[i], RenderSize = 160 }
            };
            cell.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => SelectedIndex = index) });
            if (i % Columns == 0) _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _grid.Add(cell, i % Columns, i / Columns);
            _cells.Add(cell);
        }

        Content = _grid;
        SizeChanged += (_, _) =>
        {
            if (Width <= 0) return;
            double size = (Width - Gap * (Columns - 1)) / Columns;       // square cells
            foreach (var c in _cells) c.HeightRequest = size;
        };
        ShowSelection();
        if (Application.Current is { } app) app.RequestedThemeChanged += (_, _) => ShowSelection();
    }

    private void ShowSelection()
    {
        var primary = (Color)Application.Current!.Resources["Primary"];
        for (int i = 0; i < _cells.Count; i++)
            _cells[i].Stroke = new SolidColorBrush(i == SelectedIndex ? primary : Colors.Transparent);
    }
}
