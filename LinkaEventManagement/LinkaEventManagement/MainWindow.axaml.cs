using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Controls.Documents;
using Avalonia.Threading;

namespace LinkasEventManagement
{
    public partial class MainWindow : Window
    {
        private WrapPanel _sessionsPanel;
        private HttpClient _httpClient = new HttpClient();
        private const string AllSessionsUrl = "https://api.resonite.com/sessions/";
        private TextBox _refreshIntervalTextBox;
        private TextBox _sessionIdPartialTextBox;
        private int _refreshInterval = 10; // Default refresh interval in seconds
        private System.Timers.Timer _timer;
        private string _sessionIdPartial = "TheRoxDen"; // Default session ID partial search

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            _sessionsPanel = this.FindControl<WrapPanel>("SessionsPanel");
            _refreshIntervalTextBox = this.FindControl<TextBox>("RefreshIntervalTextBox");
            _sessionIdPartialTextBox = this.FindControl<TextBox>("SessionIdPartialTextBox");
            _refreshIntervalTextBox.Text = _refreshInterval.ToString();
            _sessionIdPartialTextBox.Text = _sessionIdPartial;
            SetRefreshTimer(_refreshInterval);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void SetRefreshTimer(int interval)
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Elapsed -= OnTimerElapsed;
            }

            _timer = new System.Timers.Timer(interval * 1000);
            _timer.Elapsed += OnTimerElapsed;
            _timer.Start();
        }

        private void OnTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Dispatcher.UIThread.InvokeAsync(FetchAndDisplayData);
        }

        private async void FetchAndDisplayData()
        {
            try
            {
                var allSessionsResponse = await _httpClient.GetStringAsync(AllSessionsUrl);
                var allSessions = JArray.Parse(allSessionsResponse);
                var processedSessionIds = new HashSet<string>();

                _sessionsPanel.Children.Clear();

                foreach (var session in allSessions)
                {
                    var sessionId = session["sessionId"].ToString();
                    if (sessionId.StartsWith("S-U-" + _sessionIdPartial, StringComparison.OrdinalIgnoreCase) && !processedSessionIds.Contains(sessionId))
                    {
                        processedSessionIds.Add(sessionId);
                        var sessionUrl = $"https://api.resonite.com/sessions/{sessionId}/";
                        var sessionData = await FetchSessionData(sessionUrl);
                        DisplaySession(sessionData);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching data: {ex.Message}");
            }
        }

        private async Task<JObject> FetchSessionData(string url)
        {
            try
            {
                var response = await _httpClient.GetStringAsync(url);
                return JObject.Parse(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching session data from {url}: {ex.Message}");
                return null;
            }
        }

        private void DisplaySession(JObject data)
        {
            if (data == null) return;

            var sessionName = data["name"].ToString();
            var joinedUsers = data["joinedUsers"].ToString();
            var activeUsers = data["activeUsers"].ToString();
            var totalJoinedUsers = data["totalJoinedUsers"].ToString();
            var totalActiveUsers = data["totalActiveUsers"].ToString();
            var maxUsers = data["maxUsers"].ToString();
            var sessionUsers = data["sessionUsers"];

            var card = new Border
            {
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(5),
                Padding = new Thickness(10),
                Background = new SolidColorBrush(Color.Parse("#3C3F41")),
                CornerRadius = new CornerRadius(10),
                Width = 240, // Adjust the width to fit multiple cards in a row
                Child = new StackPanel
                {
                    Children =
                    {
                        CreateColoredTextBlock(sessionName),
                        new TextBlock { Text = $"Joined Users: {joinedUsers}", Foreground = Brushes.LightBlue },
                        new TextBlock { Text = $"Active Users: {activeUsers}", Foreground = Brushes.LightBlue },
                        new TextBlock { Text = $"Total Joined Users: {totalJoinedUsers}", Foreground = Brushes.LightBlue },
                        new TextBlock { Text = $"Total Active Users: {totalActiveUsers}", Foreground = Brushes.LightBlue },
                        new TextBlock { Text = $"Max Users: {maxUsers}", Foreground = Brushes.LightBlue },
                        new TextBlock { Text = "Users in Session:", FontSize = 16, Margin = new Thickness(0, 10, 0, 0), Foreground = Brushes.White },
                        CreateUserList(sessionUsers)
                    }
                }
            };

            _sessionsPanel.Children.Add(card);
        }

        private TextBlock CreateColoredTextBlock(string text)
        {
            var textBlock = new TextBlock { Foreground = Brushes.White };
            var regex = new Regex(@"<color\s*=\s*['""]?(?<color>[^>'""]+)['""]?\s*>(?<content>.*?)<\/color>", RegexOptions.IgnoreCase);
            var lastIndex = 0;

            foreach (Match match in regex.Matches(text))
            {
                if (match.Index > lastIndex)
                {
                    textBlock.Inlines.Add(new Run { Text = text.Substring(lastIndex, match.Index - lastIndex) });
                }

                var colorName = match.Groups["color"].Value.ToLower();
                var content = match.Groups["content"].Value;

                var run = new Run { Text = content };
                run.Foreground = colorName switch
                {
                    "red" => Brushes.Red,
                    "purple" => Brushes.MediumPurple,
                    "green" => Brushes.Green,
                    "blue" => Brushes.Blue,
                    _ => Brushes.White,
                };

                textBlock.Inlines.Add(run);
                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < text.Length)
            {
                textBlock.Inlines.Add(new Run { Text = text.Substring(lastIndex) });
            }

            return textBlock;
        }

        private StackPanel CreateUserList(JToken sessionUsers)
        {
            var userList = new StackPanel();
            foreach (var user in sessionUsers)
            {
                var username = user["username"].ToString();
                var isPresent = (bool)user["isPresent"];
                var color = isPresent ? Brushes.Green : Brushes.Red;
                userList.Children.Add(new TextBlock { Text = $"- {username} (Present: {isPresent})", Foreground = color });
            }
            return userList;
        }

        private void OnSetRefreshInterval(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (int.TryParse(_refreshIntervalTextBox.Text, out int newInterval))
            {
                _refreshInterval = newInterval;
                SetRefreshTimer(_refreshInterval);
            }
            else
            {
            }
        }

        private void OnSearchSessionIdPartial(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _sessionIdPartial = _sessionIdPartialTextBox.Text;
            FetchAndDisplayData();
        }
    }
}
