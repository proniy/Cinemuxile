using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Npgsql;
using BCrypt.Net;
using System.Windows.Input;

namespace Курсач
{
    public partial class LoginWindow : Window
    {
        private readonly DatabaseManager db = new DatabaseManager();
        private readonly string RememberMePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "remember_me.json");

        public LoginWindow()
        {
            InitializeComponent();
            Loaded += LoginWindow_Loaded;
            txtLogin.Focus();
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadRememberedCredentials();
        }

        // === Загрузка сохраненных данных ===
        private void LoadRememberedCredentials()
        {
            if (File.Exists(RememberMePath))
            {
                try
                {
                    var json = File.ReadAllText(RememberMePath);
                    var credentials = JsonSerializer.Deserialize<RememberedCredentials>(json);
                    if (credentials != null)
                    {
                        txtLogin.Text = credentials.Login;
                        txtPassword.Password = credentials.Password;
                        chkRememberMe.IsChecked = true;
                    }
                }
                catch
                {
                    MessageBox.Show("Ошибка загрузки сохраненных данных");
                    File.Delete(RememberMePath);
                }
            }
        }

        // === Сохранение данных при включении чекбокса ===
        private void SaveRememberedCredentials()
        {
            var credentials = new RememberedCredentials
            {
                Login = txtLogin.Text.Trim(),
                Password = txtPassword.Password
            };
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(credentials, options);
            File.WriteAllText(RememberMePath, json);
        }

        // === Очистка данных при отключении чекбокса ===
        private void ClearRememberedCredentials()
        {
            if (File.Exists(RememberMePath))
                File.Delete(RememberMePath);
        }

        // === Обработчики событий чекбокса ===
        private void RememberMe_Checked(object sender, RoutedEventArgs e)
        {
            SaveRememberedCredentials();
        }

        private void RememberMe_Unchecked(object sender, RoutedEventArgs e)
        {
            ClearRememberedCredentials();
        }

        // === Вход пользователя ===
        private void Login_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtLogin.Text))
            {
                MessageBox.Show("Введите логин/email/телефон!");
                return;
            }
            if (string.IsNullOrWhiteSpace(txtPassword.Password))
            {
                MessageBox.Show("Введите пароль!");
                return;
            }

            // Сохранение данных, если чекбокс отмечен
            if (chkRememberMe.IsChecked == true)
            {
                SaveRememberedCredentials();
            }
            else
            {
                ClearRememberedCredentials();
            }

            // Проверка аутентификации
            (bool success, string role, int userId) = db.ValidateUser(txtLogin.Text, txtPassword.Password);
            if (success)
            {
                OpenMainWindow(role, userId);
                this.Close();
            }
            else
            {
                MessageBox.Show("Неверные учетные данные!");
            }
        }

        private void OpenMainWindow(string role, int userId)
        {
            Window mainWindow;
            if (role == "admin")
            {
                mainWindow = new AdminWindow();
            }
            else
            {
                mainWindow = new ClientWindow(userId); // Передаем ID пользователя
            }
            mainWindow.Show();
            this.Close();
        }

        // === Регистрация и восстановление ===
        private void Register_Click(object sender, RoutedEventArgs e)
        {
            var regWindow = new RegistrationWindow();
            if (regWindow.ShowDialog() == true)
            {
                MessageBox.Show("Регистрация успешна!");
            }
        }

        private void Recover_Click(object sender, RoutedEventArgs e)
        {
            new RecoveryWindow().ShowDialog();
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
    }

    // === Модель сохранения данных ===
    public class RememberedCredentials
    {
        public string Login { get; set; }
        public string Password { get; set; }
    }
}