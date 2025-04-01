using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Mini_Download_Manager
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string? _title;
        private string? _fileUrl;
        private string? _imageUrl;
        private BitmapImage? _image;
        private int _progress;
        private bool _isDownloading;
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly string _tempFolder = Path.GetTempPath();

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

        public MainViewModel()
        {
            DownloadFileCommand = new RelayCommand(async () => await DownloadFileAsync());

            _ = FetchAndDisplayInfo();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private async Task FetchAndDisplayInfo()
        {
            try
            {
                string jsonUrl = "https://4qgz7zu7l5um367pzultcpbhmm0thhhg.lambda-url.us-west-2.on.aws/";
                string jsonData = await _httpClient.GetStringAsync(jsonUrl);
                List<Dictionary<string, object>>? jsons =
                    JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonData);
                
                if (jsons == null)
                {
                    MessageBox.Show("Failed to parse JSON data.");
                    return;
                }

                var json = jsons
                    .Where(item => item.ContainsKey("Score"))
                    // .Where(item =>
                    // {
                    //     if (item.TryGetValue("os", out var os))
                    //     {
                    //         if (os is JsonElement { ValueKind: JsonValueKind.Number } element)
                    //         {
                    //             return element.GetInt32() >= WindowsSystemInfo.GetOsVersion();
                    //         }
                    //     }
                    //
                    //     return true;
                    // })
                    // .Where(item =>
                    // {
                    //     if (item.TryGetValue("ram", out var ram))
                    //     {
                    //         if (ram is JsonElement { ValueKind: JsonValueKind.Number } element)
                    //         {
                    //             return element.GetInt32() <= WindowsSystemInfo.GetTotalRam();
                    //         }
                    //     }
                    //
                    //     return true;
                    // })
                    // .Where(item =>
                    // {
                    //     if (item.TryGetValue("disk", out var disk))
                    //     {
                    //         if (disk is JsonElement { ValueKind: JsonValueKind.Number } element)
                    //         {
                    //             return element.GetInt32() <= WindowsSystemInfo.GetAvailableDiskSpace("C");
                    //         }
                    //     }
                    //     return true;
                    // })
                    .Where(item =>
                    {
                        if (item["Score"] is JsonElement element)
                        {
                            return element.ValueKind == JsonValueKind.Number;
                        }

                        return true;
                    })
                    .OrderByDescending(item =>
                    {
                        if (item["Score"] is JsonElement element)
                        {
                            return element.GetInt32();
                        }

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
                string fileName = Path.GetFileName(url);
                string tempImagePath = Path.Combine(_tempFolder, fileName);

                if (!File.Exists(tempImagePath))
                {
                    byte[] imageBytes = await _httpClient.GetByteArrayAsync(url);
                    await File.WriteAllBytesAsync(tempImagePath, imageBytes);
                }

                BitmapImage? bitmap = new BitmapImage();
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
                using (HttpClient client = new HttpClient())
                using (HttpResponseMessage response =
                       await client.GetAsync(_fileUrl, HttpCompletionOption.ResponseHeadersRead))
                using (Stream contentStream = await response.Content.ReadAsStreamAsync(),
                       stream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = new byte[8192];
                    long totalBytes = response.Content.Headers.ContentLength ?? -1;
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await stream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;
                        if (totalBytes > 0)
                        {
                            Progress = (int)((totalRead * 100) / totalBytes);
                        }
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
        private readonly Func<Task> execute;
        private readonly Func<bool> canExecute;

        public RelayCommand(Func<Task> execute, Func<bool> canExecute = null)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => canExecute == null || canExecute();

        public async void Execute(object parameter) => await execute();
    }
}