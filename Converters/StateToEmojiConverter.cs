using System.Windows.Data;
using System.Globalization;
using System.Windows;

namespace SLSKDONET.Converters
{
    /// <summary>
    /// Converts OrchestratedQueryProgress state to animated emoji string.
    /// Used for visual feedback during orchestration (searching, ranking, matched).
    /// </summary>
    public class StateToEmojiConverter : IValueConverter
    {
        private static readonly string[] SearchingEmojis = { "🔍", "🔎" };
        private static readonly string[] RankingEmojis = { "⭐", "✨" };
        private static int _animationFrame = 0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string state)
            {
                _animationFrame = (_animationFrame + 1) % 2;
                
                return state switch
                {
                    "Queued" => "⏳",
                    "Searching" => SearchingEmojis[_animationFrame],
                    "Ranking" => RankingEmojis[_animationFrame],
                    "Matched" => "✅",
                    "Failed" => "❌",
                    _ => "◌"
                };
            }
            return "◌";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
