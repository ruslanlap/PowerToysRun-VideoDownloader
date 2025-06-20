using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Diagnostics;

namespace Community.PowerToys.Run.Plugin.VideoDownloader
{
    public class VideoInfoWindow : Window
    {
        private string _originalInfo;

        public VideoInfoWindow(string info)
        {
            _originalInfo = info;
            InitializeWindow();
            CreateContent();
        }

        private void InitializeWindow()
        {
            Title = "Available Video Formats";
            Width = 1000;
            Height = 700;
            MinWidth = 800;
            MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // Modern window styling
            Background = new SolidColorBrush(Color.FromRgb(32, 32, 32));
            Foreground = new SolidColorBrush(Color.FromRgb(240, 240, 240));

            // Add icon if available
            try
            {
                Icon = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/Images/videodownloader.dark.png", UriKind.Relative));
            }
            catch { /* Icon not found, continue without it */ }
        }

        private void CreateContent()
        {
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header section
            var headerPanel = CreateHeader();
            Grid.SetRow(headerPanel, 0);
            mainGrid.Children.Add(headerPanel);

            // Content section
            var contentPanel = CreateContentPanel();
            Grid.SetRow(contentPanel, 1);
            mainGrid.Children.Add(contentPanel);

            // Footer section
            var footerPanel = CreateFooter();
            Grid.SetRow(footerPanel, 2);
            mainGrid.Children.Add(footerPanel);

            Content = mainGrid;
        }

        private StackPanel CreateHeader()
        {
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Background = new SolidColorBrush(Color.FromRgb(24, 24, 24)),
                Margin = new Thickness(0, 0, 0, 1)
            };

            // Title
            var titleText = new TextBlock
            {
                Text = "📋 Available Video Formats",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 162, 255)),
                Margin = new Thickness(20, 15, 20, 5)
            };

            // Subtitle
            var subtitleText = new TextBlock
            {
                Text = "Choose from the available video and audio formats below",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                Margin = new Thickness(20, 0, 20, 15)
            };

            headerPanel.Children.Add(titleText);
            headerPanel.Children.Add(subtitleText);

            return headerPanel;
        }

        private Border CreateContentPanel()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                Margin = new Thickness(20, 10, 20, 10),
                CornerRadius = new CornerRadius(6),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                BorderThickness = new Thickness(1)
            };

            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(15)
            };

            var textBox = new TextBox
            {
                Text = FormatVideoInfo(_originalInfo),
                FontFamily = new FontFamily("Consolas, 'Courier New', monospace"),
                FontSize = 11,
                IsReadOnly = true,
                TextWrapping = TextWrapping.NoWrap,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                BorderThickness = new Thickness(0),
                AcceptsReturn = true,
                AcceptsTab = true
            };

            // Style the scrollbars
            scrollViewer.Resources.Add(SystemColors.ControlBrushKey, new SolidColorBrush(Color.FromRgb(60, 60, 60)));

            scrollViewer.Content = textBox;
            border.Child = scrollViewer;

            return border;
        }

        private StackPanel CreateFooter()
        {
            var footerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = new SolidColorBrush(Color.FromRgb(24, 24, 24)),
                Margin = new Thickness(0, 1, 0, 0)
            };

            // Copy button
            var copyButton = new Button
            {
                Content = "📋 Copy to Clipboard",
                Margin = new Thickness(10, 15, 10, 15),
                Padding = new Thickness(15, 8, 15, 8),
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            copyButton.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(_originalInfo);
                    ShowTemporaryMessage(copyButton, "✅ Copied!");
                }
                catch (Exception ex)
                {
                    ShowTemporaryMessage(copyButton, "❌ Failed to copy");
                    Debug.WriteLine($"Failed to copy to clipboard: {ex.Message}");
                }
            };

            // Close button
            var closeButton = new Button
            {
                Content = "Close",
                Margin = new Thickness(0, 15, 20, 15),
                Padding = new Thickness(15, 8, 15, 8),
                Background = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            closeButton.Click += (s, e) => Close();

            // Style buttons on hover
            StyleButton(copyButton, Color.FromRgb(0, 122, 204), Color.FromRgb(0, 142, 224));
            StyleButton(closeButton, Color.FromRgb(70, 70, 70), Color.FromRgb(90, 90, 90));

            footerPanel.Children.Add(copyButton);
            footerPanel.Children.Add(closeButton);

            return footerPanel;
        }

        private void StyleButton(Button button, Color normalColor, Color hoverColor)
        {
            button.MouseEnter += (s, e) => button.Background = new SolidColorBrush(hoverColor);
            button.MouseLeave += (s, e) => button.Background = new SolidColorBrush(normalColor);
        }

        private async void ShowTemporaryMessage(Button button, string message)
        {
            var originalContent = button.Content;
            button.Content = message;
            button.IsEnabled = false;

            await System.Threading.Tasks.Task.Delay(1500);

            button.Content = originalContent;
            button.IsEnabled = true;
        }

        private string FormatVideoInfo(string rawInfo)
        {
            if (string.IsNullOrEmpty(rawInfo))
                return "No format information available.";

            var lines = rawInfo.Split('\n');
            var formattedLines = new System.Collections.Generic.List<string>();

            bool inFormatSection = false;
            string currentSection = "";

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Detect different sections
                if (trimmedLine.StartsWith("[youtube]"))
                {
                    currentSection = "🔍 EXTRACTION INFO";
                    if (!string.IsNullOrEmpty(currentSection))
                    {
                        formattedLines.Add("");
                        formattedLines.Add("═══════════════════════════════════════════════════════════════");
                        formattedLines.Add($"  {currentSection}");
                        formattedLines.Add("═══════════════════════════════════════════════════════════════");
                    }
                    formattedLines.Add(line);
                }
                else if (trimmedLine.StartsWith("Available formats") || trimmedLine.Contains("format code"))
                {
                    inFormatSection = true;
                    currentSection = "📋 AVAILABLE FORMATS";
                    formattedLines.Add("");
                    formattedLines.Add("═══════════════════════════════════════════════════════════════");
                    formattedLines.Add($"  {currentSection}");
                    formattedLines.Add("═══════════════════════════════════════════════════════════════");
                    formattedLines.Add(line);
                }
                else if (trimmedLine.StartsWith("ID") && trimmedLine.Contains("EXT"))
                {
                    // Format table header
                    formattedLines.Add("");
                    formattedLines.Add("┌─────────────────────────────────────────────────────────────────────────────────┐");
                    formattedLines.Add($"│  {line.PadRight(80)}│");
                    formattedLines.Add("├─────────────────────────────────────────────────────────────────────────────────┤");
                }
                else if (inFormatSection && !string.IsNullOrWhiteSpace(trimmedLine))
                {
                    // Format each format line
                    if (trimmedLine.Contains("audio only"))
                        formattedLines.Add($"│🎵 {line.PadRight(78)}│");
                    else if (trimmedLine.Contains("video only"))
                        formattedLines.Add($"│🎬 {line.PadRight(78)}│");
                    else if (char.IsDigit(trimmedLine[0]))
                        formattedLines.Add($"│📹 {line.PadRight(78)}│");
                    else
                        formattedLines.Add($"│  {line.PadRight(80)}│");
                }
                else
                {
                    formattedLines.Add(line);
                }
            }

            if (inFormatSection)
            {
                formattedLines.Add("└─────────────────────────────────────────────────────────────────────────────────┘");
                formattedLines.Add("");
                formattedLines.Add("Legend: 🎵 Audio Only  🎬 Video Only  📹 Audio+Video");
            }

            return string.Join("\n", formattedLines);
        }
    }
}