// Publish Commands:
// dotnet publish -f net10.0-android
// dotnet publish -f net10.0-windows10.0.19041.0 -c Release -p:RuntimeIdentifierOverride=win10-x64 -p:WindowsPackageType=None

using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using HtmlAgilityPack;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using static BestPerfume.Accord;

namespace BestPerfume;

public partial class Catalogue : ContentPage
{
    public Catalogue()
    {
        TheCatalogue = this;
        InitializeComponent();
        DownloadDataAsync();
        BindingContext = PerfumeViewModel;
    }

    public static List<Perfume> AllPerfumes { get; } = [];
    public static Catalogue? TheCatalogue { get; private set; } = null;
    public static string FolderPath { get; } = FileSystem.Current.AppDataDirectory;
    public static string FavouritesPath { get; } = $"{FolderPath}/favourites.jamie";
    public static string ReleaseDatesPath { get; } = $"{FolderPath}/release_dates.jamie";
    public static Window CurrentWindow { get; } = Application.Current!.Windows[0];
    public PerfumeViewModel PerfumeViewModel { get; } = new();

    public static void EnsureFilesExist(bool updateFavourites = true)
    {
        if (!Directory.Exists(FolderPath)) Directory.CreateDirectory(FolderPath);
        if (!File.Exists(FavouritesPath)) File.WriteAllText(FavouritesPath, "");
        if (!File.Exists(ReleaseDatesPath)) File.WriteAllText(ReleaseDatesPath, "");
        if (updateFavourites)
            File.WriteAllText(FavouritesPath, JsonSerializer.Serialize(AllPerfumes.Select(x => (x.ID, x.Favourited)).ToDictionary()));
    }

