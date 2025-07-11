using System.Windows;

namespace Community.PowerToys.Run.Plugin.VideoDownloader
{
    public partial class VideoInfoWindow : Window
    {
        public VideoInfoWindow(string info)
        {
            InitializeComponent();
            InfoTextBlock.Text = info;
        }
    }
}
