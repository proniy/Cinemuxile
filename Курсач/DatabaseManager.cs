using Npgsql;
using BCrypt.Net;
using System;
using System.Data;
using System.Text.RegularExpressions;
using System.Linq;
using System.Windows;

namespace Курсач
{
    public class DatabaseManager
    {
        // Убираем константу ConnectionString и используем SecureConfig
        public static string ConnectionString { get; set; }
        public static string ApiKey { get; set; }

        public NpgsqlConnection GetConnection()
        {
            return new NpgsqlConnection(ConnectionString);
        }


        /// <summary>
        /// Регистрация нового пользователя
        /// </summary>
        public bool RegisterUser(string username, string email, string phone, string password)
        {
            if (!ValidateCredentials(username, email, phone, password))
                return false;

            using (var conn = GetConnection()) // Используем GetConnection() вместо прямого использования ConnectionString
            {
                try
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(
                        "INSERT INTO users (username, email, phone, password_hash) " +
                        "VALUES (@username, @email, @phone, @hash)", conn))
                    {
                        cmd.Parameters.AddWithValue("username", username);
                        cmd.Parameters.AddWithValue("email", email);
                        cmd.Parameters.AddWithValue("phone", phone);
                        string hash = BCrypt.Net.BCrypt.EnhancedHashPassword(password, 13);
                        cmd.Parameters.AddWithValue("hash", hash);
                        int result = cmd.ExecuteNonQuery();
                        return result == 1;
                    }
                }
                catch (PostgresException ex)
                {
                    MessageBox.Show($"Ошибка регистрации: {GetUserFriendlyError(ex)}");
                    return false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Общая ошибка: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Аутентификация пользователя
        /// </summary>
        public (bool success, string role, int userId) ValidateUser(string login, string password)
        {
            using (var conn = GetConnection()) // Используем GetConnection()
            {
                try
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(
                        "SELECT user_id, password_hash, role, is_active FROM users " +
                        "WHERE username = @login OR email = @login OR phone = @login", conn))
                    {
                        cmd.Parameters.AddWithValue("login", login);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                if (!reader.GetBoolean(3)) // Проверка is_active
                                {
                                    MessageBox.Show("Аккаунт деактивирован!");
                                    return (false, null, -1);
                                }
                                if (BCrypt.Net.BCrypt.EnhancedVerify(password, reader.GetString(1)))
                                {
                                    int userId = reader.GetInt32(0);
                                    return (true, reader.GetString(2), userId);
                                }
                            }
                        }
                    }
                    return (false, null, -1);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка аутентификации: {ex.Message}");
                    return (false, null, -1);
                }
            }
        }

        /// <summary>
        /// Восстановление пароля
        /// </summary>
        public bool RequestPasswordReset(string login)
        {
            using (var conn = GetConnection()) // Используем GetConnection()
            {
                try
                {
                    conn.Open();
                    string tempPassword = GenerateTemporaryPassword();
                    string hash = BCrypt.Net.BCrypt.EnhancedHashPassword(tempPassword, 13);
                    using (var cmd = new NpgsqlCommand(
                        "UPDATE users SET password_hash = @hash, " +
                        "temporary_password_expiry = NOW() + INTERVAL '1 hour' " +
                        "WHERE username = @login OR email = @login OR phone = @login", conn))
                    {
                        cmd.Parameters.AddWithValue("login", login);
                        cmd.Parameters.AddWithValue("hash", hash);
                        int affected = cmd.ExecuteNonQuery();
                        if (affected == 1)
                        {
                            // Здесь должна быть реальная отправка пароля
                            MessageBox.Show($"Временный пароль: {tempPassword}");
                            return true;
                        }
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка восстановления: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Генерация временного пароля
        /// </summary>
        private string GenerateTemporaryPassword(int length = 8)
        {
            const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
            var random = new Random();
            return new string(Enumerable.Repeat(validChars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        /// <summary>
        /// Валидация вводимых данных
        /// </summary>
        private bool ValidateCredentials(string username, string email, string phone, string password)
        {
            // Проверка логина
            if (string.IsNullOrWhiteSpace(username) || username.Length < 4)
            {
                MessageBox.Show("Логин должен содержать минимум 4 символа!");
                return false;
            }
            // Проверка email
            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                MessageBox.Show("Некорректный формат email!");
                return false;
            }
            // Проверка телефона
            if (!Regex.IsMatch(phone, @"^\+?[1-9]\d{1,14}$"))
            {
                MessageBox.Show("Некорректный формат телефона!");
                return false;
            }
            // Проверка пароля
            if (password.Length < 6)
            {
                MessageBox.Show("Пароль должен содержать минимум 6 символов!");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Преобразование ошибок БД в понятный текст
        /// </summary>
        public string GetUserFriendlyError(PostgresException ex)
        {
            if (ex.SqlState == "23505")
            {
                switch (ex.ConstraintName)
                {
                    case "users_username_key": return "Логин занят";
                    case "users_email_key": return "Email уже используется";
                    case "users_phone_key": return "Телефон уже зарегистрирован";
                    default: return "Ошибка уникальности данных";
                }
            }
            return ex.Message;
        }

        /// <summary>
        /// Проверка существования пользователя
        /// </summary>
        public bool UserExists(string login)
        {
            using (var conn = GetConnection()) // Используем GetConnection()
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(
                    "SELECT COUNT(1) FROM users " +
                    "WHERE username = @login OR email = @login OR phone = @login", conn))
                {
                    cmd.Parameters.AddWithValue("login", login);
                    return (long)cmd.ExecuteScalar() > 0;
                }
            }
        }

        /// <summary>
        /// Обновление постоянного пароля
        /// </summary>
        public bool UpdatePassword(string login, string newPassword)
        {
            using (var conn = GetConnection()) // Используем GetConnection()
            {
                try
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(
                        "UPDATE users SET password_hash = @hash, " +
                        "temporary_password = NULL, temporary_password_expiry = NULL " +
                        "WHERE username = @login OR email = @login OR phone = @login", conn))
                    {
                        cmd.Parameters.AddWithValue("login", login);
                        string hash = BCrypt.Net.BCrypt.EnhancedHashPassword(newPassword, 13);
                        cmd.Parameters.AddWithValue("hash", hash);
                        return cmd.ExecuteNonQuery() == 1;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка обновления пароля: {ex.Message}");
                    return false;
                }
            }
        }

        public void ExecuteQuery(string query)
        {
            using (var conn = GetConnection()) // Используем GetConnection()
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public DataTable GetDataTable(string query)
        {
            using (var conn = GetConnection()) // Используем GetConnection()
            {
                var adapter = new NpgsqlDataAdapter(query, conn);
                var dt = new DataTable();
                adapter.Fill(dt);
                return dt;
            }
        }

        /// <summary>
        /// Добавление нового обращения в техподдержку
        /// </summary>
        public bool SubmitSupportTicket(int userId, string requestText, string category)
        {
            using (var conn = GetConnection()) // Используем GetConnection()
            {
                try
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(
                        "INSERT INTO support_tickets (user_id, request_text, category) " +
                        "VALUES (@userId, @requestText, @category)", conn))
                    {
                        cmd.Parameters.AddWithValue("userId", userId);
                        cmd.Parameters.AddWithValue("requestText", requestText);
                        cmd.Parameters.AddWithValue("category", category);
                        return cmd.ExecuteNonQuery() == 1;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка отправки запроса: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Обновление статуса обращения
        /// </summary>
        public bool UpdateTicketStatus(int ticketId, bool isResolved)
        {
            using (var conn = GetConnection()) // Используем GetConnection()
            {
                try
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(
                        "UPDATE support_tickets SET is_resolved = @status WHERE ticket_id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("status", isResolved);
                        cmd.Parameters.AddWithValue("id", ticketId);
                        return cmd.ExecuteNonQuery() == 1;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка обновления статуса: {ex.Message}");
                    return false;
                }
            }
        }
    }
}