    public async void DownloadDataAsync()
    {
        EnsureFilesExist(false);
        string data = File.ReadAllText(ReleaseDatesPath);
        Dictionary<string, DateTime> releaseDatesDict = data == "" ? [] : JsonSerializer.Deserialize<Dictionary<string, DateTime>>(data) ?? [];
        data = File.ReadAllText(FavouritesPath);
        Dictionary<string, bool> favouritesDictionary = data == "" ? [] : JsonSerializer.Deserialize<Dictionary<string, bool>>(data) ?? [];

        using HttpClient client = new();
        client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/120.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Add("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

        List<Task<string>> tasks = [];
        for (int page = 1; page < 7; page++)
            tasks.Add(client.GetStringAsync($"https://bestperfume.store/collections/all?page={page}"));
        string[] raw_htmls = await Task.WhenAll(tasks);

        List<HtmlDocument> htmls = [];
        for (int page = 0; page < raw_htmls.Length; page++)
        {
            HtmlDocument doc = new();
            doc.LoadHtml(raw_htmls[page].Replace('\u00a0', ' ').Replace("&quot;", ""));
            doc.Save($"{FolderPath}/html{page+1}.html");
            htmls.Add(doc);
        }

        HtmlNode[] nodes = [.. htmls.SelectMany(x => x.DocumentNode.SelectNodes(".//div[@data-load-more--grid-item]"))];
        TextInfo textInfo = new CultureInfo("en-SG", false).TextInfo;
        bool emptyReleaseDates = releaseDatesDict.Count == 0;
        DateTime earliestReleaseDate = emptyReleaseDates ? DateTime.Now : releaseDatesDict.Values.Min();
        foreach (HtmlNode node in nodes)
        {
            try
            {
                HtmlNode[] accordsNodes = [.. node.SelectNodes(".//div[@class='tags-absolute']/span")];
                ObservableCollection<Accord> accords = [.. accordsNodes.Select(x => Accords[textInfo.ToTitleCase(x.InnerText.ToLower())])];

                HtmlNode nameNode = node.SelectSingleNode(".//div[@class='product-details']//div[@class='metafield-rich_text_field']");
                Match nameMatch = Regex.Match(nameNode.InnerText, "resembling (.*?) by (.*?)$");
                string name = $"{nameMatch.Groups[1].Value} | {nameMatch.Groups[2].Value}".Replace("&amp;", "&").Replace("&#39;", "'");

                HtmlNode notesNode = node.SelectSingleNode(".//div[contains(@data-product, 'MAIN ACCORDS')]");
                string notesData = notesNode.GetAttributeValue("data-product", "");
                string id = Regex.Match(notesData, "handle:(.*?),").Groups[1].Value.ToUpper();
                string store = $"https://bestperfume.store/products/{id}";
                string fragrantica = Regex.Match(notesData, @"href=\\(.*?)\\ ").Groups[1].Value.Replace(@"\", "");
                notesData = Regex.Replace(notesData, @"\\u003c(.*?)\\u003e", "");
                string top = Regex.Match(notesData, @"(?<=TOP - )(.*?)(?=\\n|MID)").Groups[1].Value;
                string mid = Regex.Match(notesData, @"(?<=MID - )(.*?)(?=\\n|BASE)").Groups[1].Value;
                string @base = Regex.Match(notesData, @"(?<=BASE - )(.*?)\\n").Groups[1].Value;

                HtmlNode imageNode = node.SelectSingleNode(".//img");
                string image = $"https:{imageNode.GetAttributeValue("src", "").Split('&')[0]}";

                if (!emptyReleaseDates
                    && (!releaseDatesDict.TryGetValue(id, out DateTime releaseDate) 
                    || (DateTime.Now.Subtract(releaseDate).TotalDays < 14
                    && releaseDate.Subtract(earliestReleaseDate).TotalHours > 1)))
                {
                    releaseDatesDict.TryAdd(id, DateTime.Now);
                    accords.Insert(1, Accords["New"]);
                }
                favouritesDictionary.TryGetValue(id, out bool favourited);

                Perfume perfume = new(id, name, favourited, accords, top, mid, @base, store, fragrantica, image);
                AllPerfumes.Add(perfume);
            }
            catch { } // Luxury Atomiser / Tester Strips
        }
        AllPerfumes.Sort();
        PerfumeViewModel.DisplayedPerfumes = new(AllPerfumes);
        EnsureFilesExist();
        File.WriteAllText(ReleaseDatesPath, JsonSerializer.Serialize(releaseDatesDict));
    }

    async void OnOptionsClicked(object sender, EventArgs e)
    {
        if (sender is not Button button) return;
        Perfume perfume = button.CommandParameter as Perfume ?? throw new ArgumentException("Unknown perfume selected.");
        if (!char.IsDigit(perfume.ID[^1])) return;
        IPopupResult<Task> popupResult = await this.ShowPopupAsync<Task>(new PerfumePopup(perfume));
        if (popupResult.Result is null) return;
        await popupResult.Result;
    }

    async void ShowFilterPopup(object sender, EventArgs e)
    {
        IPopupResult popupResult = await this.ShowPopupAsync(new FilterPopup());
        if (popupResult.WasDismissedByTappingOutsideOfPopup) return;
        string[] words = FilterPopup.SearchQuery.ToLower().Split([' ', ',','.'], StringSplitOptions.RemoveEmptyEntries);
        IEnumerable<Accord> ands = Accords.Values.Where(x => x.FilterState == FilterState.And);
        IEnumerable<Accord> ors = Accords.Values.Where(x => x.FilterState == FilterState.Or);
        IEnumerable<Accord> nots = Accords.Values.Where(x => x.FilterState == FilterState.Not);
        PerfumeViewModel.DisplayedPerfumes.Clear();
        foreach (Perfume perfume in AllPerfumes)
        {
            if (perfume.Favourited) perfume.Accords.Add(Accords["Favourite"]);
            if (ands.All(perfume.Accords.Contains)
                && !nots.Intersect(perfume.Accords).Any()
                && (!ors.Any() || ors.Intersect(perfume.Accords).Any())
                && (words.Length==0 || words.All(x => $"{perfume.ID} {perfume.Name}".Contains(x, StringComparison.OrdinalIgnoreCase))))
                PerfumeViewModel.DisplayedPerfumes.Add(perfume);
            if (perfume.Favourited) perfume.Accords.Remove(Accords["Favourite"]);
        }
        if (!PerfumeViewModel.DisplayedPerfumes.Any())
            PerfumeViewModel.DisplayedPerfumes.Add(new(" No perfumes found."));
    }

    protected override async void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        PerfumeViewModel.Span = DeviceInfo.Current.Idiom != DeviceIdiom.Phone ? (int)CurrentWindow.Width / 350 : 1;
    }
}

public partial class Perfume(string ID, string Name, bool Favourited, ObservableCollection<Accord> Accords, string Top, string Mid, string Base, string StoreURL, string FragranticaURL, string ImageURL) : ObservableObject, IComparable<Perfume>
{
    public Perfume(string ID) : this(ID, "", false, [], "", "", "", "", "", "") { }

