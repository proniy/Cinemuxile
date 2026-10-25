using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using Npgsql;
using System.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using Newtonsoft.Json;
using System.Windows.Media.Effects;

namespace Курсач
{
    public partial class VideoEditWindow : Window
    {
        //private const string KINOPOISK_API_KEY = "705363e5-36e8-40f2-97fd-d242ef461b5a";
        private string KINOPOISK_API_KEY => DatabaseManager.ApiKey;
        private readonly DatabaseManager db = new DatabaseManager();
        private List<KinopoiskResult> currentResults = new List<KinopoiskResult>();
        private Theme currentTheme;

        public VideoEditWindow()
        {
            InitializeComponent();
            Loaded += VideoEditWindow_Loaded;
        }

        private void VideoEditWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Загрузка темы
            currentTheme = ThemeManager.LoadCurrentTheme() ?? new Theme
            {
                Name = "Темная",
                BackgroundColor = Colors.Black,
                BorderColor = Colors.DeepSkyBlue,
                TextColor = Colors.White,
                FontSize = 14,
                NeonEnabled = true,
                NeonBorderColor = Colors.DeepSkyBlue
            };

            ApplyTheme(currentTheme);
        }

        // === Применение темы ===
        private void ApplyTheme(Theme theme)
        {
            // Обновление фона окна
            this.Background = new SolidColorBrush(theme.BackgroundColor);
            mainBorder.Background = new SolidColorBrush(theme.BackgroundColor);
            mainBorder.BorderBrush = new SolidColorBrush(theme.NeonEnabled ? theme.NeonBorderColor : theme.BorderColor);

            // Обновление верхней панели
            dockPanelHeader.Background = new SolidColorBrush(theme.BackgroundColor);

            // Обновление всех элементов
            foreach (var element in FindVisualChildren<FrameworkElement>(this))
            {
                if (element is Control control)
                {
                    control.Background = new SolidColorBrush(theme.BackgroundColor);
                    control.Foreground = new SolidColorBrush(theme.TextColor);
                    control.BorderBrush = new SolidColorBrush(theme.NeonEnabled ? theme.NeonBorderColor : theme.BorderColor);
                    control.FontSize = theme.FontSize;
                }
                else if (element is TextBlock textBlock)
                {
                    textBlock.Foreground = new SolidColorBrush(theme.TextColor);
                    textBlock.FontSize = theme.FontSize;
                }
                else if (element is Border border)
                {
                    border.BorderBrush = new SolidColorBrush(theme.NeonEnabled ? theme.NeonBorderColor : theme.BorderColor);
                    border.Background = new SolidColorBrush(theme.BackgroundColor);
                }
            }

            // Неон-эффект
            if (theme.NeonEnabled)
            {
                this.Effect = new DropShadowEffect
                {
                    Color = theme.NeonBorderColor,
                    BlurRadius = 15,
                    ShadowDepth = 0,
                    Opacity = 0.8
                };
            }
            else
            {
                this.Effect = null;
            }
        }

        // === Поиск и сохранение видео ===
        private List<KinopoiskResult> SearchKinopoiskSync(string query)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("X-API-KEY", KINOPOISK_API_KEY);
                    var url = $"https://kinopoiskapiunofficial.tech/api/v2.1/films/search-by-keyword?keyword={query}&page=1";
                    var response = client.GetAsync(url).Result;
                    response.EnsureSuccessStatusCode();
                    var json = response.Content.ReadAsStringAsync().Result;
                    var responseObj = DeserializeKinopoiskResponse(json);
                    return responseObj.films;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка поиска: {ex.Message}");
                return new List<KinopoiskResult>();
            }
        }

        private KinopoiskSearchResponse DeserializeKinopoiskResponse(string json)
        {
            try
            {
                json = json.Replace(@"""null""", "null");
                return JsonConvert.DeserializeObject<KinopoiskSearchResponse>(json);
            }
            catch
            {
                MessageBox.Show("Ошибка десериализации данных");
                return new KinopoiskSearchResponse { films = new List<KinopoiskResult>() };
            }
        }

        private void SaveToDatabase(KinopoiskResult item)
        {
            using (var conn = db.GetConnection())
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand($@"
                    INSERT INTO videos (title, type, release_date, rating, kinopoisk_id, content_type, 
                                        poster_url, genres, description, file_path)
                    VALUES (@t, @y, @r, @rt, @kp, @ct, @pu, @g, @d, @fp)
                    ON CONFLICT (kinopoisk_id) DO NOTHING", conn))
                {
                    cmd.Parameters.AddWithValue("t", item.nameRu ?? item.nameEn);
                    cmd.Parameters.AddWithValue("y", item.type.ToLower() == "film" ? "movie" : "series");
                    cmd.Parameters.AddWithValue("r", item.year.HasValue ? new DateTime(item.year.Value, 1, 1) : (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("rt", item.Rating1 ?? 0.0);
                    cmd.Parameters.AddWithValue("kp", item.filmId.ToString());
                    cmd.Parameters.AddWithValue("ct", item.type.ToLower() == "FILM" ? "film" : "series");
                    cmd.Parameters.AddWithValue("pu", item.posterUrl);
                    cmd.Parameters.AddWithValue("g", item.GenresString);
                    cmd.Parameters.AddWithValue("d", item.description ?? "Описание не указано");
                    cmd.Parameters.AddWithValue("fp", GenerateSspoiskLink(item));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private string GenerateSspoiskLink(KinopoiskResult item)
        {
            string contentType = item.type == "FILM" ? "film" : "series";
            return $"https://flcksbr.top/{contentType}/{item.filmId}/";
        }

        // === Обработчики событий ===
        private void Search_Click(object sender, RoutedEventArgs e)
        {
            var query = txtSearchQuery.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                MessageBox.Show("Введите запрос для поиска");
                return;
            }
            currentResults = SearchKinopoiskSync(query);
            dgResults.ItemsSource = currentResults;
        }

        private void SaveSelected_Click(object sender, RoutedEventArgs e)
        {
            if (dgResults.SelectedItems.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы один фильм");
                return;
            }

            foreach (KinopoiskResult item in dgResults.SelectedItems)
            {
                SaveToDatabase(item);
            }

            MessageBox.Show($"Сохранено {dgResults.SelectedItems.Count} фильмов");
        }

        // === Управление окном ===
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => this.Close();
        private void Minimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        // === Вспомогательные методы ===
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

    // === Модели API ===
    public class KinopoiskSearchResponse
    {
        public List<KinopoiskResult> films { get; set; }
    }

    public class KinopoiskResult
    {
        public int filmId { get; set; }
        public string nameRu { get; set; }
        public string nameEn { get; set; }
        public string type { get; set; }
        public int? year { get; set; }
        public object rating { get; set; }
        public string posterUrl { get; set; }
        public List<KinopoiskGenre> genres { get; set; }
        public string description { get; set; }

        public string GenresString
        {
            get
            {
                return string.Join(", ", genres?.Select(g => g.genre) ?? new string[] { "Не указан" });
            }
        }

        public double? Rating1
        {
            get
            {
                if (rating == null || rating.ToString() == "null") return null;
                if (double.TryParse(rating.ToString(), out double value))
                {
                    return value;
                }
                return null;
            }
        }

        public string Name
        {
            get
            {
                if (!string.IsNullOrEmpty(nameRu) && !string.IsNullOrEmpty(nameEn))
                    return $"{nameRu} / {nameEn}";
                return nameRu ?? nameEn ?? "Без названия";
            }
        }
    }

    public class KinopoiskGenre
    {
        public string genre { get; set; }
    }
}