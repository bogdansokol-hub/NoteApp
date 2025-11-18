using System;
using System.IO;
using Microsoft.Maui.Storage;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace NoteApp
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        // Browse -> wybór pliku i wpisanie ścieżki do Entry
        private async void OnBrowseClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync();
                if (result != null)
                {
                    PathEntry.Text = result.FullPath ?? result.FileName;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Błąd", ex.Message, "OK");
            }
        }

        // Menu / przycisk: Odczytaj
        private async void OnReadClicked(object sender, EventArgs e)
        {
            string path = PathEntry.Text;
            if (string.IsNullOrWhiteSpace(path))
            {
                await DisplayAlert("Uwaga", "Wprowadź ścieżkę do pliku lub użyj Przeglądaj.", "OK");
                return;
            }

            try
            {
                string text = await ReadAllTextFromFileAsync(path);
                ContentEditor.Text = text;
                await DisplayAlert("Gotowe", "Plik został wczytany.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Błąd podczas odczytu", ex.Message, "OK");
            }
        }

        // Menu / przycisk: Zapisz
        private async void OnSaveClicked(object sender, EventArgs e)
        {
            string path = PathEntry.Text;
            if (string.IsNullOrWhiteSpace(path))
            {
                // Prosty dialog zapisu: zapisz z domyślną nazwą w katalogu aplikacji (alternatywnie można użyć FilePicker do Save)
                string defaultName = Path.Combine(FileSystem.Current.AppDataDirectory, "note.txt");
                bool save = await DisplayAlert("Brak ścieżki", $"Brak ścieżki. Zapisz do domyślnej lokalizacji?\n{defaultName}", "Tak", "Nie");
                if (!save) return;
                path = defaultName;
                PathEntry.Text = path;
            }

            try
            {
                await WriteAllTextToFileAsync(path, ContentEditor.Text ?? string.Empty);
                await DisplayAlert("Gotowe", "Plik został zapisany.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Błąd podczas zapisu", ex.Message, "OK");
            }
        }

        private void OnExitClicked(object sender, EventArgs e)
        {
            // Zamykamy aplikację — platformowo zależne; tutaj próbujemy zakończyć proces.
            System.Diagnostics.Process.GetCurrentProcess().CloseMainWindow();
        }

        // ---- Logika odczytu/zapisu używająca FileStream + dekoratory ----

        private async Task<string> ReadAllTextFromFileAsync(string path)
        {
            // DRY: konfiguracja dekoratorów i otwarcie strumienia jest w StreamFactory.OpenReadDecorated
            using Stream baseStream = StreamFactory.OpenReadDecorated(path, CompressCheck.IsChecked, EncryptCheck.IsChecked, PasswordEntry.Text);
            using var sr = new StreamReader(baseStream);
            return await sr.ReadToEndAsync();
        }

        private async Task WriteAllTextToFileAsync(string path, string content)
        {
            // Upewnij się, że katalog istnieje
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using Stream baseStream = StreamFactory.OpenWriteDecorated(path, CompressCheck.IsChecked, EncryptCheck.IsChecked, PasswordEntry.Text);
            using var sw = new StreamWriter(baseStream);
            await sw.WriteAsync(content);
            await sw.FlushAsync();
            // ważne: przy użyciu 'using' stream zostanie zamknięty i jeśli jest CryptoStream/GZipStream wykona finalizację
        }
    }
}
