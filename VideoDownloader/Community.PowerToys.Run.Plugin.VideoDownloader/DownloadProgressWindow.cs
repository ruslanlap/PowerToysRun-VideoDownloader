using System.Windows;
using System.Windows.Controls;

namespace Community.PowerToys.Run.Plugin.VideoDownloader
{
    internal class DownloadProgressWindow : Window
    {
        private readonly ProgressBar _bar;
        private readonly TextBlock _label;

        public DownloadProgressWindow()
        {
            Title = "Downloading...";
            Width = 400;
            Height = 120;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;

            _bar = new ProgressBar { Minimum = 0, Maximum = 100, Height = 20, Margin = new Thickness(20,10,20,10) };
            _label = new TextBlock { Margin = new Thickness(20,10,20,0) };

            var panel = new StackPanel();
            panel.Children.Add(_label);
            panel.Children.Add(_bar);
            Content = panel;
        }

        public void Update(double percent, string eta)
        {
            Dispatcher.Invoke(() =>
            {
                _bar.Value = percent;
                _label.Text = $"{percent:F1}% - ETA {eta}";
            });
        }
    }
}
