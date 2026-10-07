using Npgsql;
using BCrypt.Net;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Collections.Generic;

namespace Курсач
{
    public partial class UserEditWindow : Window
    {
        private readonly DatabaseManager db = new DatabaseManager();
        private readonly string _userId;
        private Theme currentTheme;

        // Конструктор для создания нового пользователя
        public UserEditWindow(Theme theme) : this(theme, null) { }

        // Конструктор для редактирования существующего пользователя
        public UserEditWindow(Theme theme, string userId = null)
        {
            InitializeComponent();
            currentTheme = theme;
            _userId = userId;
            Loaded += UserEditWindow_Loaded;

            if (userId != null)
            {
                // Скрываем поля, которые не должен редактировать клиент
                cbRole.Visibility = Visibility.Collapsed;
                chkActive.Visibility = Visibility.Collapsed;
                txtUsername.IsReadOnly = true;
            }
        }

        private void UserEditWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadUserData();
            ApplyTheme(currentTheme);
        }

        private void LoadUserData()
        {
            if (string.IsNullOrEmpty(_userId)) return;

            using (var conn = db.GetConnection())
            {
                conn.Open();
                var cmd = new NpgsqlCommand(
                    "SELECT username, email, phone, role, is_active FROM users WHERE user_id = @id", conn);

                // ✅ Преобразуем строку в целое число
                if (int.TryParse(_userId, out int userId))
                {
                    cmd.Parameters.AddWithValue("id", userId); // Передаем как integer
                }
                else
                {
                    MessageBox.Show("Некорректный идентификатор пользователя");
                    return;
                }

                using (var reader = cmd.ExecuteReader()) // 🔥 Теперь работает без ошибок
                {
                    if (reader.Read())
                    {
                        txtUsername.Text = reader.GetString(0);
                        txtEmail.Text = reader.GetString(1);
                        txtPhone.Text = reader.GetString(2);
                        cbRole.Text = reader.GetString(3);
                        chkActive.IsChecked = reader.GetBoolean(4);
                    }
                }
            }
        }

        private void ApplyTheme(Theme theme)
        {
            this.Background = new SolidColorBrush(theme.BackgroundColor);
            mainBorder.Background = new SolidColorBrush(theme.BackgroundColor);
            mainBorder.BorderBrush = new SolidColorBrush(theme.NeonEnabled ? theme.NeonBorderColor : theme.BorderColor);

            dockPanelHeader.Background = new SolidColorBrush(theme.BackgroundColor);

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

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var conn = db.GetConnection())
                {
                    conn.Open();
                    var cmd = new NpgsqlCommand();

                    if (string.IsNullOrEmpty(_userId))
                    {
                        // Регистрация нового пользователя
                        cmd.CommandText = @"INSERT INTO users 
                    (username, email, phone, password_hash, role, is_active)
                    VALUES (@u, @e, @p, @h, @r, @a)";

                        var hash = BCrypt.Net.BCrypt.EnhancedHashPassword(txtPassword.Password, 13);
                        cmd.Parameters.AddWithValue("h", hash);
                    }
                    else
                    {
                        // Обновление существующего пользователя
                        cmd.CommandText = @"UPDATE users SET
                    username = @u,
                    email = @e,
                    phone = @p,
                    role = @r,
                    is_active = @a";

                        // Добавляем обновление пароля только если он был введён
                        if (!string.IsNullOrEmpty(txtPassword.Password))
                        {
                            var hash = BCrypt.Net.BCrypt.EnhancedHashPassword(txtPassword.Password, 13);
                            cmd.CommandText += ", password_hash = @h";
                            cmd.Parameters.AddWithValue("h", hash);
                        }

                        cmd.CommandText += " WHERE user_id = @id";

                        // ✅ Преобразуем _userId в int
                        if (!int.TryParse(_userId, out int userId))
                        {
                            MessageBox.Show("Некорректный идентификатор пользователя");
                            return;
                        }
                        cmd.Parameters.AddWithValue("id", userId); // ✅ Передаем как integer
                    }

                    // Общие параметры
                    cmd.Connection = conn;
                    cmd.Parameters.AddWithValue("u", txtUsername.Text);
                    cmd.Parameters.AddWithValue("e", txtEmail.Text);
                    cmd.Parameters.AddWithValue("p", txtPhone.Text);
                    cmd.Parameters.AddWithValue("r", cbRole.Text);
                    cmd.Parameters.AddWithValue("a", chkActive.IsChecked ?? false);

                    cmd.ExecuteNonQuery();
                    DialogResult = true;
                    Close();
                }
            }
            catch (PostgresException ex)
            {
                MessageBox.Show(db.GetUserFriendlyError(ex));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}");
            }
        }

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

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

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