    public string ID { get; } = ID;
    public string Name { get; } = Name;
    public bool Favourited { get; set => SetProperty(ref field, value); } = Favourited;
    public ObservableCollection<Accord> Accords { get; } = Accords;
    public string Top { get; } = Top;
    public string Mid { get; } = Mid;
    public string Base { get; } = Base;
    public string StoreURL { get; } = StoreURL;
    public string FragranticaURL { get; } = FragranticaURL;
    public string ImageURL { get; } = ImageURL;

    public int Index => ID.Contains('X') ? 1000 + int.Parse(ID[1..]) : int.Parse(ID);

    int IComparable<Perfume>.CompareTo(Perfume? other)
    {
        if (other is null) return int.MaxValue;
        return Index.CompareTo(other.Index);
    }
}

public sealed partial class PerfumeViewModel : INotifyPropertyChanged
{
    public ObservableCollection<Perfume> DisplayedPerfumes
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    } = [new(" Loading Perfumes...")];
    public int Span
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    } = 1;

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string name = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public enum FilterState { None, And, Or, Not }

public partial class Accord(string Name, Color BackgroundColour, Color TextColour) : ObservableObject
{
    public string Name { get; } = Name;
    public Color BackgroundColour { get; } = BackgroundColour;
    public Color TextColour { get; } = TextColour;
    public FilterState FilterState { get; set => SetProperty(ref field, value); } = FilterState.None;

    public static Dictionary<string, Accord> Accords { get; } = new Accord[]
    {
        new("Favourite", Colors.Red, Colors.White),
        new("Sweet", Color.FromRgba("#c6b4f8"), Colors.Black),
        new("Fruity", Color.FromRgba("#f7b6ca"), Colors.Black),
        new("Floral", Color.FromRgba("#ef5486"), Colors.White),
        new("Aromatic", Color.FromRgba("#c0d678"), Colors.Black),
        new("Citrus", Color.FromRgba("#fee46f"), Colors.Black),
        new("Sea", Color.FromRgba("#7dd6ff"), Colors.Black),
        new("Vanilla", Color.FromRgba("#f6efd1"), Colors.Black),
        new("Musky", Color.FromRgba("#ecc275"), Colors.Black),
        new("Woody", Color.FromRgba("#c77900"), Colors.White),
        new("Fresh", Color.FromRgba("#bdf7ff"), Colors.Black),
        new("Powdery", Color.FromRgba("#fecc64"), Colors.Black),
        new("Spicy", Color.FromRgba("#f87650"), Colors.Black),
        new("Clean", Color.FromRgba("#82dadc"), Colors.Black),
        new("Green", Color.FromRgba("#90c885"), Colors.Black),
        new("Leather", Color.FromRgba("#875710"), Colors.White),
        new("Tobacco", Color.FromRgba("#d35148"), Colors.White),
        new("Oud", Color.FromRgba("#301a06"), Colors.White),
        new("Women", Color.FromRgba("#e000e0"), Colors.White),
        new("Men", Color.FromRgba("#0000ff"), Colors.White),
        new("Unisex", Color.FromRgba("#7000f0"), Colors.White),
        new("New", Colors.Black, Colors.White)
    }.Select(x => (x.Name, x)).ToDictionary();
}

public class BoolToOpacityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is bool favourited && favourited ? 1.0 : 0.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}