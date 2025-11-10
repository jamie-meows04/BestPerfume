using CommunityToolkit.Maui.Views;
using System.Collections.ObjectModel;
using System.Globalization;
using static BestPerfume.Catalogue;

namespace BestPerfume;

public partial class FilterPopup : Popup
{
    public FilterPopup()
	{
        BackgroundColor = TheCatalogue!.BackgroundColor;
		InitializeComponent();
		BindingContext = this;
        SearchBar = DeviceInfo.Idiom == DeviceIdiom.Phone ? searchBarPhone : searchBarDesktop;
        SearchBar.Text = SearchQuery;
    }

    public static string SearchQuery { get; private set; } = "";
    public Entry SearchBar { get; init; }
    public ObservableCollection<Accord> Accords { get; } = [.. Accord.Accords.Values];

    void ToggleAnd(object? sender, EventArgs e)
    {
        if (sender is not Button button) return;
        Accord accord = (Accord)button.CommandParameter;
        FilterState currentState = accord.FilterState;
        accord.FilterState = currentState == FilterState.And ? FilterState.None : FilterState.And;
    }

    void ToggleOr(object? sender, EventArgs e)
    {
        if (sender is not Button button) return;
        Accord accord = (Accord)button.CommandParameter;
        FilterState currentState = accord.FilterState;
        accord.FilterState = currentState == FilterState.Or ? FilterState.None : FilterState.Or;
    }

    void ToggleNot(object? sender, EventArgs e)
    {
        if (sender is not Button button) return;
        Accord accord = (Accord)button.CommandParameter;
        FilterState currentState = accord.FilterState;
        accord.FilterState = currentState == FilterState.Not ? FilterState.None : FilterState.Not;
    }

    private void ClearSearchBar(object sender, EventArgs e)
    {
        SearchQuery = SearchBar.Text = "";
    }

    private void ResetFilters(object sender, EventArgs e)
    {
        foreach (Accord accord in Accords)
            accord.FilterState = FilterState.None;
    }

    private async void Close(object sender, EventArgs e)
    {
        SearchQuery = SearchBar.Text;
        await CloseAsync();
    }
}

public class FilterStateToOpacityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is FilterState filterState && parameter is string buttonID && $"{(int)filterState}" == buttonID ? 1.0 : 0.5;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}