using CommunityToolkit.Mvvm.ComponentModel;

namespace WheelPicker.Maui.Sample.ViewModels
{
    public partial class VisualStatesPageViewModel : ObservableObject
    {
        private static readonly string[] WordBank =
        {
            "maui", "wheel", "picker", "smooth", "gesture", "haptic", "gradient", "layout",
            "async", "binding", "template", "scroll", "cache", "vector", "shadow", "native",
            "control", "animate", "select", "update", "render", "optimize"
        };

        public IList<VisualStateItem> Items { get; } = new List<VisualStateItem>();


        public VisualStatesPageViewModel()
        {
            for (int i = 0; i < 30; i++)
            {
                var item = new VisualStateItem
                {
                    Title = GeneratePhrases(1, 2),
                    SubTitle = GeneratePhrases(5, 10)
                };
                Items.Add(item);
            }
        }

        static string GeneratePhrases(int minWords = 4, int maxWords = 10)
        {
            if (minWords <= 0 || maxWords < minWords) throw new ArgumentOutOfRangeException();

            var rng = Random.Shared;

            int words = rng.Next(minWords, maxWords + 1);

            var parts = new List<string>(words);
            for (int w = 0; w < words; w++)
                parts.Add(WordBank[rng.Next(WordBank.Length)]);

            parts[0] = char.ToUpperInvariant(parts[0][0]) + parts[0][1..];
            return string.Join(' ', parts);
        }
    }

    public class VisualStateItem
    {
        public string Title { get; set; } = string.Empty;
        public string SubTitle { get; set; } = string.Empty;
    }
}
