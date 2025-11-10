using CommunityToolkit.Maui.Views;
using static BestPerfume.Catalogue;

namespace BestPerfume;

public partial class PerfumePopup : Popup<Task>
{
    public PerfumePopup(Perfume perfume)
	{
		Perfume = perfume;
        BackgroundColor = TheCatalogue!.BackgroundColor;
		InitializeComponent();
        scrollView.WidthRequest = Math.Min(600, TheCatalogue.Width - 100);
		BindingContext = this;
    }
    public Perfume Perfume { get; set; }
    public string PerfumeTitle => $"{Perfume.ID}: {Perfume.Name.Split(" | ")[0]}";
    public string PerfumeNotes => $"Top: {Perfume.Top}\n\nMid: {Perfume.Mid}\n\nBase: {Perfume.Base}";
    public string FavouriteText => Perfume.Favourited ? "Remove from favourites" : "Add to favourites";

    async void ToggleFavourites(object? sender, EventArgs e) => await CloseAsync(Task.Run(async () =>
    {
        Perfume.Favourited ^= true;
        EnsureFilesExist();
        await Task.Yield();
    }));

    async void ViewStorePage(object? sender, EventArgs e) => await CloseAsync(Browser.Default.OpenAsync(Perfume.StoreURL));
    async void ViewFragranticaPage(object? sender, EventArgs e) => await CloseAsync(Browser.Default.OpenAsync(Perfume.FragranticaURL));
    async void Close(object? sender, EventArgs e) => await CloseAsync(Task.Delay(0));
}