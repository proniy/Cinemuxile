using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Linq;
using System.Windows.Media.Imaging;
using System.Data;

namespace Курсач
{
    public partial class MoodMovieWindow : Window
    {
        private readonly DatabaseManager db = new DatabaseManager();
        private Theme currentTheme;
        private int currentStep = 0;
        private Dictionary<string, string> userAnswers = new Dictionary<string, string>();

        // Класс-обертка для фильма, чтобы постеры загружались принудительно
        public class MovieResult
        {
            public string title { get; set; }
            public string genres { get; set; }
            public string description { get; set; }
            public string file_path { get; set; }
            public string poster_url { get; set; }
            public ImageSource poster_image { get; set; }
        }

        private class Question
        {
            public string Text { get; set; }
            public Dictionary<string, string> Options { get; set; }
        }

        private List<Question> questions = new List<Question>
        {
            new Question { Text = "Как вы себя чувствуете сейчас?", Options = new Dictionary<string, string> { { "😊 Счастлив", "happy" }, { "😢 Грустно", "sad" }, { "😱 Тревожно", "scared" }, { "🔥 Заряжен", "excited" }, { "❤️ Романтично", "romantic" } } },
            new Question { Text = "Какой темп повествования вам ближе?", Options = new Dictionary<string, string> { { "🚀 Быстрый и захватывающий", "dynamic" }, { "☕ Медленный и атмосферный", "calm" } } },
            new Question { Text = "Какую атмосферу ищем?", Options = new Dictionary<string, string> { { "🌌 Фантастическую", "fantasy" }, { "🌍 Реалистичную", "realistic" }, { "🕵️ Загадочную", "mystery" } } },
            new Question { Text = "Насколько мрачным должен быть фильм?", Options = new Dictionary<string, string> { { "🌑 Очень мрачным", "dark" }, { "☀️ Светлым и позитивным", "light" } } },
            new Question { Text = "Что важнее в сюжете?", Options = new Dictionary<string, string> { { "📈 Напряжение и интрига", "tense" }, { "🎭 Эмоции и чувства", "emotional" } } },
            new Question { Text = "Масштаб событий?", Options = new Dictionary<string, string> { { "🌍 Эпический / Глобальный", "epic" }, { "🏠 Камерный / Личный", "mystery" } } },
            new Question { Text = "Степень странности?", Options = new Dictionary<string, string> { { "🌀 Сюрреалистично", "weird" }, { "✅ Логично", "realistic" } } }
        };

        public MoodMovieWindow(Theme theme)
        {
            InitializeComponent();
            this.currentTheme = theme;
            ApplyTheme();
            UpdateQuestion();
        }

        private void ApplyTheme()
        {
            if (currentTheme == null) return;
            this.Background = new SolidColorBrush(currentTheme.BackgroundColor);
            mainBorder.Background = new SolidColorBrush(currentTheme.BackgroundColor);
            mainBorder.BorderBrush = new SolidColorBrush(currentTheme.AccentColor);
            titleText.Foreground = new SolidColorBrush(currentTheme.TextColor);
            questionText.Foreground = new SolidColorBrush(currentTheme.TextColor);
            resultText.Foreground = new SolidColorBrush(currentTheme.TextColor);
            foreach (var btn in FindVisualChildren<Button>(this))
            {
                btn.Background = new SolidColorBrush(currentTheme.ButtonBackgroundColor);
                btn.Foreground = new SolidColorBrush(currentTheme.TextColor);
                btn.BorderBrush = new SolidColorBrush(currentTheme.BorderColor);
            }
        }

        private void OnAnswerSelected(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            string answer = btn.Tag.ToString();
            userAnswers[currentStep.ToString()] = answer;
            currentStep++;
            if (currentStep < questions.Count) UpdateQuestion();
            else ShowRecommendedMovies();
        }

        private void UpdateQuestion()
        {
            var currentQ = questions[currentStep];
            questionText.Text = currentQ.Text;
            optionsPanel.Children.Clear();
            foreach (var option in currentQ.Options) AddOptionButton(option.Key, option.Value);
        }

        private void AddOptionButton(string text, string tag)
        {
            var btn = new Button { Content = text, Tag = tag, Margin = new Thickness(5), Padding = new Thickness(10), Height = 40 };
            btn.Click += OnAnswerSelected;
            optionsPanel.Children.Add(btn);
        }

