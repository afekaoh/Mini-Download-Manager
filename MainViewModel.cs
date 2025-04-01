using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Mini_Download_Manager;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly HttpClient _httpClient = new();
    private readonly string _tempFolder = Path.GetTempPath();
    private string? _fileUrl;
    private BitmapImage? _image;
    private string? _imageUrl;
    private bool _isDownloading;
    private int _progress;
    private string? _title;

    public MainViewModel()
    {
        DownloadFileCommand = new RelayCommand(async () => await DownloadFileAsync());

        _ = FetchAndDisplayInfo();
    }

    public string? Title
    {
        get => _title;
        set
        {
            _title = value;
            OnPropertyChanged("Title");
        }
    }

    public BitmapImage? Image
    {
        get => _image;
        set
        {
            _image = value;
            OnPropertyChanged("Image");
        }
    }

    public int Progress
    {
        get => _progress;
        set
        {
            _progress = value;
            OnPropertyChanged("Progress");
        }
    }

    public bool ShowProgress
    {
        get => _isDownloading;
        set
        {
            _isDownloading = value;
            OnPropertyChanged("ShowProgress");
        }
    }

    public ICommand DownloadFileCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private async Task FetchAndDisplayInfo()
    {
        try
        {
            var jsonUrl = "https://4qgz7zu7l5um367pzultcpbhmm0thhhg.lambda-url.us-west-2.on.aws/";
            var jsonData = await _httpClient.GetStringAsync(jsonUrl);
            List<Dictionary<string, object>>? jsons =
                JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonData);

            if (jsons == null)
            {
                MessageBox.Show("Failed to parse JSON data.");
                return;
            }

            var json = jsons
                .Where(item => item.ContainsKey("Score"))
                .Where(item =>
                {
                    if (item.TryGetValue("os", out var os))
                        if (os is JsonElement { ValueKind: JsonValueKind.Number } element)
                            return element.GetInt32() >= WindowsSystemInfo.GetOsVersion();

                    return true;
                })
                .Where(item =>
                {
                    if (item.TryGetValue("ram", out var ram))
                        if (ram is JsonElement { ValueKind: JsonValueKind.Number } element)
                            return element.GetInt32() <= WindowsSystemInfo.GetTotalRam();

                    return true;
                })
                .Where(item =>
                {
                    if (item.TryGetValue("disk", out var disk))
                        if (disk is JsonElement { ValueKind: JsonValueKind.Number } element)
                            return element.GetInt32() <= WindowsSystemInfo.GetAvailableDiskSpace("C");

                    return true;
                })
                .Where(item =>
                {
                    if (item["Score"] is JsonElement element) return element.ValueKind == JsonValueKind.Number;

                    return true;
                })
                .OrderByDescending(item =>
                {
                    if (item["Score"] is JsonElement element) return element.GetInt32();

                    return 0;
                })
                .FirstOrDefault();

            Title = json?["Title"].ToString() ?? string.Empty;
            _fileUrl = json?["FileURL"].ToString() ?? string.Empty;
            _imageUrl = json?["ImageURL"].ToString() ?? string.Empty;
            await DownloadAndSetImage(_imageUrl);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to fetch JSON data: " + ex.Message);
        }
    }

    private async Task DownloadAndSetImage(string? url)
    {
        try
        {
            var fileName = Path.GetFileName(url);
            var tempImagePath = Path.Combine(_tempFolder, fileName);

            if (!File.Exists(tempImagePath))
            {
                var imageBytes = await _httpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(tempImagePath, imageBytes);
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(tempImagePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            Image = bitmap;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to download image: " + ex.Message);
        }
    }

    private async Task DownloadFileAsync()
    {
        if (string.IsNullOrEmpty(_fileUrl)) return;

        var fileName = Path.GetFileName(_fileUrl);
        var tempFilePath = Path.Combine(_tempFolder, fileName);

        if (File.Exists(tempFilePath))
        {
            MessageBox.Show("File already downloaded: " + tempFilePath);
            return;
        }

        try
        {
            ShowProgress = true;
            using (var client = new HttpClient())
            using (var response =
                   await client.GetAsync(_fileUrl, HttpCompletionOption.ResponseHeadersRead))
            using (Stream contentStream = await response.Content.ReadAsStreamAsync(),
                   stream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[8192];
                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await stream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;
                    if (totalBytes > 0) Progress = (int)(totalRead * 100 / totalBytes);
                }
            }

            MessageBox.Show("Download Complete! Saved at: " + tempFilePath);
            ShowProgress = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Download Failed: " + ex.Message);
        }
    }
}

public class RelayCommand : ICommand
{
    private readonly Func<bool> canExecute;
    private readonly Func<Task> execute;

    public RelayCommand(Func<Task> execute, Func<bool> canExecute = null)
    {
        this.execute = execute;
        this.canExecute = canExecute;
    }

    public event EventHandler CanExecuteChanged;

    public bool CanExecute(object parameter)
    {
        return canExecute == null || canExecute();
    }

    public async void Execute(object parameter)
    {
        await execute();
    }
}