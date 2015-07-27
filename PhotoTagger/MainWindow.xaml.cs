using DropNet;
using DropNet.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PhotoTagger
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DropNetClient _client;
        private UserLogin _accessToken;
        private MetaData _metaData;
        private int _position;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _client = new DropNetClient("zta6zq4gmjrjz6y", "30qfyt4yzwcltlj");

            if (File.Exists("accessToken.json"))
            {
                var json = File.ReadAllText("accessToken.json");
                _accessToken = JsonConvert.DeserializeObject<UserLogin>(json);
                _client.UserLogin = _accessToken;
                GetMetaData();
            }
            else
            {
                _client.GetToken();
                var url = _client.BuildAuthorizeUrl();
                dropboxPermissionsPanel.Visibility = Visibility.Visible;
                regularPanel.Visibility = Visibility.Hidden;
                webBrowser.Navigate(url);
            }
        }

        private void dropboxDone_Click(object sender, RoutedEventArgs e)
        {
            dropboxPermissionsPanel.Visibility = Visibility.Hidden;
            regularPanel.Visibility = Visibility.Visible;
            if (_accessToken == null)
            {
                var accessToken = _client.GetAccessToken();
                var json = JsonConvert.SerializeObject(accessToken);
                File.WriteAllText("accessToken.json", json);
            }

            GetMetaData();
        }

        private async void GetMetaData()
        {
            _metaData = _client.GetMetaData(path: "/Photos/Camera/2014/2014-05-11");
            _position = 0;
            LoadImageIntoControl(await GetImage(_position));
            if (_position < _metaData.Contents.Count - 1)
                await GetImage(_position + 1); // cache the next image
        }

        private static BitmapImage LoadImage(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0) return null;
            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = null;
                image.StreamSource = mem;
                image.EndInit();
            }
            image.Freeze();
            return image;
        }

        private async void nextButton_Click(object sender, RoutedEventArgs e)
        {
            _position++;
            LoadImageIntoControl(await GetImage(_position));
            if (_position < _metaData.Contents.Count - 1)
                await GetImage(_position + 1); // cache the next image
        }

        private async void prevButton_Click(object sender, RoutedEventArgs e)
        {
            _position--;
            LoadImageIntoControl(await GetImage(_position));
        }

        private async Task<byte[]> GetImage(int position)
        {
            var nextJpg = _metaData.Contents[position];

            byte[] jpgBytes;
            var cachePath = nextJpg.Path.TrimStart('/');
            var cacheDirectory = System.IO.Path.GetDirectoryName(cachePath);
            if (!Directory.Exists(cacheDirectory))
                Directory.CreateDirectory(cacheDirectory);

            if (!File.Exists(cachePath))
            {
                var tcs = new TaskCompletionSource<byte[]>();
                _client.GetThumbnailAsync(nextJpg.Path, ThumbnailSize.ExtraLarge, bs => tcs.SetResult(bs), e => tcs.SetException(e));
                jpgBytes = await tcs.Task;

                File.WriteAllBytes(cachePath, jpgBytes);
            }
            else
            {
                jpgBytes = File.ReadAllBytes(cachePath);
            }

            return jpgBytes;
        }

        private void LoadImageIntoControl(byte[] jpgBytes)
        {
            var nextJpg = _metaData.Contents[_position];
            metaDataText.Text = nextJpg.Path;
            image.Source = LoadImage(jpgBytes);
            prevButton.IsEnabled = (_position != 0);
            nextButton.IsEnabled = (_position != _metaData.Contents.Count - 1);
            tagsInput.Focus();
        }

        private List<string> _tags;
        private bool _consumeTextChanged;

        private void tagsInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_consumeTextChanged)
            {
                _consumeTextChanged = false;
                return;
            }

            if (_tags == null)
            {
                _tags = new List<string>();
                _tags.Add("Hello");
            }

            var testString = tagsInput.Text.Substring(0, tagsInput.CaretIndex);
            if (testString != "")
            {
                var tag = _tags.FirstOrDefault(t => t.StartsWith(testString));
                if (tag != null)
                {
                    var pos = tagsInput.CaretIndex;
                    if (tagsInput.Text != tag)
                    {
                        ChangeText(tag);
                        tagsInput.CaretIndex = pos;
                    }
                }
            }
            else
            {
                ChangeText("");
            }

        }

        private void tagsInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                e.Handled = true;
                ChangeText(tagsInput.Text + " ");
                tagsInput.CaretIndex = tagsInput.Text.Length;
            }
        }

        private void ChangeText(string text)
        {
            if (_consumeTextChanged) return;

            if (text != tagsInput.Text)
            {
                _consumeTextChanged = true;
                tagsInput.Text = text;
            }
        }
    }

}
