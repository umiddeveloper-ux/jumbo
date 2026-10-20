using System.Collections;
using System.Collections.Specialized;

namespace Jumbo.Controls;

/// <summary>A labelled, rounded drop-down. Items are shown through <see cref="DisplayMember"/> (a property name; the item itself when empty).</summary>
public partial class FormPicker : ContentView
{
    public static readonly BindableProperty CaptionProperty = BindableProperty.Create(
        nameof(Caption), typeof(string), typeof(FormPicker), string.Empty, propertyChanged: (b, _, n) => ((FormPicker)b).CaptionLabel.Text = (string?)n);

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(FormPicker), string.Empty, propertyChanged: (b, _, n) => ((FormPicker)b).Input.Title = (string?)n);

    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource), typeof(IList), typeof(FormPicker), null, propertyChanged: (b, o, n) => ((FormPicker)b).OnItemsSourceChanged(o as IList, n as IList));

    public static readonly BindableProperty DisplayMemberProperty = BindableProperty.Create(
        nameof(DisplayMember), typeof(string), typeof(FormPicker), string.Empty, propertyChanged: (b, _, _) => ((FormPicker)b).Rebuild());

    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(
        nameof(SelectedItem), typeof(object), typeof(FormPicker), null, BindingMode.TwoWay, propertyChanged: (b, _, n) => ((FormPicker)b).SelectItem(n));

    private bool _syncing;

    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public IList? ItemsSource { get => (IList?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public string DisplayMember { get => (string)GetValue(DisplayMemberProperty); set => SetValue(DisplayMemberProperty, value); }
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }

    public FormPicker() => InitializeComponent();

    private void OnItemsSourceChanged(IList? oldList, IList? newList)
    {
        if (oldList is INotifyCollectionChanged oldNotify) oldNotify.CollectionChanged -= OnCollectionChanged;
        if (newList is INotifyCollectionChanged newNotify) newNotify.CollectionChanged += OnCollectionChanged;
        Rebuild();
    }

    // The list is often filled after it was bound (loaded from the server): follow it.
    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => MainThread.BeginInvokeOnMainThread(Rebuild);

    private string Display(object? item)
    {
        if (item is null) return string.Empty;
        if (string.IsNullOrEmpty(DisplayMember)) return item.ToString() ?? string.Empty;
        return item.GetType().GetProperty(DisplayMember)?.GetValue(item)?.ToString() ?? string.Empty;
    }

    private void Rebuild()
    {
        _syncing = true;
        try
        {
            Input.ItemsSource = ItemsSource?.Cast<object>().Select(Display).ToList() ?? [];
        }
        finally { _syncing = false; }
        SelectItem(SelectedItem);
    }

    private void SelectItem(object? item)
    {
        if (_syncing) return;
        _syncing = true;
        try
        {
            int idx = item is null || ItemsSource is null ? -1 : ItemsSource.IndexOf(item);
            if (Input.SelectedIndex != idx) Input.SelectedIndex = idx;
        }
        finally { _syncing = false; }
    }

    private void OnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_syncing) return;
        var idx = Input.SelectedIndex;
        SelectedItem = idx >= 0 && ItemsSource is not null && idx < ItemsSource.Count ? ItemsSource[idx] : null;
    }
}
