using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Курсач
{
    public partial class WebViewWindow : Window
    {
        private readonly string VideoUrl;
        private readonly string ExtensionPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ad");
        private bool _isInitialized = false;
        private bool _isClosing = false;

        // Делегаты для событий
        private EventHandler<CoreWebView2InitializationCompletedEventArgs> _coreWebView2InitializationCompletedHandler;
        private EventHandler<object> _containsFullScreenElementChangedHandler;
        private EventHandler<CoreWebView2NewWindowRequestedEventArgs> _newWindowRequestedHandler;
        private EventHandler<CoreWebView2ProcessFailedEventArgs> _processFailedHandler;

        public WebViewWindow(string url)
        {
            try
            {
                InitializeComponent();
                VideoUrl = url;

                // Инициализируем обработчики событий
                InitializeEventHandlers();

                // Подписываемся на глобальные обработчики ошибок
                Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

                Loaded += WebViewWindow_Loaded;
            }
            catch (Exception ex)
            {
                ShowError($"Критическая ошибка при создании окна: {ex.Message}");
            }
        }

        private void InitializeEventHandlers()
        {
            _coreWebView2InitializationCompletedHandler = (sender, e) => WebView2_CoreWebView2InitializationCompleted(sender, e);
            _containsFullScreenElementChangedHandler = (sender, e) => CoreWebView2_ContainsFullScreenElementChanged(sender, e);
            _newWindowRequestedHandler = (sender, e) => CoreWebView2_NewWindowRequested(sender, e);
            _processFailedHandler = (sender, e) => CoreWebView2_ProcessFailed(sender, e);
        }

        private async void WebViewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(VideoUrl))
                {
                    ShowError("URL видео не может быть пустым.");
                    return;
                }

                if (!Uri.TryCreate(VideoUrl, UriKind.Absolute, out Uri uri))
                {
                    ShowError($"Неверный формат URL: {VideoUrl}");
                    return;
                }

                // Укажите путь к кэшу
                string cacheFolder = Path.Combine(Path.GetTempPath(), "WebView2Cache");

                try
                {
                    // Создаём опции окружения
                    var options = new CoreWebView2EnvironmentOptions();
                    options.AreBrowserExtensionsEnabled = true;

                    // Инициализация WebView2 с указанием опций
                    var env = await CoreWebView2Environment.CreateAsync(null, cacheFolder, options);

                    // Подписываемся на событие инициализации до EnsureCoreWebView2Async
                    webView2.CoreWebView2InitializationCompleted += _coreWebView2InitializationCompletedHandler;

                    await webView2.EnsureCoreWebView2Async(env);

                    _isInitialized = true;

                    // Загружаем AdGuard как локальное расширение
                    if (Directory.Exists(ExtensionPath))
                    {
                        try
                        {
                            var profile = webView2.CoreWebView2.Profile;
                            await LoadExtension(profile, ExtensionPath);
                        }
                        catch (Exception extEx)
                        {
                            // Не блокируем загрузку видео из-за ошибки расширения
                            LogError($"Ошибка загрузки расширения: {extEx.Message}");
                        }
                    }

                    // JS для разрешения полноэкранного режима
                    try
                    {
                        await webView2.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                            document.querySelectorAll('video').forEach(v => {
                                v.setAttribute('allowfullscreen', '');
                                v.setAttribute('allow', 'fullscreen');
                            });
                        ");
                    }
                    catch (Exception jsEx)
                    {
                        LogError($"Ошибка выполнения JavaScript: {jsEx.Message}");
                    }

                    // Загрузка видео
                    webView2.Source = uri;
                }
                catch (Exception initEx)
                {
                    ShowError($"Ошибка инициализации WebView2: {initEx.Message}");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Неожиданная ошибка при загрузке окна: {ex.Message}");
            }
        }

        private void WebView2_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                ShowError($"Ошибка инициализации CoreWebView2: {e.InitializationException?.Message}");
                return;
            }

            try
            {
                // Подписка на события CoreWebView2
                if (webView2.CoreWebView2 != null)
                {
                    webView2.CoreWebView2.ContainsFullScreenElementChanged += _containsFullScreenElementChangedHandler;
                    webView2.CoreWebView2.NewWindowRequested += _newWindowRequestedHandler;
                    webView2.CoreWebView2.ProcessFailed += _processFailedHandler;
                }
            }
            catch (Exception ex)
            {
                LogError($"Ошибка при подписке на события CoreWebView2: {ex.Message}");
            }
        }

        private async Task LoadExtension(CoreWebView2Profile profile, string extensionFolderPath)
        {
            try
            {
                if (profile == null)
                    throw new ArgumentNullException(nameof(profile));

                if (!Directory.Exists(extensionFolderPath))
                {
                    LogError($"Папка расширения не существует: {extensionFolderPath}");
                    return;
                }

                // Проверяем наличие manifest.json
                string manifestPath = Path.Combine(extensionFolderPath, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    LogError($"Файл manifest.json не найден в папке расширения: {extensionFolderPath}");
                    return;
                }

                // Загружаем расширение из локальной папки
                var extension = await profile.AddBrowserExtensionAsync(extensionFolderPath);

                if (extension != null)
                {
                    LogInfo("AdGuard успешно загружен из локальной папки.");
                }
                else
                {
                    LogError("Не удалось загрузить AdGuard. Проверьте путь к расширению.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка загрузки расширения: {ex.Message}\n" +
                                  "Убедитесь, что:\n" +
                                  "1. Расширение AdGuard распаковано корректно.\n" +
                                  "2. manifest.json доступен в папке.\n" +
                                  "3. SDK WebView2 актуален (≥ 1.0.1519.0).", ex);
            }
        }

        private void CoreWebView2_ContainsFullScreenElementChanged(object sender, object e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (webView2.CoreWebView2?.ContainsFullScreenElement == true)
                    {
                        // Переключаем окно в полноэкранный режим
                        this.WindowStyle = WindowStyle.None;
                        this.ResizeMode = ResizeMode.NoResize;
                        this.WindowState = WindowState.Maximized;
                    }
                    else
                    {
                        // Восстанавливаем окно
                        this.WindowStyle = WindowStyle.SingleBorderWindow;
                        this.ResizeMode = ResizeMode.CanResize;
                        this.WindowState = WindowState.Normal;
                    }
                });
            }
            catch (Exception ex)
            {
                LogError($"Ошибка при переключении полноэкранного режима: {ex.Message}");
            }
        }

        private void CoreWebView2_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            try
            {
                // Блокируем открытие новых окон
                e.Handled = true;

                // Открываем ссылку в системном браузере
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = e.Uri,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                LogError($"Ошибка при обработке нового окна: {ex.Message}");
            }
        }

        private void CoreWebView2_ProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs e)
        {
            try
            {
                string processType = "Неизвестный процесс";

                // Заменяем switch на if-else для совместимости с C# 7.3
                if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited)
                {
                    processType = "Браузерный процесс";
                }
                else if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessExited)
                {
                    processType = "Рендер процесс";
                }
                else if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessUnresponsive)
                {
                    processType = "Рендер процесс не отвечает";
                }

                LogError($"Сбой процесса WebView2: {processType}. Попытка перезагрузки...");

                // Пытаемся перезагрузить страницу
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (!_isClosing && webView2 != null && webView2.CoreWebView2 != null)
                        {
                            webView2.Reload();
                        }
                    }
                    catch (Exception reloadEx)
                    {
                        LogError($"Ошибка при перезагрузке: {reloadEx.Message}");
                    }
                }));
            }
            catch (Exception ex)
            {
                LogError($"Ошибка при обработке сбоя процесса: {ex.Message}");
            }
        }

        private void WebViewWindow_Closed(object sender, EventArgs e)
        {
            try
            {
                _isClosing = true;

                // Отписываемся от событий
                Application.Current.DispatcherUnhandledException -= Current_DispatcherUnhandledException;
                AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
                TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;

                if (webView2 != null)
                {
                    try
                    {
                        // Останавливаем воспроизведение видео и аудио
                        if (webView2.CoreWebView2 != null)
                        {
                            webView2.CoreWebView2.ExecuteScriptAsync("document.querySelectorAll('video,audio').forEach(el => el.pause());");

                            // Отписываемся от событий CoreWebView2
                            webView2.CoreWebView2.ContainsFullScreenElementChanged -= _containsFullScreenElementChangedHandler;
                            webView2.CoreWebView2.NewWindowRequested -= _newWindowRequestedHandler;
                            webView2.CoreWebView2.ProcessFailed -= _processFailedHandler;
                        }

                        // Очищаем WebView2
                        webView2.CoreWebView2?.NavigateToString("<html><body></body></html>");
                    }
                    catch (Exception cleanupEx)
                    {
                        LogError($"Ошибка при очистке WebView2: {cleanupEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Ошибка при закрытии окна: {ex.Message}");
            }
        }

        #region Глобальные обработчики ошибок

        private void Current_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogError($"Необработанное исключение в Dispatcher: {e.Exception.Message}");
            e.Handled = true; // Предотвращаем крах приложения
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            LogError($"Необработанное исключение в домене приложения: {ex?.Message}");
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            LogError($"Необработанное исключение в задаче: {e.Exception.Message}");
            e.SetObserved(); // Помечаем как обработанное
        }

        #endregion

        #region Вспомогательные методы

        private void ShowError(string message)
        {
            try
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    MessageBox.Show(this, message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }));
            }
            catch
            {
                // Если Dispatcher недоступен, выводим в консоль
                Console.WriteLine($"Ошибка: {message}");
            }
        }

        private void LogError(string message)
        {
            // В реальном приложении здесь можно добавить запись в лог-файл
            Console.WriteLine($"[ERROR] {DateTime.Now:HH:mm:ss} - {message}");

            // Также можно показать уведомление пользователю
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Можно добавить статус бар или другое ненавязчивое уведомление
                }));
            }
            catch
            {
                // Игнорируем ошибки при логировании
            }
        }

        private void LogInfo(string message)
        {
            // Логирование информационных сообщений
            Console.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss} - {message}");
        }

        #endregion

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                _isClosing = true;
                base.OnClosing(e);
            }
            catch (Exception ex)
            {
                LogError($"Ошибка при закрытии окна: {ex.Message}");
            }
        }
    }
}