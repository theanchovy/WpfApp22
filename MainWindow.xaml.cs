using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfApp22
{
    public partial class MainWindow : Window
    {
        private bool _isTitlePlaceholder = true;
        private bool _isDescriptionPlaceholder = true;
        private readonly ApiService _apiService = new();
        private Event? _selectedEvent; // Для редактирования

        public MainWindow()
        {
            InitializeComponent();

            TitleTextBox.GotFocus += (s, e) =>
            {
                if (_isTitlePlaceholder)
                {
                    TitleTextBox.Text = string.Empty;
                    TitleTextBox.Foreground = Brushes.Black;
                    _isTitlePlaceholder = false;
                }
            };

            TitleTextBox.LostFocus += (s, e) => UpdatePlaceholderVisibility();

            DescriptionTextBox.GotFocus += (s, e) =>
            {
                if (_isDescriptionPlaceholder)
                {
                    DescriptionTextBox.Text = string.Empty;
                    DescriptionTextBox.Foreground = Brushes.Black;
                    _isDescriptionPlaceholder = false;
                }
            };

            DescriptionTextBox.LostFocus += (s, e) => UpdateDescriptionPlaceholderVisibility();

            // Инициализация placeholder’ов
            UpdatePlaceholderVisibility();
            UpdateDescriptionPlaceholderVisibility();
        }

        // Асинхронный метод загрузки данных
        private async Task LoadUpcomingEventsAsync()
        {
            try
            {
                var events = await _apiService.GetUpcomingEventsAsync();
                EventsList.ItemsSource = events;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}");
            }
        }

        // Обработчик кнопки «Загрузить ближайшие события»
        private async void LoadUpcomingEvents_Click(object sender, RoutedEventArgs e)
        {
            await LoadUpcomingEventsAsync();
        }

        // Кнопка «Добавить событие»
        private void AddEvent_Click(object sender, RoutedEventArgs e)
        {
            _selectedEvent = null;
            ShowEventDialog();
        }

        // Кнопка «Редактировать»
        private void EditEvent_Click(object sender, RoutedEventArgs e)
        {
            if (EventsList.SelectedItem is Event selectedEvent)
            {
                _selectedEvent = selectedEvent;
                LoadEventToDialog(selectedEvent);
                ShowEventDialog();
            }
            else
            {
                MessageBox.Show("Выберите событие для редактирования");
            }
        }

        // Кнопка «Удалить»
        private async void DeleteEvent_Click(object sender, RoutedEventArgs e)
        {
            if (EventsList.SelectedItem is Event eventToDelete)
            {
                if (MessageBox.Show($"Удалить событие '{eventToDelete.Title}'?",
                    "Подтверждение удаления",
            MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _apiService.DeleteEventAsync(eventToDelete.Id);
                        await LoadUpcomingEventsAsync(); // Перезагрузка списка
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка удаления: {ex.Message}");
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите событие для удаления");
            }
        }

        // Показать диалоговое окно
        private void ShowEventDialog()
        {
            EventDialog.Visibility = Visibility.Visible;
            DialogTitle.Text = "Добавление события";

            // Очистка и инициализация полей
            TitleTextBox.Text = string.Empty;
            DatePicker.SelectedDate = DateTime.Now; // Устанавливаем текущую дату по умолчанию
            IsHolidayCheckBox.IsChecked = false;
            DescriptionTextBox.Text = string.Empty;

            // Обновляем видимость placeholder'ов после очистки полей
            _isTitlePlaceholder = true;
            _isDescriptionPlaceholder = true;
            UpdatePlaceholderVisibility();
            UpdateDescriptionPlaceholderVisibility();
        }

        // Скрыть диалоговое окно
        private void CancelDialog_Click(object sender, RoutedEventArgs e)
        {
            HideEventDialog();
        }

        // Загрузить данные события в диалоговое окно (для редактирования)
        private void LoadEventToDialog(Event eventItem)
        {
            DialogTitle.Text = "Редактирование события";
            TitleTextBox.Text = eventItem.Title;
            DatePicker.SelectedDate = eventItem.EventDate; // Программно устанавливаем дату из события
            IsHolidayCheckBox.IsChecked = eventItem.IsHoliday;
            DescriptionTextBox.Text = eventItem.Description ?? string.Empty;

            // Обновляем видимость placeholder'ов при загрузке данных для редактирования
            if (!string.IsNullOrWhiteSpace(eventItem.Title))
            {
                _isTitlePlaceholder = false;
            }
            else
            {
                _isTitlePlaceholder = true;
            }

            if (!string.IsNullOrWhiteSpace(eventItem.Description))
            {
                _isDescriptionPlaceholder = false;
            }
            else
            {
                _isDescriptionPlaceholder = true;
            }
            UpdatePlaceholderVisibility();
            UpdateDescriptionPlaceholderVisibility();
        }

        // Сохранить событие (добавление/редактирование)
        private async void SaveEvent_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
            {
                MessageBox.Show("Введите название события");
                return;
            }

            if (!DatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("Выберите дату события");
                return;
            }

            var newEvent = new Event
            {
                Title = TitleTextBox.Text,
                EventDate = DatePicker.SelectedDate.Value,
                IsHoliday = IsHolidayCheckBox.IsChecked ?? false,
                Description = DescriptionTextBox.Text
            };

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                if (_selectedEvent != null)
                {
                    newEvent.Id = _selectedEvent.Id;
                    await _apiService.UpdateEventAsync(newEvent);
                }
                else
                {
                    await _apiService.AddEventAsync(newEvent);
                }

                HideEventDialog();
                await LoadUpcomingEventsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}");
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void HideEventDialog()
        {
            EventDialog.Visibility = Visibility.Collapsed;

            // Очистка полей
            TitleTextBox.Text = string.Empty;
            DatePicker.SelectedDate = null;
            IsHolidayCheckBox.IsChecked = false;
            DescriptionTextBox.Text = string.Empty;

            // Обновляем видимость placeholder'ов
            UpdatePlaceholderVisibility();
            UpdateDescriptionPlaceholderVisibility();

            _selectedEvent = null;
        }

        // Обработчики событий для поля «Название»
        private void TitleTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdatePlaceholderVisibility();
        private void TitleTextBox_GotFocus(object sender, RoutedEventArgs e) => UpdatePlaceholderVisibility();
        private void TitleTextBox_LostFocus(object sender, RoutedEventArgs e) => UpdatePlaceholderVisibility();

        // Обработчики событий для поля «Описание»
        private void DescriptionTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateDescriptionPlaceholderVisibility();
        private void DescriptionTextBox_GotFocus(object sender, RoutedEventArgs e) => UpdateDescriptionPlaceholderVisibility();
        private void DescriptionTextBox_LostFocus(object sender, RoutedEventArgs e) => UpdateDescriptionPlaceholderVisibility();

        // Метод обновления видимости placeholder'а для «Названия события»
        private void UpdatePlaceholderVisibility()
        {
            if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
            {
                if (!TitleTextBox.IsFocused && _isTitlePlaceholder)
                    return; // Уже в режиме placeholder’а

                TitleTextBox.Text = "Название события";
                TitleTextBox.Foreground = Brushes.Gray;
                _isTitlePlaceholder = true;
            }
            else
            {
                if (TitleTextBox.IsFocused || !_isTitlePlaceholder)
                    return; // Уже в режиме ввода

                TitleTextBox.Foreground = Brushes.Black;
                _isTitlePlaceholder = false;
            }
        }

        // Метод обновления видимости placeholder'а для «Описания»
        private void UpdateDescriptionPlaceholderVisibility()
        {
            if (string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
            {
                if (!DescriptionTextBox.IsFocused && _isDescriptionPlaceholder)
                    return;

                DescriptionTextBox.Text = "Введите описание события...";
                DescriptionTextBox.Foreground = Brushes.LightGray;
                _isDescriptionPlaceholder = true;
            }
            else
            {
                if (DescriptionTextBox.IsFocused || !_isDescriptionPlaceholder)
                    return;

                DescriptionTextBox.Foreground = Brushes.Black;
                _isDescriptionPlaceholder = false;
            }
        }
    }
}