        private void ShowRecommendedMovies()
        {
            Dictionary<string, int> genreScores = new Dictionary<string, int>();
            foreach (var answer in userAnswers.Values)
            {
                var matchedGenres = MapAnswerToGenres(answer);
                foreach (var g in matchedGenres)
                {
                    if (genreScores.ContainsKey(g)) genreScores[g]++;
                    else genreScores[g] = 1;
                }
            }

            var sortedGenres = genreScores.OrderByDescending(x => x.Value).ToList();
            try
            {
                DataTable dt = null;

                if (sortedGenres.Count > 0)
                {
                    var topGenres = sortedGenres.Take(3).Select(x => x.Key).ToList();
                    string conditions = string.Join(" OR ", topGenres.Select(g => $"genres ILIKE '%{g}%'"));
                    string query = $"SELECT title, poster_url, genres, description, file_path FROM videos WHERE {conditions} ORDER BY RANDOM() LIMIT 5";
                    dt = db.GetDataTable(query);
                }

                if (dt == null || dt.Rows.Count == 0)
                {
                    string fallbackQuery = "SELECT title, poster_url, genres, description, file_path FROM videos ORDER BY RANDOM() LIMIT 5";
                    dt = db.GetDataTable(fallbackQuery);
                }

                if (dt != null && dt.Rows.Count > 0)
                {
                    // СОЗДАЕМ СПИСОК ОБЪЕКТОВ MovieResult С ПРЕДЗАГРУЗКОЙ ПОСТЕРОВ
                    List<MovieResult> results = new List<MovieResult>();
                    foreach (DataRow row in dt.Rows)
                    {
                        var movie = new MovieResult
                        {
                            title = row["title"]?.ToString(),
                            genres = row["genres"]?.ToString(),
                            description = row["description"]?.ToString(),
                            file_path = row["file_path"]?.ToString(),
                            poster_url = row["poster_url"]?.ToString()
                        };

                        string url = row["poster_url"]?.ToString();
                        if (!string.IsNullOrEmpty(url))
                        {
                            try
                            {
                                BitmapImage bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.UriSource = new Uri(url, UriKind.Absolute);
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                bitmap.EndInit();
                                movie.poster_image = bitmap;
                            }
                            catch { movie.poster_image = null; }
                        }
                        results.Add(movie);
                    }

                    moviesList.ItemsSource = results;
                    resultPanel.Visibility = Visibility.Visible;
                    questionsPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    resultText.Text = "База данных пуста.";
                    resultPanel.Visibility = Visibility.Visible;
                    questionsPanel.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                resultText.Text = $"Ошибка: {ex.Message}";
                resultPanel.Visibility = Visibility.Visible;
                questionsPanel. Visibility = Visibility.Collapsed;
            }
        }

        private void btnWatch_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            // Теперь данные приходят из объекта MovieResult
            var movie = btn.DataContext as MovieResult;
            string filePath = movie?.file_path;
            if (!string.IsNullOrEmpty(filePath))
            {
                try
                {
                    var webView = new WebViewWindow(filePath);
                    webView.Show();
                    this.Close(); 
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}");
                }
            }
        }

        private List<string> MapAnswerToGenres(string answer)
        {
            List<string> g = new List<string>();
            switch (answer)
            {
                case "happy": g.Add("комедия"); break;
                case "sad": g.Add("драма"); break;
                case "scared": g.Add("ужасы"); g.Add("триллер"); break;
                case "excited": g.Add("боевик"); g.Add("приключения"); break;
                case "romantic": g.Add("мелодрама"); g.Add("романтика"); break;
                case "dynamic": g.Add("боевик"); g.Add("приключения"); g.Add("фантастика"); break;
                case "calm": g.Add("драма"); break;
                case "fantasy": g.Add("фантастика"); g.Add("приключения"); g.Add("фэнтези"); break;
                case "realistic": g.Add("драма"); g.Add("биография"); break;
                case "mystery": g.Add("триллер"); g.Add("детектив"); break;
                case "dark": g.Add("ужасы"); g.Add("триллер"); break;
                case "light": g.Add("комедия"); g.Add("мультфильм"); break;
                case "tense": g.Add("триллер"); g.Add("боевик"); break;
                case "emotional": g.Add("драма"); g.Add("мелодрама"); break;
                case "epic": g.Add("фантастика"); g.Add("боевик"); break;
                case "weird": g.Add("фантастика"); g.Add("ужасы"); break;
            }
            return g;
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) { this.Close(); }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t) yield return t;
                foreach (var descendant in FindVisualChildren<T>(child)) yield return descendant;
            }
        }
    }
}