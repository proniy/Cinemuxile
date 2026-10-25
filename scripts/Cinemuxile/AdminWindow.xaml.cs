using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Npgsql;
using System.Data;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Windows.Media.Effects;

namespace Курсач
{
    public partial class AdminWindow : Window
    {
        private readonly DatabaseManager db = new DatabaseManager();
        private DataTable userTable;
        private DataTable videoTable;
        private List<Theme> themes;
        private Theme currentTheme;
        private DataTable supportTicketsTable = new DataTable();

        public AdminWindow()
        {
            InitializeComponent();
            Loaded += AdminWindow_Loaded;
        }

        private void AdminWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAllData();
            InitializeThemes();

            // Загрузка последней темы
            var lastTheme = ThemeManager.LoadCurrentTheme();
            if (lastTheme != null && themes.Any(t => t.Name == lastTheme.Name))
            {
                cmbThemes.SelectedItem = themes.First(t => t.Name == lastTheme.Name);
            }
        }

        private void LoadAllData()
        {
            LoadUsers();
            LoadVideos();
            LoadSupportTickets();
        }

        private void LoadUsers()
        {
            try
            {
                using (var conn = db.GetConnection())
                {
                    var adapter = new NpgsqlDataAdapter(
                        "SELECT user_id, username, email, phone, role, is_active FROM users", conn);
                    userTable = new DataTable();
                    adapter.Fill(userTable);
                    dgUsers.ItemsSource = userTable.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки пользователей: {ex.Message}");
            }
        }

        private void LoadVideos()
        {
            try
            {
                var query = @"
                    SELECT 
                        video_id, title, type, 
                        EXTRACT(YEAR FROM release_date) as release_year,
                        rating, 
                        file_path, genres, poster_url, description
                    FROM videos
                    ORDER BY title";
                using (var conn = db.GetConnection())
                {
                    var adapter = new NpgsqlDataAdapter(query, conn);
                    videoTable = new DataTable();
                    adapter.Fill(videoTable);
                    dgVideos.ItemsSource = videoTable.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки видео: {ex.Message}");
            }
        }

        // === Управление темами ===
        private void InitializeThemes()
        {
            themes = ThemeManager.LoadThemes();
            cmbThemes.ItemsSource = themes;
            if (themes.Count > 0)
            {
                cmbThemes.SelectedItem = themes[0];
                currentTheme = themes[0];
                ApplyTheme(currentTheme);
            }
        }

        private void ApplyTheme(Theme theme)
        {
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

        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            if (cmbThemes.SelectedItem is Theme selectedTheme)
            {
                var defaultThemes = new List<Theme>
                {
                    new Theme { Name = "Темная", BackgroundColor = Colors.Black, BorderColor = Colors.DeepSkyBlue, TextColor = Colors.White, FontSize = 14 },
                    new Theme { Name = "Светлая", BackgroundColor = Colors.White, BorderColor = Colors.Black, TextColor = Colors.Black, FontSize = 14 }
                };
                var current = themes.Find(t => t.Name == selectedTheme.Name);
                var index = themes.IndexOf(current);
                var nextIndex = (index + 1) % themes.Count;
                cmbThemes.SelectedIndex = nextIndex;
                ApplyTheme(themes[nextIndex]);
            }
        }

        private void CreateTheme_Click(object sender, RoutedEventArgs e)
        {
            var editor = new ThemeEditorWindow();
            if (editor.ShowDialog() == true)
            {
                InitializeThemes();
                cmbThemes.SelectedItem = editor.ViewModel.CurrentTheme;
                ApplyTheme(editor.ViewModel.CurrentTheme);
            }
        }

        private void cmbThemes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbThemes.SelectedItem is Theme selectedTheme)
            {
                ApplyTheme(selectedTheme);
                ThemeManager.SaveCurrentTheme(selectedTheme);
            }
        }

