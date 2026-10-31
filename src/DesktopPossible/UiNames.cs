using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Desktop_Frames
{
    /// <summary>
    /// Turkish display names for values that are persisted or compared in English
    /// (colours, launch effects, sizes, log levels/categories, text-frame modes, auto-sort
    /// categories, the "Default" option/profile). Only what the user SEES changes — the stored
    /// value stays English, so settings, frames.json and every comparison keep working.
    /// </summary>
    public static class UiNames
    {
        private static readonly Dictionary<string, string> _names = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // "use default" combo option, and the built-in profile name
            ["Default"] = "Varsayılan",

            // Colours
            ["Gray"] = "Gri", ["Black"] = "Siyah", ["White"] = "Beyaz", ["Beige"] = "Bej",
            ["Green"] = "Yeşil", ["Teal"] = "Camgöbeği", ["Purple"] = "Mor", ["Fuchsia"] = "Fuşya",
            ["Yellow"] = "Sarı", ["Orange"] = "Turuncu", ["Red"] = "Kırmızı", ["Blue"] = "Mavi",
            ["Bismark"] = "Bismark mavisi", ["Transparent"] = "Şeffaf",

            // Launch effects
            ["Zoom"] = "Yakınlaştır", ["Bounce"] = "Zıpla", ["FadeOut"] = "Solarak kaybol",
            ["SlideUp"] = "Yukarı kay", ["Rotate"] = "Döndür", ["Agitate"] = "Salla",
            ["GrowAndFly"] = "Büyü ve uç", ["Pulse"] = "Nabız", ["Elastic"] = "Esnek",
            ["Flip3D"] = "3B çevir", ["Spiral"] = "Sarmal", ["Shockwave"] = "Şok dalgası",
            ["Supernova"] = "Süpernova", ["Teleport"] = "Işınlan",

            // Sizes
            ["Tiny"] = "Çok küçük", ["Small"] = "Küçük", ["Medium"] = "Orta", ["Large"] = "Büyük", ["Huge"] = "Çok büyük",

            // Log levels and categories (LogManager.LogLevel / LogCategory)
            ["Debug"] = "Hata ayıklama", ["Info"] = "Bilgi", ["Warn"] = "Uyarı", ["Error"] = "Hata",
            ["General"] = "Genel", ["FrameCreation"] = "Kutu oluşturma", ["FrameUpdate"] = "Kutu güncelleme",
            ["UI"] = "Arayüz", ["IconHandling"] = "Simge işleme", ["ImportExport"] = "İçe/dışa aktarma",
            ["Settings"] = "Ayarlar", ["BackgroundValidation"] = "Arka plan doğrulama",

            // Text frame draw modes / alignments (TextFramemanager.DrawModes / Alignments)
            ["Shadow"] = "Gölge", ["Glow"] = "Parıltı", ["Outline"] = "Dış çizgi",
            ["Emboss"] = "Kabartma", ["Engrave"] = "Oyma",
            ["Left"] = "Sol", ["Center"] = "Orta", ["Right"] = "Sağ",

            // Auto-sort categories (AppCategorizer)
            [AppCategorizer.Productivity] = "Verimlilik", [AppCategorizer.Utilities] = "Araçlar",
            [AppCategorizer.Games] = "Oyunlar", [AppCategorizer.DeveloperTools] = "Geliştirici araçları",
            [AppCategorizer.SecurityApps] = "Güvenlik uygulamaları", [AppCategorizer.Media] = "Medya",
            [AppCategorizer.Documents] = "Belgeler", [AppCategorizer.Images] = "Resimler", [AppCategorizer.Other] = "Diğer",
        };

        /// <summary>Turkish text for a stored value, or the value unchanged when unknown.</summary>
        public static string Display(string? value) =>
            value != null && _names.TryGetValue(value, out var tr) ? tr : value ?? "";

        /// <summary>Profile names: only the built-in "Default" profile is translated.</summary>
        public static string Profile(string? name) =>
            string.Equals(name, "Default", StringComparison.OrdinalIgnoreCase) ? _names["Default"] : name ?? "";

        private sealed class DisplayConverter : IValueConverter
        {
            public bool Profiles;
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
                Profiles ? Profile(value?.ToString()) : Display(value?.ToString());
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
        }

        private static readonly DataTemplate _template = BuildTemplate(false);
        private static readonly DataTemplate _profileTemplate = BuildTemplate(true);

        private static DataTemplate BuildTemplate(bool profiles)
        {
            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetBinding(TextBlock.TextProperty, new Binding { Converter = new DisplayConverter { Profiles = profiles } });
            var t = new DataTemplate { VisualTree = text };
            t.Seal();
            return t;
        }

        /// <summary>
        /// Shows a combo of English string items in Turkish. Items / SelectedItem stay the
        /// English values, so the code that saves or compares them is untouched.
        /// </summary>
        public static void Translate(ComboBox comboBox) => comboBox.ItemTemplate = _template;

        /// <summary>Same as <see cref="Translate"/> for a combo whose items are profile names.</summary>
        public static void TranslateProfiles(ComboBox comboBox) => comboBox.ItemTemplate = _profileTemplate;
    }
}
