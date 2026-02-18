using CommunityToolkit.Mvvm.ComponentModel;

namespace Sample.ViewModels
{
    public partial class VisualStatesPageViewModel : ObservableObject
    {
        private static readonly string[] WordBank =
        {
            "lorem", "ipsum", "dolor", "sit", "amet",
            "consectetur", "adipiscing", "elit", "sed",
            "eiusmod", "tempor", "incididunt", "labore",
            "dolore", "magna", "aliqua", "enim",
            "minim", "veniam", "quis", "nostrud",
            "exercitation", "ullamco", "laboris", "nisi",
            "aliquip", "commodo", "consequat",
            "duis", "aute", "irure", "dolor",
            "reprehenderit", "voluptate", "velit", "esse",
            "cillum", "dolore", "fugiat", "nulla",
            "pariatur", "excepteur", "sint", "occaecat",
            "cupidatat", "non", "proident", "sunt",
            "culpa", "qui", "officia", "deserunt", "mollit",
            "anim", "est", "laborum"
        };

        public IList<VisualStateItem> Items { get; } = new List<VisualStateItem>();


        public VisualStatesPageViewModel()
        {
            for (int i = 0; i < 30; i++)
            {
                var item = new VisualStateItem
                {
                    Title = GeneratePhrases(1, 2),
                    SubTitle = GeneratePhrases(4, 5)
                };
                Items.Add(item);
            }
        }

        static string GeneratePhrases(int minWords = 4, int maxWords = 20)
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