        // === Работа с пользователями ===
        private void UserFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyUserFilter();
        }

        private void ResetUserFilter_Click(object sender, RoutedEventArgs e)
        {
            txtUserFilter.Text = string.Empty;
            dgUsers.ItemsSource = userTable.DefaultView;
        }

        private void ApplyUserFilter()
        {
            if (userTable == null) return;
            string filter = txtUserFilter.Text.Trim();
            if (string.IsNullOrEmpty(filter))
            {
                dgUsers.ItemsSource = userTable.DefaultView;
                return;
            }
            string safeFilter = filter.Replace("'", "''");
            var view = new DataView(userTable);
            view.RowFilter = $"username LIKE '%{safeFilter}%' OR email LIKE '%{safeFilter}%' OR phone LIKE '%{safeFilter}%'";
            dgUsers.ItemsSource = view;
        }

        private void AddUser_Click(object sender, RoutedEventArgs e)
        {
            var editWindow = new UserEditWindow(currentTheme); // Передаем текущую тему
            if (editWindow.ShowDialog() == true)
            {
                LoadUsers();
            }
        }

        private void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (dgUsers.SelectedItem is DataRowView row &&
                MessageBox.Show("Удалить пользователя?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                db.ExecuteQuery($"DELETE FROM users WHERE user_id = {row["user_id"]}");
                LoadUsers();
            }
        }

        // === Работа с видео ===
        private void VideoFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyVideoFilter();
        }

        private void ResetVideoFilter_Click(object sender, RoutedEventArgs e)
        {
            txtVideoFilter.Text = string.Empty;
            dgVideos.ItemsSource = videoTable.DefaultView;
        }

        private void ApplyVideoFilter()
        {
            if (videoTable == null) return;
            string filter = txtVideoFilter.Text.Trim();
            if (string.IsNullOrEmpty(filter))
            {
                dgVideos.ItemsSource = videoTable.DefaultView;
                return;
            }
            string safeFilter = filter.Replace("'", "''");
            var view = new DataView(videoTable);
            view.RowFilter = $"title LIKE '%{safeFilter}%'";
            dgVideos.ItemsSource = view;
        }

        private void AddVideo_Click(object sender, RoutedEventArgs e)
        {
            var editWindow = new VideoEditWindow();
            if (editWindow.ShowDialog() == true)
            {
                LoadVideos(); // Обновляем данные после добавления
            }
            LoadVideos();
        }

        private void DeleteVideo_Click(object sender, RoutedEventArgs e)
        {
            if (dgVideos.SelectedItem is DataRowView row &&
                MessageBox.Show("Удалить видео?", "Подтверждение",
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                db.ExecuteQuery($"DELETE FROM videos WHERE video_id = {row["video_id"]}");
                LoadVideos();
            }
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
        private void LoadSupportTickets(string filter = "")
        {
            if (dgSupportTickets == null)
            {
                //
                //MessageBox.Show("DataGrid не инициализирован!");
                return;
            }

            try
            {
                var query = @"
            SELECT 
                t.ticket_id, 
                u.username, 
                t.request_text, 
                t.category, 
                t.created_at, 
                t.is_resolved
            FROM support_tickets t
            JOIN users u ON t.user_id = u.user_id";

                // Фильтрация по статусу
                if (!chkShowResolved.IsChecked == true)
                {
                    query += " WHERE t.is_resolved = FALSE";
                }

                // Фильтрация по категории
                if (cmbCategoryFilter.SelectedItem is ComboBoxItem selectedCategory &&
                    selectedCategory.Content.ToString() != "Все категории")
                {
                    string category = selectedCategory.Content.ToString();
                    if (query.Contains("WHERE"))
                    {
                        query += $" AND t.category = '{category}'";
                    }
                    else
                    {
                        query += $" WHERE t.category = '{category}'";
                    }
                }

                // Фильтрация по тексту или пользователю
                if (!string.IsNullOrEmpty(filter))
                {
                    if (query.Contains("WHERE"))
                    {
                        query += $" AND (t.request_text ILIKE '%{filter}%' OR u.username ILIKE '%{filter}%')";
                    }
                    else
                    {
                        query += $" WHERE (t.request_text ILIKE '%{filter}%' OR u.username ILIKE '%{filter}%')";
                    }
                }

                query += " ORDER BY t.created_at DESC";

                using (var conn = db.GetConnection())
                {
                    var adapter = new NpgsqlDataAdapter(query, conn);
                    supportTicketsTable = new DataTable();
                    adapter.Fill(supportTicketsTable);
                    dgSupportTickets.ItemsSource = supportTicketsTable.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки обращений: {ex.Message}");
            }
        }

        private void SupportFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoadSupportTickets(txtSupportFilter.Text);
        }

        private void ResetSupportFilter_Click(object sender, RoutedEventArgs e)
        {
            txtSupportFilter.Text = string.Empty;
            chkShowResolved.IsChecked = false;
            cmbCategoryFilter.SelectedIndex = 0;
            LoadSupportTickets();
        }

        private void SupportFilter_Changed(object sender, RoutedEventArgs e)
        {
            LoadSupportTickets(txtSupportFilter.Text);
        }
        private void ChangeStatus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int ticketId)
            {
                var row = supportTicketsTable.AsEnumerable().FirstOrDefault(r => r.Field<int>("ticket_id") == ticketId);
                if (row != null)
                {
                    bool currentStatus = row.Field<bool>("is_resolved");
                    if (db.UpdateTicketStatus(ticketId, !currentStatus))
                    {
                        LoadSupportTickets(); // Обновляем таблицу
                    }
                }
            }
        }
        private void EditUser_Click(object sender, RoutedEventArgs e)
        {
            if (dgUsers.SelectedItem is DataRowView row)
            {
                var userId = Convert.ToInt32(row["user_id"]); // ✅ Всегда как int
                var editWindow = new UserEditWindow(currentTheme, userId.ToString());
                if (editWindow.ShowDialog() == true)
                {
                    LoadUsers();
                }
            }
        }
    }
}