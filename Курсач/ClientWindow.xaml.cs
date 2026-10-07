using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Npgsql;
using System.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Linq;
using System.Windows.Media.Effects;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Windows.Media.Animation;

namespace Курсач
{
    public partial class ClientWindow : Window
    {
        private readonly DatabaseManager db = new DatabaseManager();
        private DataTable videoTable;
        private List<Theme> themes;
        private Theme currentTheme;
        private int currentUserId = -1;
        private DataRowView selectedVideo;
        private bool isDetailVisible = false;
        private WindowState previousWindowState;

        // Поля для пасхалок
        private string lastSearchText = "";
        private DateTime lastSearchTime = DateTime.MinValue;
        private DateTime lastSpellCastTime = DateTime.MinValue;

        // Флаг для предотвращения конфликтов анимаций
        private bool isAnimationRunning = false;

        public ClientWindow(int userId)
        {
            InitializeComponent();
            currentUserId = userId;
            Loaded += ClientWindow_Loaded;
        }

        private void ClientWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadVideos();
            InitializeThemes();

            var lastTheme = ThemeManager.LoadCurrentTheme();
            if (lastTheme != null && themes.Any(t => t.Name == lastTheme.Name))
            {
                cmbThemes.SelectedItem = themes.First(t => t.Name == lastTheme.Name);
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

                    itemsControlVideos.ItemsSource = videoTable.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}");
            }
        }

        private void InitializeThemes()
        {
            themes = ThemeManager.LoadThemes();
            cmbThemes.ItemsSource = themes;
            if (themes.Count > 0)
            {
                cmbThemes.SelectedItem = themes[1];
                currentTheme = themes[1];
                ApplyTheme(currentTheme);
            }
        }

        private void ApplyTheme(Theme theme)
        {
            if (theme == null) return;

            try
            {
                // Применяем тему ко всем основным элементам
                mainBorder.Background = new SolidColorBrush(theme.BackgroundColor);
                mainBorder.BorderBrush = new SolidColorBrush(theme.AccentColor);

                // Применяем тему к верхней панели
                headerBorder.Background = new SolidColorBrush(theme.HeaderBackgroundColor);

                // Применяем тему к логотипу и заголовку
                if (logoTextBlock != null)
                    logoTextBlock.Foreground = new SolidColorBrush(theme.TextColor);

                if (titleTextBlock != null)
                    titleTextBlock.Foreground = new SolidColorBrush(theme.TextColor);

                // Применяем тему к поисковой строке
                if (txtFilter != null)
                    txtFilter.Foreground = new SolidColorBrush(theme.TextColor);
                txtFilter.Background = new SolidColorBrush(theme.BackgroundColor);
                txtFilter.BorderThickness = new Thickness(1);
                txtFilter.BorderBrush = new SolidColorBrush(theme.BorderColor);

                if (searchPlaceholderTextBlock != null)
                    searchPlaceholderTextBlock.Foreground = new SolidColorBrush(theme.TextColor);

                // Применяем тему к детальной панели
                detailBorder.Background = new SolidColorBrush(theme.DetailBackgroundColor);
                detailBorder.BorderBrush = new SolidColorBrush(theme.AccentColor);

                // Создаем кисти для цветов текста карточки
                this.Resources["CardTitleColorBrush"] = new SolidColorBrush(theme.TextColor);
                this.Resources["CardYearColorBrush"] = new SolidColorBrush(theme.TextColor);
                this.Resources["CardTypeColorBrush"] = new SolidColorBrush(theme.TextColor);
                this.Resources["CardGenresColorBrush"] = new SolidColorBrush(theme.TextColor);

                // Применяем тему к текстовым элементам во всем окне
                ApplyTextTheme(theme);

                // Применяем тему к ComboBox (но текст остается черным благодаря стилю)
                if (cmbThemes != null)
                {
                    cmbThemes.Background = new SolidColorBrush(theme.BackgroundColor);
                    cmbThemes.Foreground = new SolidColorBrush(theme.TextColor);
                    cmbThemes.BorderBrush = new SolidColorBrush(theme.AccentColor);
                }

                // Применяем тему к карточкам - ВАЖНО: сохраняем обводку
                var cardStyle = new Style(typeof(Border));
                cardStyle.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(theme.CardBackgroundColor)));
                cardStyle.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(theme.BorderColor)));
                cardStyle.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1))); // Обводка всегда включена
                cardStyle.Setters.Add(new Setter(Border.MarginProperty, new Thickness(8)));
                cardStyle.Setters.Add(new Setter(Border.WidthProperty, 240.0));
                cardStyle.Setters.Add(new Setter(Border.HeightProperty, 470.0)); // Уменьшенная высота
                cardStyle.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(8)));
                this.Resources["ModernCardStyle"] = cardStyle;

                // Применяем тему к кнопкам - ИСПРАВЛЕНО: сохраняем ControlTemplate
                var buttonStyle = new Style(typeof(Button));
                buttonStyle.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(theme.ButtonBackgroundColor)));
                buttonStyle.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(theme.TextColor)));
                buttonStyle.Setters.Add(new Setter(Button.BorderBrushProperty, new SolidColorBrush(theme.BorderColor)));
                buttonStyle.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(1)));
                buttonStyle.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(12, 6, 12, 6)));
                buttonStyle.Setters.Add(new Setter(Button.FontSizeProperty, 12.0));
                buttonStyle.Setters.Add(new Setter(Button.FontWeightProperty, FontWeights.SemiBold));
                buttonStyle.Setters.Add(new Setter(Button.CursorProperty, Cursors.Hand));

                // Устанавливаем ControlTemplate для кнопок - ИСПРАВЛЕНО
                var buttonTemplate = new ControlTemplate(typeof(Button));
                var buttonBorderFactory = new FrameworkElementFactory(typeof(Border));
                buttonBorderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
                buttonBorderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
                buttonBorderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
                buttonBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

                var contentPresenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
                contentPresenterFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                contentPresenterFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

                buttonBorderFactory.AppendChild(contentPresenterFactory);
                buttonTemplate.VisualTree = buttonBorderFactory;
                buttonStyle.Setters.Add(new Setter(Button.TemplateProperty, buttonTemplate));

                this.Resources["ModernButtonStyle"] = buttonStyle;

                // Применяем тему к кнопкам верхней панели - ИСПРАВЛЕНО
                var headerButtonStyle = new Style(typeof(Button));
                headerButtonStyle.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Transparent));
                headerButtonStyle.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(theme.TextColor)));
                headerButtonStyle.Setters.Add(new Setter(Button.BorderBrushProperty, new SolidColorBrush(theme.BorderColor)));
                headerButtonStyle.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(1)));
                headerButtonStyle.Setters.Add(new Setter(Button.WidthProperty, 40.0));
                headerButtonStyle.Setters.Add(new Setter(Button.HeightProperty, 40.0));
                headerButtonStyle.Setters.Add(new Setter(Button.FontSizeProperty, 14.0));
                headerButtonStyle.Setters.Add(new Setter(Button.CursorProperty, Cursors.Hand));

                // Устанавливаем ControlTemplate для кнопок заголовка - ИСПРАВЛЕНО
                var headerButtonTemplate = new ControlTemplate(typeof(Button));
                var headerButtonBorderFactory = new FrameworkElementFactory(typeof(Border));
                headerButtonBorderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
                headerButtonBorderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
                headerButtonBorderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
                headerButtonBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

                var headerContentPresenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
                headerContentPresenterFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                headerContentPresenterFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

                headerButtonBorderFactory.AppendChild(headerContentPresenterFactory);
                headerButtonTemplate.VisualTree = headerButtonBorderFactory;
                headerButtonStyle.Setters.Add(new Setter(Button.TemplateProperty, headerButtonTemplate));

                this.Resources["HeaderButtonStyle"] = headerButtonStyle;

                // Применяем тему к ComboBox
                var comboStyle = new Style(typeof(ComboBox));
                comboStyle.Setters.Add(new Setter(ComboBox.BackgroundProperty, new SolidColorBrush(theme.BackgroundColor)));
                comboStyle.Setters.Add(new Setter(ComboBox.ForegroundProperty, new SolidColorBrush(theme.TextColor)));
                comboStyle.Setters.Add(new Setter(ComboBox.BorderBrushProperty, new SolidColorBrush(theme.BorderColor)));
                comboStyle.Setters.Add(new Setter(ComboBox.BorderThicknessProperty, new Thickness(1)));
                comboStyle.Setters.Add(new Setter(ComboBox.PaddingProperty, new Thickness(10, 8, 10, 8)));
                comboStyle.Setters.Add(new Setter(ComboBox.FontSizeProperty, 12.0));
                this.Resources["ModernComboBoxStyle"] = comboStyle;

                // ОБНОВЛЕНИЕ ТЕНИ КНОПКИ ВОСПРОИЗВЕДЕНИЯ - тень в цвет текста
                if (btnDetailPlay != null)
                {
                    var shadowEffect = new DropShadowEffect
                    {
                        Color = theme.TextColor, // Используем цвет текста кнопки
                        BlurRadius = 12,
                        ShadowDepth = 3,
                        Opacity = 0.6
                    };
                    btnDetailPlay.Effect = shadowEffect;
                }
                this.Background = new SolidColorBrush(theme.BackgroundColor);
                mainBorder.Background = new SolidColorBrush(theme.BackgroundColor);
                mainBorder.BorderBrush = new SolidColorBrush(theme.NeonEnabled ? theme.NeonBorderColor : theme.BorderColor);

                // Обновление всех элементов, КРОМЕ бейджей с фиксированными цветами
                foreach (var element in FindVisualChildren<FrameworkElement>(this))
                {
                    // Пропускаем бейджи с фиксированными цветами по имени
                    if (element.Name == "yearBadge" || element.Name == "typeBadge" || element.Name == "ratingBadge")
                        continue;

                    if (element is Control control)
                    {
                        control.Background = new SolidColorBrush(theme.BackgroundColor);
                        control.Foreground = new SolidColorBrush(theme.TextColor);
                        control.BorderBrush = new SolidColorBrush(theme.NeonEnabled ? theme.NeonBorderColor : theme.BorderColor);
                        control.FontSize = theme.FontSize;
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
                        Color = theme.AccentColor,
                        BlurRadius = 20,
                        ShadowDepth = 0,
                        Opacity = 0.6
                    };
                }
                else
                {
                    this.Effect = null;
                }

                currentTheme = theme;

                // Применяем стили к существующим элементам
                ApplyStylesToExistingElements();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка применения темы: {ex.Message}");
            }
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

        private void ApplyTextTheme(Theme theme)
        {
            // Применяем тему ко всем текстовым элементам
            var textStyle = new Style(typeof(TextBlock));
            textStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(theme.TextColor)));
            textStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            textStyle.Setters.Add(new Setter(TextBlock.FontFamilyProperty, new FontFamily("Segoe UI")));
            this.Resources["ModernTextStyle"] = textStyle;

            // Применяем к конкретным элементам, КРОМЕ текста бейджей
            ApplyTextColorToElement(txtFilter, theme.TextColor);
            ApplyTextColorToElement(txtDetailTitle, theme.TextColor);
            // НЕ применяем к txtDetailYear, txtDetailType, txtDetailRating - они используют фиксированные стили
            ApplyTextColorToElement(txtDetailGenres, theme.TextColor);
            ApplyTextColorToElement(txtDetailDescription, theme.TextColor);

            // Применяем к элементам верхней панели
            if (logoTextBlock != null)
                logoTextBlock.Foreground = new SolidColorBrush(theme.TextColor);
            if (titleTextBlock != null)
                titleTextBlock.Foreground = new SolidColorBrush(theme.TextColor);
            if (searchPlaceholderTextBlock != null)
                searchPlaceholderTextBlock.Foreground = new SolidColorBrush(theme.TextColor);
        }

        private void ApplyTextColorToElement(Control control, Color color)
        {
            if (control != null)
                control.Foreground = new SolidColorBrush(color);
        }

        private void ApplyTextColorToElement(TextBlock textBlock, Color color)
        {
            if (textBlock != null)
                textBlock.Foreground = new SolidColorBrush(color);
        }

        private void ApplyStylesToExistingElements()
        {
            // Применяем стили к уже существующим элементам
            foreach (var item in itemsControlVideos.Items)
            {
                var container = itemsControlVideos.ItemContainerGenerator.ContainerFromItem(item);
                if (container != null)
                {
                    var border = FindVisualChild<Border>(container);
                    if (border != null)
                    {
                        border.Style = this.Resources["ModernCardStyle"] as Style;
                    }
                }
            }

            // Применяем стили к кнопкам управления
            btnToggleTheme.Style = this.Resources["HeaderButtonStyle"] as Style;
            btnCreateTheme.Style = this.Resources["HeaderButtonStyle"] as Style;
            btnSupport.Style = this.Resources["HeaderButtonStyle"] as Style;
            btnProfile.Style = this.Resources["HeaderButtonStyle"] as Style;
            btnMinimize.Style = this.Resources["HeaderButtonStyle"] as Style;
            btnFullscreen.Style = this.Resources["HeaderButtonStyle"] as Style;
            btnClose.Style = this.Resources["HeaderButtonStyle"] as Style;

            // Применяем стиль к ComboBox
            cmbThemes.Style = this.Resources["ModernComboBoxStyle"] as Style;

            // Применяем стиль к кнопке детальной панели
            btnDetailPlay.Style = this.Resources["HeaderButtonStyle"] as Style;
            btnCloseDetail.Style = this.Resources["HeaderButtonStyle"] as Style;

            // Обновляем тень кнопки воспроизведения
            if (btnDetailPlay != null && currentTheme != null)
            {
                var shadowEffect = new DropShadowEffect
                {
                    Color = currentTheme.ButtonForegroundColor,
                    BlurRadius = 12,
                    ShadowDepth = 3,
                    Opacity = 0.6
                };
                btnDetailPlay.Effect = shadowEffect;
            }
        }

        private T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T)
                    return (T)child;
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }

        // Обработчики событий
        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            if (cmbThemes.SelectedItem is Theme selectedTheme)
            {
                var index = themes.IndexOf(selectedTheme);
                var nextIndex = (index + 1) % themes.Count;
                cmbThemes.SelectedIndex = nextIndex;
            }
        }

        private void CreateTheme_Click(object sender, RoutedEventArgs e)
        {
            var editor = new ThemeEditorWindow();
            if (editor.ShowDialog() == true)
            {
                InitializeThemes();
                if (editor.ViewModel?.CurrentTheme != null)
                {
                    cmbThemes.SelectedItem = themes.FirstOrDefault(t => t.Name == editor.ViewModel.CurrentTheme.Name);
                }
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

        private void txtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            CheckForSpells();
            ApplyFilter();
        }

        private void CheckForSpells()
        {
            string searchText = txtFilter.Text.ToLower().Trim();

            // Проверяем интервал между вводами (максимум 2 секунды)
            TimeSpan timeSinceLastSearch = DateTime.Now - lastSearchTime;

            if (timeSinceLastSearch.TotalSeconds > 2)
            {
                lastSearchText = "";
            }

            lastSearchText += searchText;
            lastSearchTime = DateTime.Now;

            // Проверяем заклинания
            if (lastSearchText.Contains("lumen"))
            {
                ProcessSpell("lumen");
            }
            else if (lastSearchText.Contains("avada kedavra"))
            {
                ProcessSpell("avada kedavra");
            }
            else if (lastSearchText.Contains("expecto patronum"))
            {
                ProcessSpell("expecto patronum");
            }
        }

        private void ProcessSpell(string spell)
        {
            // Защита от повторного срабатывания - минимум 3 секунды между заклинаниями
            TimeSpan timeSinceLastSpell = DateTime.Now - lastSpellCastTime;
            if (timeSinceLastSpell.TotalSeconds < 3)
            {
                return;
            }

            lastSpellCastTime = DateTime.Now;

            switch (spell)
            {
                case "lumen":
                    // Смена темы
                    ToggleTheme_Click(null, null);
                    txtFilter.Text = "";
                    lastSearchText = "";
                    ShowSpellMessage("Lumen! Свет сменяет тьму!");
                    break;

                case "avada kedavra":
                    // Закрытие приложения
                    ShowSpellMessage("Avada Kedavra! Приложение будет закрыто!");
                    Task.Delay(1000).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() => Close());
                    });
                    txtFilter.Text = "";
                    lastSearchText = "";
                    break;

                case "expecto patronum":
                    // Открытие специального фильма по ID
                    OpenSpecialVideo(GetHolidayVideoId());
                    txtFilter.Text = "";
                    lastSearchText = "";
                    ShowSpellMessage("Expecto Patronum! Защита от дементоров!");
                    break;
            }
        }

        private void ShowSpellMessage(string message)
        {
            // Создаем всплывающее сообщение о заклинании
            var spellBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(220, 26, 26, 46)),
                BorderBrush = new SolidColorBrush(Colors.Gold),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20, 10, 20, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 100, 0, 0),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Gold,
                    BlurRadius = 20,
                    ShadowDepth = 0,
                    Opacity = 0.8
                }
            };

            var textBlock = new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Colors.Gold),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                FontFamily = new FontFamily("Segoe UI")
            };

            spellBorder.Child = textBlock;

            // Добавляем на основной Grid
            if (mainContentGrid.Children.OfType<Border>().FirstOrDefault(b => b.Name == "spellMessage") == null)
            {
                spellBorder.Name = "spellMessage";
                mainContentGrid.Children.Add(spellBorder);

                // Автоматически скрываем через 3 секунды
                Task.Delay(3000).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        mainContentGrid.Children.Remove(spellBorder);
                    });
                });
            }
        }

        private int GetHolidayVideoId()
        {
            DateTime today = DateTime.Today;

            // рождество
            if ((today.Month == 12 && today.Day >= 25) || (today.Month == 1 && today.Day <= 7))
                return 68; // ID новогоднего фильма

            // 8 марта
            if (today.Month == 3 && today.Day == 8)
                return 70; // ID романтического фильма

            // 23 февраля
            if (today.Month == 2 && today.Day == 23)
                return 72; // ID боевика

            //  14 февраля
            if (today.Month == 2 && today.Day == 14)
                return 69; // ID романтического фильма

            // новый год 
            if (today.Month == 12 && today.Day == 31)
                return 64;
            // новый год 
            if (today.Month == 1 && today.Day == 1)
                return 65;
            // новый год 
            if (today.Month == 1 && today.Day == 2)
                return 66;
            if (today.Month == 1 && today.Day == 3)
                return 67;
            // новый год 
            if (today.Month == 5 && today.Day == 15)
                return 71; //оверлорд
            // новый год 
            if (today.Month == 10 && today.Day == 31)
                return 73;//кошмар перед рождеством
            // новый год 
            if (today.Month == 4 && today.Day == 26)
                return 74;//сталкер
            // новый год 
            if (today.Month == 8 && today.Day == 29)
                return 75;//фалаут
            // новый год 
            if (today.Month == 7 && today.Day == 11)
                return 76;//вилли вонка и шоколадная фабрика
            // новый год 
            if (today.Month == 9 && today.Day == 13)
                return 78;//киберсталкер
            // новый год 
            if (today.Month == 9 && today.Day == 12)
                return 79;//цикада
            // новый год 
            if (today.Month == 10 && today.Day == 24)
                return 80;//мой создатель
            // новый год 
            if (today.Month == 12 && today.Day == 2)
                return 81;//марсианин
            // новый год 
            if (today.Month == 6 && today.Day == 1)
                return 82;//хранители 
            // По умолчанию возвращаем первый фильм
            return 38;
        }

        private void OpenSpecialVideo(int videoId)
        {
            try
            {
                // Ищем видео по ID в базе данных
                var query = @"
                    SELECT 
                        video_id, title, type, 
                        EXTRACT(YEAR FROM release_date) as release_year,
                        rating, 
                        file_path, genres, poster_url, description
                    FROM videos
                    WHERE video_id = @videoId";

                using (var conn = db.GetConnection())
                {
                    var command = new NpgsqlCommand(query, conn);
                    command.Parameters.AddWithValue("@videoId", videoId);

                    var adapter = new NpgsqlDataAdapter(command);
                    var specialTable = new DataTable();
                    adapter.Fill(specialTable);

                    if (specialTable.Rows.Count > 0)
                    {
                        var specialVideo = specialTable.DefaultView[0];
                        SelectVideo((DataRowView)specialVideo);

                        // Автоматически начинаем воспроизведение
                        Task.Delay(500).ContinueWith(_ =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                PlaySelectedVideo();
                            });
                        });
                    }
                    else
                    {
                        ShowSpellMessage("Фильм не найден! Проверьте ID в базе данных.");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowSpellMessage($"Заклинание не сработало: {ex.Message}");
            }
        }

        // Голосовое управление (пасхалка)
        private void InitializeVoiceControl()
        {
            try
            {
                // Используем рефлексию для доступа к System.Speech без прямых ссылок
                var speechAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.FullName.Contains("System.Speech"));

                if (speechAssembly == null)
                {
                    // Пытаемся загрузить System.Speech
                    try
                    {
                        speechAssembly = AppDomain.CurrentDomain.Load("System.Speech");
                    }
                    catch (Exception ex)
                    {
                        ShowSpellMessage("Голосовое управление недоступно." + ex.Message);
                        return;
                    }
                }

                var speechRecognizerType = speechAssembly.GetType("System.Speech.Recognition.SpeechRecognitionEngine");
                var speechSynthesizerType = speechAssembly.GetType("System.Speech.Synthesis.SpeechSynthesizer");

                if (speechRecognizerType == null || speechSynthesizerType == null)
                {
                    ShowSpellMessage("Голосовое управление недоступно.");
                    return;
                }

                // Создаем синтезатор речи
                var speechSynthesizer = Activator.CreateInstance(speechSynthesizerType);
                var setOutputMethod = speechSynthesizerType.GetMethod("SetOutputToDefaultAudioDevice");
                setOutputMethod?.Invoke(speechSynthesizer, null);

                // Создаем распознаватель речи
                var speechRecognizer = Activator.CreateInstance(speechRecognizerType);

                // Создаем грамматику для комманд
                var choicesType = speechAssembly.GetType("System.Speech.Recognition.Choices");
                var choices = Activator.CreateInstance(choicesType, new object[] { new string[] {
                    "люмен", "авада кедавра", "экспекто патронум",
                    "сменить тему", "закрыть приложение", "открыть фильм",
                    "открыть профиль", "минимизировать", "полный экран", "закрыть",
                    "воспроизвести", "следующий", "предыдущий"
                }});

                var grammarBuilderType = speechAssembly.GetType("System.Speech.Recognition.GrammarBuilder");
                var grammarBuilder = Activator.CreateInstance(grammarBuilderType, new object[] { choices });

                var grammarType = speechAssembly.GetType("System.Speech.Recognition.Grammar");
                var grammar = Activator.CreateInstance(grammarType, new object[] { grammarBuilder });

                var loadGrammarMethod = speechRecognizerType.GetMethod("LoadGrammar");
                loadGrammarMethod?.Invoke(speechRecognizer, new object[] { grammar });

                // Подписываемся на события
                var speechRecognizedEvent = speechRecognizerType.GetEvent("SpeechRecognized");
                var eventHandler = new Action<object, EventArgs>(SpeechRecognized);
                var delegateType = typeof(EventHandler<>).MakeGenericType(speechAssembly.GetType("System.Speech.Recognition.SpeechRecognizedEventArgs"));
                var eventHandlerDelegate = Delegate.CreateDelegate(delegateType, this, GetType().GetMethod("SpeechRecognized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));

                speechRecognizedEvent?.AddEventHandler(speechRecognizer, eventHandlerDelegate);

                var setInputMethod = speechRecognizerType.GetMethod("SetInputToDefaultAudioDevice");
                setInputMethod?.Invoke(speechRecognizer, null);

                var recognizeAsyncMethod = speechRecognizerType.GetMethod("RecognizeAsync", new Type[] { speechAssembly.GetType("System.Speech.Recognition.RecognizeMode") });
                var recognizeMode = Enum.Parse(speechAssembly.GetType("System.Speech.Recognition.RecognizeMode"), "Multiple");
                recognizeAsyncMethod?.Invoke(speechRecognizer, new object[] { recognizeMode });

                ShowSpellMessage("Голосовое управление активировано! Произносите команды...");

                // Сохраняем ссылки для последующего освобождения
                this.Resources["SpeechRecognizer"] = speechRecognizer;
                this.Resources["SpeechSynthesizer"] = speechSynthesizer;

            }
            catch (Exception ex)
            {
                ShowSpellMessage("Голосовое управление недоступно: " + ex.Message);
            }
        }

        // Обработчик распознанных команд (приватный метод)
        private void SpeechRecognized(object sender, EventArgs e)
        {
            // Используем рефлексию для доступа к свойствам события
            var resultProperty = e.GetType().GetProperty("Result");
            if (resultProperty == null) return;

            var result = resultProperty.GetValue(e);
            var textProperty = result?.GetType().GetProperty("Text");
            var confidenceProperty = result?.GetType().GetProperty("Confidence");

            if (textProperty == null || confidenceProperty == null) return;

            string command = (textProperty.GetValue(result) as string)?.ToLower() ?? "";
            double confidence = (double)(confidenceProperty.GetValue(result) ?? 0.0);

            if (confidence < 0.7) return;

            Dispatcher.Invoke(() =>
            {
                ProcessVoiceCommand(command);
            });
        }

        private void ProcessVoiceCommand(string command)
        {
            ShowVoiceMessage($"Распознано: {command}");

            switch (command)
            {
                case "люмен":
                case "сменить тему":
                    ProcessSpell("lumen");
                    break;

                case "авада кедавра":
                case "закрыть приложение":
                    ProcessSpell("avada kedavra");
                    break;

                case "экспекто патронум":
                case "открыть фильм":
                    ProcessSpell("expecto patronum");
                    break;

                case "открыть профиль":
                    btnProfile_Click(null, null);
                    break;

                case "минимизировать":
                    Minimize_Click(null, null);
                    break;

                case "полный экран":
                    Fullscreen_Click(null, null);
                    break;

                case "закрыть":
                    Close_Click(null, null);
                    break;

                case "воспроизвести":
                    if (selectedVideo != null)
                        PlaySelectedVideo();
                    break;
            }
        }

        private void ShowVoiceMessage(string message)
        {
            var voiceBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(220, 41, 128, 185)),
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15, 8, 15, 8),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 100),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Cyan,
                    BlurRadius = 15,
                    ShadowDepth = 0,
                    Opacity = 0.7
                }
            };

            var textBlock = new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center
            };

            voiceBorder.Child = textBlock;

            // Удаляем предыдущее сообщение
            var existingMessage = mainContentGrid.Children.OfType<Border>().FirstOrDefault(b => b.Name == "voiceMessage");
            if (existingMessage != null)
            {
                mainContentGrid.Children.Remove(existingMessage);
            }

            voiceBorder.Name = "voiceMessage";
            mainContentGrid.Children.Add(voiceBorder);

            // Автоматически скрываем через 3 секунды
            Task.Delay(3000).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    mainContentGrid.Children.Remove(voiceBorder);
                });
            });
        }

        private void ApplyFilter()
        {
            if (videoTable == null) return;

            string filter = txtFilter.Text.Trim();
            if (string.IsNullOrEmpty(filter))
            {
                itemsControlVideos.ItemsSource = videoTable.DefaultView;
                return;
            }

            try
            {
                string safeFilter = filter.Replace("'", "''");
                var view = new DataView(videoTable);
                view.RowFilter = $"title LIKE '%{safeFilter}%' OR genres LIKE '%{safeFilter}%'";
                itemsControlVideos.ItemsSource = view;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка фильтрации: {ex.Message}");
            }
        }

        // ИСПРАВЛЕННАЯ АНИМАЦИЯ С ПЕРЕКЛЮЧЕНИЕМ КАРТОЧЕК
        private async void SelectVideo(DataRowView row)
        {
            // Если анимация уже выполняется, игнорируем новый вызов
            if (isAnimationRunning) return;

            // Если выбрана та же карточка, просто закрываем панель
            if (isDetailVisible && selectedVideo == row)
            {
                await HideDetailPanelAsync();
                selectedVideo = null;
                return;
            }

            // Сохраняем предыдущее выбранное видео
            var previousVideo = selectedVideo;
            selectedVideo = row;

            // Если панель уже открыта, сначала скрываем ее, затем показываем новую
            if (isDetailVisible && previousVideo != null)
            {
                await HideDetailPanelAsync();
                await Task.Delay(100); // Небольшая задержка для плавности
            }

            // Обновляем информацию и показываем панель
            UpdateDetailInfo(row);
            await ShowDetailPanelAsync();
        }

        private async Task ShowDetailPanelAsync()
        {
            if (isAnimationRunning) return;
            isAnimationRunning = true;

            try
            {
                // Устанавливаем начальное состояние для анимации
                detailBorder.Opacity = 0;
                detailScaleTransform.ScaleX = 0.7;
                detailScaleTransform.ScaleY = 0.7;
                detailTranslateTransform.Y = 150;
                detailBorder.Visibility = Visibility.Visible;

                // Ждем обновления layout
                await Task.Delay(10);

                // Создаем сложную анимацию с несколькими эффектами
                var opacityAnimation = new DoubleAnimation
                {
                    To = 1,
                    Duration = TimeSpan.FromSeconds(0.4),
                    EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
                };

                var scaleXAnimation = new DoubleAnimation
                {
                    To = 1,
                    Duration = TimeSpan.FromSeconds(0.5),
                    EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.8 }
                };

                var scaleYAnimation = new DoubleAnimation
                {
                    To = 1,
                    Duration = TimeSpan.FromSeconds(0.5),
                    EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.8 }
                };

                var translateAnimation = new DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromSeconds(0.4),
                    EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
                };

                // Добавляем эффект свечения с анимацией
                var glowEffect = new DropShadowEffect
                {
                    Color = Color.FromArgb(255, 51, 255, 204),
                    BlurRadius = 0,
                    ShadowDepth = 0,
                    Opacity = 0
                };

                detailBorder.Effect = glowEffect;

                var glowBlurAnimation = new DoubleAnimation
                {
                    To = 30,
                    Duration = TimeSpan.FromSeconds(0.4),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                var glowOpacityAnimation = new DoubleAnimation
                {
                    To = 0.5,
                    Duration = TimeSpan.FromSeconds(0.3),
                    AutoReverse = true,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                // Запускаем анимации
                detailBorder.BeginAnimation(OpacityProperty, opacityAnimation);
                detailScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnimation);
                detailScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnimation);
                detailTranslateTransform.BeginAnimation(TranslateTransform.YProperty, translateAnimation);

                glowEffect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, glowBlurAnimation);
                glowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, glowOpacityAnimation);

                // Добавляем анимацию для содержимого панели
                await Task.Delay(200);
                await AnimateContentAppearance();

                isDetailVisible = true;
            }
            finally
            {
                isAnimationRunning = false;
            }
        }

        private async Task AnimateContentAppearance()
        {
            // Анимация для элементов внутри детальной панели
            var elements = new List<FrameworkElement>
            {
                imgDetailPoster,
                txtDetailTitle,
                txtDetailYear,
                txtDetailType,
                txtDetailRating,
                txtDetailGenres,
                txtDetailDescription,
                btnDetailPlay
            };

            foreach (var element in elements.Where(e => e != null))
            {
                element.Opacity = 0;
                var transform = new TranslateTransform { Y = 20 };
                element.RenderTransform = transform;

                var opacityAnim = new DoubleAnimation
                {
                    To = 1,
                    Duration = TimeSpan.FromSeconds(0.3),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                var translateAnim = new DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromSeconds(0.3),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                element.BeginAnimation(OpacityProperty, opacityAnim);
                transform.BeginAnimation(TranslateTransform.YProperty, translateAnim);

                await Task.Delay(50); // Уменьшена задержка для более быстрого появления
            }
        }

        private async Task HideDetailPanelAsync()
        {
            if (isAnimationRunning) return;
            isAnimationRunning = true;

            try
            {
                // Анимация для скрытия элементов содержимого
                var elements = new List<FrameworkElement>
                {
                    imgDetailPoster,
                    txtDetailTitle,
                    txtDetailYear,
                    txtDetailType,
                    txtDetailRating,
                    txtDetailGenres,
                    txtDetailDescription,
                    btnDetailPlay
                };

                // Сначала скрываем содержимое
                foreach (var element in elements.Where(e => e != null))
                {
                    var opacityAnim = new DoubleAnimation
                    {
                        To = 0,
                        Duration = TimeSpan.FromSeconds(0.15),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                    };

                    var translateAnim = new DoubleAnimation
                    {
                        To = -20,
                        Duration = TimeSpan.FromSeconds(0.15),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                    };

                    element.BeginAnimation(OpacityProperty, opacityAnim);
                    if (element.RenderTransform is TranslateTransform transform)
                    {
                        transform.BeginAnimation(TranslateTransform.YProperty, translateAnim);
                    }
                }

                await Task.Delay(150);

                // Создаем анимации для скрытия панели
                var opacityAnimation = new DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromSeconds(0.4),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                var scaleXAnimation = new DoubleAnimation
                {
                    To = 0.7,
                    Duration = TimeSpan.FromSeconds(0.4),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                var scaleYAnimation = new DoubleAnimation
                {
                    To = 0.7,
                    Duration = TimeSpan.FromSeconds(0.4),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                var translateAnimation = new DoubleAnimation
                {
                    To = 100,
                    Duration = TimeSpan.FromSeconds(0.4),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                // Запускаем анимации
                detailBorder.BeginAnimation(OpacityProperty, opacityAnimation);
                detailScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnimation);
                detailScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnimation);
                detailTranslateTransform.BeginAnimation(TranslateTransform.YProperty, translateAnimation);

                // Ждем завершения анимации
                await Task.Delay(400);
                detailBorder.Visibility = Visibility.Collapsed;
                isDetailVisible = false;

                // Сбрасываем прозрачность элементов
                foreach (var element in elements.Where(e => e != null))
                {
                    element.Opacity = 1;
                    element.RenderTransform = null;
                }

                // Сбрасываем эффект
                detailBorder.Effect = null;
            }
            finally
            {
                isAnimationRunning = false;
            }
        }

        private async void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is DataRowView row)
            {
                await SelectVideoAsync(row);
            }
            e.Handled = true;
        }

        // Новая асинхронная версия для обработки кликов
        private async Task SelectVideoAsync(DataRowView row)
        {
            // Если анимация уже выполняется, игнорируем новый вызов
            if (isAnimationRunning) return;

            // Если выбрана та же карточка и панель открыта, закрываем ее
            if (isDetailVisible && selectedVideo == row)
            {
                await HideDetailPanelAsync();
                selectedVideo = null;
                return;
            }

            // Сохраняем предыдущее выбранное видео
            var previousVideo = selectedVideo;
            selectedVideo = row;

            // Если панель уже открыта для другого видео, сначала скрываем ее
            if (isDetailVisible && previousVideo != null && previousVideo != row)
            {
                await HideDetailPanelAsync();
                await Task.Delay(80); // Короткая задержка для плавности
            }

            // Обновляем информацию и показываем панель
            UpdateDetailInfo(row);
            await ShowDetailPanelAsync();
        }

        private void HeaderBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private async void MainContentGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsClickOnCard(e) && !IsClickOnDetailPanel(e) && isDetailVisible)
            {
                await HideDetailPanelAsync();
                selectedVideo = null;
            }
        }

        private async void ScrollViewer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsClickOnCard(e) && isDetailVisible)
            {
                await HideDetailPanelAsync();
                selectedVideo = null;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void DetailBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
            e.Handled = true;
        }

        private bool IsClickOnCard(MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;

            while (source != null && source != this)
            {
                if (source is Border border)
                {
                    if (border.DataContext is DataRowView ||
                        (border.Parent is ContentPresenter && border.DataContext != null))
                    {
                        return true;
                    }
                }
                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        private bool IsClickOnDetailPanel(MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;

            while (source != null && source != detailBorder)
            {
                source = VisualTreeHelper.GetParent(source);
            }

            return source == detailBorder;
        }

        private void UpdateDetailInfo(DataRowView row)
        {
            if (row == null) return;

            try
            {
                txtDetailTitle.Text = row["title"]?.ToString() ?? "Неизвестно";
                txtDetailYear.Text = row["release_year"]?.ToString() ?? "Неизвестно";
                txtDetailType.Text = row["type"]?.ToString() ?? "Неизвестно";
                txtDetailGenres.Text = row["genres"]?.ToString() ?? "Неизвестно";
                txtDetailDescription.Text = row["description"]?.ToString() ?? "Описание отсутствует";

                if (row["rating"] != DBNull.Value && double.TryParse(row["rating"].ToString(), out double rating))
                {
                    txtDetailRating.Text = $"{rating:0.0}";
                }
                else
                {
                    txtDetailRating.Text = "Н/Д";
                }

                string posterUrl = row["poster_url"]?.ToString();
                if (!string.IsNullOrEmpty(posterUrl))
                {
                    try
                    {
                        imgDetailPoster.Source = new BitmapImage(new Uri(posterUrl, UriKind.RelativeOrAbsolute));
                    }
                    catch
                    {
                        imgDetailPoster.Source = null;
                    }
                }
                else
                {
                    imgDetailPoster.Source = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления информации: {ex.Message}");
            }
        }

        private void PlaySelectedVideo()
        {
            if (selectedVideo == null)
            {
                MessageBox.Show("Выберите видео для воспроизведения");
                return;
            }

            string url = selectedVideo["file_path"]?.ToString();
            if (!string.IsNullOrEmpty(url))
            {
                try
                {
                    var webView = new WebViewWindow(url);
                    webView.Show();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка воспроизведения: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("Файл для воспроизведения не найден");
            }
        }

        private void btnPlayInApp_Click(object sender, RoutedEventArgs e)
        {
            PlaySelectedVideo();
        }

        // Новая кнопка закрытия детальной панели
        private async void btnCloseDetail_Click(object sender, RoutedEventArgs e)
        {
            await HideDetailPanelAsync();
            selectedVideo = null;
        }

        // Управление окном
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void Fullscreen_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = previousWindowState;
                WindowStyle = WindowStyle.None;
                btnFullscreen.Content = "⛶";
            }
            else
            {
                previousWindowState = WindowState;
                WindowState = WindowState.Maximized;
                WindowStyle = WindowStyle.None;
                btnFullscreen.Content = "⛷";
            }
        }

        private void btnMoodMovie_Click(object sender, RoutedEventArgs e)
        {
            var moodWindow = new MoodMovieWindow(currentTheme);
            moodWindow.ShowDialog();
        }

        private void btnSupport_Click(object sender, RoutedEventArgs e)
        {
            var supportWindow = new SupportRequestWindow();
            if (supportWindow.ShowDialog() == true)
            {
                string requestText = supportWindow.RequestText;
                string category = supportWindow.SelectedCategory;

                if (db.SubmitSupportTicket(currentUserId, requestText, category))
                {
                    MessageBox.Show("Запрос успешно отправлен!");
                }
            }
        }

        private void btnProfile_Click(object sender, RoutedEventArgs e)
        {
            var profileWindow = new UserEditWindow(currentTheme, currentUserId.ToString());
            profileWindow.ShowDialog();
        }
    }
}