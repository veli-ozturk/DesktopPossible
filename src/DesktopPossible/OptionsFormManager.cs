// Inherited upstream code predates nullable reference types: nullable WARNINGS are off for this
// file until it is annotated (annotations remain valid). New files are fully nullable-clean.
#nullable disable warnings

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace Desktop_Frames
{
    public static class OptionsFormManager
    {
        private static int _lastSelectedTabIndex = 0;
        private static TabControl _tabControl;
        private static Window _optionsWindow;
        private static Color _userAccentColor;

        /// <summary>True while the Options dialog is open. Virtual-desktop profile switching
        /// is suspended while set: the dialog edits the CURRENT profile, and a switch under
        /// it would make Save push the old profile's snapshot into the new profile's options.</summary>
        public static bool IsOpen { get; private set; }

        // Colors for tabs
        private static readonly Color ColorStyle = Color.FromRgb(128, 0, 128); // Purple
        private static readonly Color ColorTools = Color.FromRgb(34, 139, 34); // Green
        private static readonly Color ColorLookDeeper = Color.FromRgb(220, 53, 69); // Red
        private static readonly Color ColorProfiles = Color.FromRgb(255, 20, 147); // Deep Pink
        private static readonly Color ColorHotkeys = Color.FromRgb(139, 69, 19); // SaddleBrown
        private static readonly Color ColorSmartDesktop = Color.FromRgb(41, 74, 122); // Semi Dark Blue
        private static readonly Color ColorTextFrames = Color.FromRgb(0, 131, 143); // Teal

        public static void ShowOptionsForm()
        {
            try
            {
                _userAccentColor = Utility.GetColorFromName(SettingsManager.SelectedColor);

                _optionsWindow = new Window
                {
                    Title = "DesktopPossible Seçenekleri",
                    Width = 800,
                    Height = 850,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStyle = WindowStyle.None,
                    Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                    AllowsTransparency = true
                };

                if (DialogIconCache.AppIcon != null) _optionsWindow.Icon = DialogIconCache.AppIcon;

                Border mainBorder = new Border
                {
                    Background = Brushes.White,
                    Margin = new Thickness(8),
                    Effect = new DropShadowEffect { Color = Colors.Black, Direction = 270, ShadowDepth = 2, BlurRadius = 10, Opacity = 0.2 }
                };

                Grid mainGrid = new Grid();
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) }); // Header
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content

                // Header
                Border headerBorder = new Border { Background = new SolidColorBrush(_userAccentColor), Height = 40 };
                Grid.SetRow(headerBorder, 0);

                Grid headerGrid = new Grid();
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });

                TextBlock titleBlock = new TextBlock
                {
                    Text = "Seçenekler",
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(20, 0, 0, 0)
                };

                Button closeButton = new Button
                {
                    Content = "✕",
                    Width = 32,
                    Height = 32,
                    Foreground = Brushes.White,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };
                closeButton.Click += (s, e) => _optionsWindow.Close();

                // Unobtrusive update notice: a link in the header, only when a newer release exists.
                // Added after the title so it is on top: the title TextBlock fills the cell and
                // would otherwise swallow the clicks.
                TextBlock updateLink = CreateUpdateLink();

                Grid.SetColumn(titleBlock, 0); headerGrid.Children.Add(titleBlock);
                Grid.SetColumn(updateLink, 0); headerGrid.Children.Add(updateLink);
                Grid.SetColumn(closeButton, 1); headerGrid.Children.Add(closeButton);
                headerBorder.Child = headerGrid;
                headerBorder.MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) _optionsWindow.DragMove(); };

                CreateTabContent(mainGrid);
                WireApplyOnChange();

                mainGrid.Children.Add(headerBorder);
                mainBorder.Child = mainGrid;
                _optionsWindow.Content = mainBorder;
                _optionsWindow.KeyDown += (s, e) => { if (e.Key == Key.Enter || e.Key == Key.Escape) _optionsWindow.Close(); };
                // Drop the static references once the dialog is gone so the whole visual tree
                // (and everything its controls capture) can be collected.
                _optionsWindow.Closed += (s, e) =>
                {
                    FlushPendingApply();
                    if (_hotkeysChangedWhileOpen)
                        MessageBoxesManager.ShowOKOnlyMessageBoxForm("Genel kısayol tuşu değişiklikleri kaydedildi ve tüm profillere uygulandı.\n\nYeni kısayolları etkinleştirmek için lütfen DesktopPossible'ı yeniden başlatın.", "Yeniden başlatma gerekli");
                    _tabControl = null; _optionsWindow = null;
                };
                // Pause Here:
                AutoOrganizeManager.Pause();

                IsOpen = true;
                try
                {
                    _optionsWindow.ShowDialog();
                }
                finally
                {
                    IsOpen = false;
                    // Resume in the finally so an exception during the dialog can't leave
                    // Auto-Organize permanently paused.
                    AutoOrganizeManager.Resume();
                }
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, LogManager.LogCategory.UI, $"Error showing Options: {ex.Message}");
            }
        }

        private static TextBlock CreateUpdateLink()
        {
            var link = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Foreground = Brushes.White,
                TextDecorations = TextDecorations.Underline,
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                Visibility = Visibility.Collapsed
            };
            link.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true; // don't start a window drag
                try { Process.Start(new ProcessStartInfo { FileName = UpdateChecker.ReleaseUrl, UseShellExecute = true }); }
                catch (Exception ex) { LogManager.Log(LogManager.LogLevel.Warn, LogManager.LogCategory.UI, $"Open release page failed: {ex.Message}"); }
            };

            void Refresh()
            {
                if (!UpdateChecker.IsUpdateAvailable) return;
                link.Text = $"Güncelleme var: v{UpdateChecker.LatestVersion?.ToString(3)}";
                link.ToolTip = $"Şu an v{UpdateChecker.CurrentVersion.ToString(3)} sürümünü kullanıyorsunuz. Sürüm sayfasını açmak için tıklayın.";
                link.Visibility = Visibility.Visible;
            }

            Refresh();
            // If a check finishes while the window is open, reveal the link then.
            Action onFound = () => link.Dispatcher.BeginInvoke(new Action(Refresh));
            UpdateChecker.UpdateFound += onFound;
            link.Unloaded += (s, e) => UpdateChecker.UpdateFound -= onFound;
            _ = UpdateChecker.CheckAsync(); // no-op if checked recently

            return link;
        }

        private static void CreateTabContent(Grid mainGrid)
        {
            Grid contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(contentGrid, 1);

            StackPanel tabPanel = new StackPanel { Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)), Margin = new Thickness(0, 20, 0, 0) };
            Border contentBorder = new Border { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(218, 220, 224)), BorderThickness = new Thickness(1, 0, 0, 0), Padding = new Thickness(20), Margin = new Thickness(0, 20, 0, 0) };

            _tabControl = new TabControl { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            var template = new ControlTemplate(typeof(TabControl));
            template.VisualTree = new FrameworkElementFactory(typeof(ContentPresenter));
            ((FrameworkElementFactory)template.VisualTree).SetValue(ContentPresenter.ContentSourceProperty, "SelectedContent");
            _tabControl.Template = template;

            CreateGeneralTab();
            CreateStyleTab();
            CreateToolsTab();
            CreateProfilesTab();
            CreateHotkeysTab();
            CreateSmartDesktopTab();
            CreateLookDeeperTab();
            CreateTextFramesTab();

            _tabControl.SelectedIndex = _lastSelectedTabIndex;
            CreateTabButton(tabPanel, "Genel", 0, _lastSelectedTabIndex == 0);
            CreateTabButton(tabPanel, "Stil ve efektler", 1, _lastSelectedTabIndex == 1);
            CreateTabButton(tabPanel, "Araçlar", 2, _lastSelectedTabIndex == 2);
            CreateTabButton(tabPanel, "Profiller", 3, _lastSelectedTabIndex == 3);
            CreateTabButton(tabPanel, "Kısayol tuşları", 4, _lastSelectedTabIndex == 4);
            CreateTabButton(tabPanel, "Akıllı masaüstü", 5, _lastSelectedTabIndex == 5);
            CreateTabButton(tabPanel, "Ayrıntılar", 6, _lastSelectedTabIndex == 6);
            CreateTabButton(tabPanel, "Metin kutuları", 7, _lastSelectedTabIndex == 7);

            contentBorder.Child = _tabControl;
            Grid.SetColumn(tabPanel, 0); contentGrid.Children.Add(tabPanel);
            Grid.SetColumn(contentBorder, 1); contentGrid.Children.Add(contentBorder);
            mainGrid.Children.Add(contentGrid);
        }

        private static void CreateTabButton(StackPanel parent, string title, int tabIndex, bool isSelected)
        {
            Button tabButton = new Button
            {
                Content = title,
                Height = 40,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(20, 0, 0, 0),
                Margin = new Thickness(0, 0, 0, 2)
            };
            SetTabButtonColors(tabButton, title, isSelected);
            tabButton.Click += (s, e) => SelectTab(tabIndex, tabButton);
            tabButton.MouseEnter += (s, e) => { if (_tabControl.SelectedIndex != tabIndex) SetTabButtonColors(tabButton, title, false, true); };
            tabButton.MouseLeave += (s, e) => { if (_tabControl.SelectedIndex != tabIndex) SetTabButtonColors(tabButton, title, false, false); };
            parent.Children.Add(tabButton);
        }

        private static void SetTabButtonColors(Button button, string title, bool isSelected, bool isHover = false)
        {
            Color activeColor = title switch { "Stil ve efektler" => ColorStyle, "Araçlar" => ColorTools, "Profiller" => ColorProfiles, "Kısayol tuşları" => ColorHotkeys, "Akıllı masaüstü" => ColorSmartDesktop, "Ayrıntılar" => ColorLookDeeper, "Metin kutuları" => ColorTextFrames, _ => _userAccentColor };
            if (isSelected) { button.Background = new SolidColorBrush(activeColor); button.Foreground = Brushes.White; }
            else if (isHover) { button.Background = new SolidColorBrush(Color.FromRgb((byte)(activeColor.R + 40), (byte)(activeColor.G + 40), (byte)(activeColor.B + 40))); button.Foreground = Brushes.White; }
            else { button.Background = new SolidColorBrush(Color.FromRgb(200, 200, 200)); button.Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60)); }
        }

        private static void SelectTab(int tabIndex, Button selectedButton)
        {
            _lastSelectedTabIndex = tabIndex;
            _tabControl.SelectedIndex = tabIndex;
            StackPanel tabPanel = (StackPanel)selectedButton.Parent;
            for (int i = 0; i < tabPanel.Children.Count; i++) if (tabPanel.Children[i] is Button btn) SetTabButtonColors(btn, btn.Content.ToString(), i == tabIndex);
        }

        // --- Tabs ---
        private static void CreateGeneralTab()
        {
            TabItem t = new TabItem();
            StackPanel c = new StackPanel();
            CreateSectionHeader(c, "Başlangıç", _userAccentColor);
            CreateCheckBox(c, "Windows ile başlat", "StartWithWindows", TrayManager.IsStartWithWindows);
            CreateSectionHeader(c, "Seçimler", _userAccentColor);
            CreateCheckBox(c, "Tek tıklamayla başlat", "SingleClickToLaunch", SettingsManager.SingleClickToLaunch);
            CreateCheckBox(c, "Kutuları diğer kutulara ve ekran kenarlarına hizala", "EnableSnapNearFrames", SettingsManager.IsSnapEnabled);
            CreateCheckBox(c, "Kutudaki öğeleri masaüstünde de tut", "KeepFramedItemsOnDesktop", SettingsManager.KeepFramedItemsOnDesktop);
            CreateCheckBox(c, "Sistem tepsisi simgesini göster", "EnableTrayIcon", SettingsManager.ShowInTray);
            CreateCheckBox(c, "Portal kutularındaki 'Öğeyi sil' komutunda Geri Dönüşüm Kutusu'nu kullan", "UseRecycleBin", SettingsManager.UseRecycleBin);

            // NEW: Context Menu Option
            CreateCheckBox(c, "Masaüstü sağ tık menüsünde 'Yeni kutu' seçeneğini göster", "EnableContextMenu", SettingsManager.EnableContextMenu);

            // Moved from Style Tab (Choices)
            CreateCheckBox(c, "Portal kutularında filigranı göster", "EnablePortalWatermark", SettingsManager.ShowBackgroundImageOnPortalFrames);
            CreateCheckBox(c, "Kutu kaydırma çubuklarını kapat", "DisableFrameScrollbars", SettingsManager.DisableFrameScrollbars);

            // --- Virtual Desktops ---
            CreateSectionHeader(c, "Sanal masaüstleri", _userAccentColor);

            // Checkbox: follow Windows virtual desktops by name (profile "Work" activates
            // on a desktop named "Work"; unmatched desktops fall back to Default).
            CheckBox vdAutoCb = CreateCheckBoxReturn(c, "Sanal masaüstüne göre profili otomatik değiştir", "EnableVirtualDesktopAutomation", SettingsManager.EnableVirtualDesktopAutomation);
            // Use Click event to ensure it only fires on user interaction, then SaveSettings immediately
            vdAutoCb.Click += (s, e) => {
                bool isChecked = vdAutoCb.IsChecked == true;
                SettingsManager.EnableVirtualDesktopAutomation = isChecked;
                SettingsManager.SaveSettings(); // Force write to JSON immediately
                if (isChecked) VirtualDesktopAutomationManager.Start();   // applies current desktop immediately
                else VirtualDesktopAutomationManager.Stop(switchToDefault: true);
            };

            t.Content = new ScrollViewer { Content = c, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _tabControl.Items.Add(t);
        }

        private static void CreateStyleTab()
        {
            TabItem t = new TabItem();
            StackPanel c = new StackPanel();

            CreateSectionHeader(c, "Görünüm", ColorStyle);

            // --- CHAMELEON TOGGLE ---
            var chamCb = CreateCheckBoxReturn(c, "Bukalemun modunu etkinleştir (duvar kâğıdı rengine otomatik uy)", "EnableChameleon", SettingsManager.EnableChameleonMode);
            chamCb.ToolTip = "Kutular, masaüstü arka planınızla uyum sağlamak için rengini otomatik olarak değiştirir.";

            CreateSliderControl(c, "Kutu tonu", "TintSlider", SettingsManager.TintValue);
            CreateSliderControl(c, "Menü tonu", "MenuTintSlider", SettingsManager.MenuTintValue);

            // Pass the chamCb reference so we can wire up the toggle event
            CreateColorAndEffectComboBoxes(c, chamCb);

            // --- Moved from General Tab (Auto-Hide Options) ---
            CreateSectionHeader(c, "Otomatik gizleme", ColorStyle);
            CreateCheckBox(c, "Kutuları otomatik gizle", "AutoHideFrames", SettingsManager.AutoHideFrames);
            CreateSliderControl(c, "Otomatik gizleme süresi (sn)", "AutoHideTimeSlider", SettingsManager.AutoHideTime, 300);

            // --- Moved from General Tab (Idle Fade-Out Options) ---
            CreateSectionHeader(c, "Boştayken soldurma", ColorStyle);
            CreateCheckBox(c, "Boştayken soldurmayı etkinleştir", "FramesFadeOutFx", SettingsManager.FramesFadeOutFx);
            CreateSliderControl(c, "Boşta kalma süresi (sn)", "FadeOutTimeSlider", SettingsManager.FadeOutTime, 300);
            CreateSliderControl(c, "Hedef opaklık (%)", "FadeOutAlphaSlider", (int)(SettingsManager.FadeOutFxTargetAlpha * 100), 100);
            CreateSliderControl(c, "Uyandırma gecikmesi (ms)", "FadeWakeDelaySlider", SettingsManager.FadeWakeDelayMs, 2000, min: 0); // 0 = instant (classic)

            // --- Moved from General Tab (Idle Auto-Roll Options) ---
            CreateSectionHeader(c, "Boştayken otomatik katlama", ColorStyle);
            CreateSliderControl(c, "Boşta kalma süresi (sn)", "AutoRollTimeSlider", SettingsManager.AutoRollTime, 300);

            // --- NEW: Desktop Icon Visibility ---
            CreateSectionHeader(c, "Masaüstü simgesi görünürlüğü", ColorStyle);
            CreateCheckBox(c, "Program çalışırken Windows masaüstü simgelerini gizle", "HideDesktopElementsOnStart", SettingsManager.HideDesktopElementsOnStart);
            CreateCheckBox(c, "Kutular gizlendiğinde Windows masaüstü simgelerini gizle", "HideDesktopElementsOnAllFramesHide", SettingsManager.HideDesktopElementsOnAllFramesHide);
            CreateCheckBox(c, "Masaüstü simgelerini göstermek/gizlemek için boş masaüstüne çift tıkla", "ToggleDesktopIconsOnDoubleClick", SettingsManager.ToggleDesktopIconsOnDoubleClick);

            // --- NEW: Frames behavior ---
            CreateSectionHeader(c, "Kutular", ColorStyle);
            CreateCheckBox(c, "Tüm kutuları göster/gizle kısayol tuşunu etkinleştir", "EnableToggleFramesHotkey", SettingsManager.EnableToggleFramesHotkey);
            CreateCheckBox(c, "Portal ayrıntı görünümünde satırları çizgili göster", "PortalDetailsStriped", SettingsManager.PortalDetailsStriped);

            // Image frames: how dragged/added image files are stored.
            Grid imgModeGrid = new Grid { Margin = new Thickness(15, 4, 0, 4) };
            imgModeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            imgModeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var imgLbl = new TextBlock { Text = "Görsel kutusuna görsel eklerken:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            Grid.SetColumn(imgLbl, 0);
            var imgCmb = new ComboBox { Name = "ImageDropModeComboBox", Width = 150, HorizontalAlignment = HorizontalAlignment.Left, Height = 25, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            imgCmb.Items.Add(new ComboBoxItem { Content = "Kutuya kopyala", Tag = "Copy" });
            imgCmb.Items.Add(new ComboBoxItem { Content = "Orijinale bağla", Tag = "Reference" });
            imgCmb.Items.Add(new ComboBoxItem { Content = "Her seferinde sor", Tag = "Ask" });
            string curImgMode = SettingsManager.ImageDropMode ?? "Copy";
            imgCmb.SelectedIndex = curImgMode == "Reference" ? 1 : curImgMode == "Ask" ? 2 : 0;
            Grid.SetColumn(imgCmb, 1);
            imgModeGrid.Children.Add(imgLbl);
            imgModeGrid.Children.Add(imgCmb);
            c.Children.Add(imgModeGrid);

            // Customizable combo for the Show/Hide-all hotkey (press-to-capture).
            string ToggleHotkeyDisp()
            {
                int dvk = SettingsManager.ToggleFramesKey;
                if (dvk == 0) return "(yok)";
                string ml = (SettingsManager.ToggleFramesModifier ?? "").ToLower();
                string disp = "";
                if (ml.Contains("ctrl") || ml.Contains("control")) disp += "Ctrl+";
                if (ml.Contains("alt")) disp += "Alt+";
                if (ml.Contains("shift")) disp += "Shift+";
                if (ml.Contains("win")) disp += "Win+";
                return disp + KeyInterop.KeyFromVirtualKey(dvk).ToString();
            }

            Grid hkGrid = new Grid { Margin = new Thickness(15, 2, 0, 8) };
            hkGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hkGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hkGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock hkLbl = new TextBlock { Text = "Kısayol tuşu:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            Grid.SetColumn(hkLbl, 0);

            TextBox hkTxt = new TextBox { IsReadOnly = true, IsReadOnlyCaretVisible = false, VerticalAlignment = VerticalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), Text = ToggleHotkeyDisp() };
            Grid.SetColumn(hkTxt, 1);

            Button hkBtn = new Button { Content = "Ata", Width = 64 };
            Grid.SetColumn(hkBtn, 2);

            // Capture via the global hook so setting the combo doesn't trigger it, and Win is allowed.
            bool hkCapturing = false;
            hkBtn.Click += (s, e) =>
            {
                hkCapturing = true; hkBtn.Content = "Basın…"; hkTxt.Text = "Bir tuş birleşimine basın…";
                GlobalHotkeyManager.BeginHotkeyCapture((vk, mods) =>
                {
                    hkCapturing = false; hkBtn.Content = "Ata";
                    if (vk == 0) { hkTxt.Text = ToggleHotkeyDisp(); return; } // cancelled
                    string ms = "";
                    if ((mods & 1) != 0) ms += "ctrl+";
                    if ((mods & 2) != 0) ms += "alt+";
                    if ((mods & 4) != 0) ms += "shift+";
                    if ((mods & 8) != 0) ms += "win+";
                    SettingsManager.ToggleFramesModifier = ms.TrimEnd('+');
                    SettingsManager.ToggleFramesKey = vk;
                    hkTxt.Text = ToggleHotkeyDisp();
                });
            };
            if (_optionsWindow != null) _optionsWindow.Closed += (s, e) => { if (hkCapturing) GlobalHotkeyManager.CancelHotkeyCapture(); };

            hkGrid.Children.Add(hkLbl);
            hkGrid.Children.Add(hkTxt);
            hkGrid.Children.Add(hkBtn);
            c.Children.Add(hkGrid);



            // --- Icons Section ---
            CreateSectionHeader(c, "Simgeler", ColorStyle);
            Grid iconGrid = new Grid { Margin = new Thickness(15, 5, 0, 15) };
            iconGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            iconGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel menuIconPanel = new StackPanel();
            menuIconPanel.Children.Add(new TextBlock { Text = "Menü simgesi", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 0) });
            CreateIconRadioButtonGroup(menuIconPanel, "MenuIconGroup", new Dictionary<string, int> { { "♥", 0 }, { "☰", 1 }, { "≣", 2 }, { "𓃑", 3 } }, SettingsManager.MenuIcon);

            StackPanel lockIconPanel = new StackPanel();
            lockIconPanel.Children.Add(new TextBlock { Text = "Konum kilidi simgesi", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 0) });
            CreateIconRadioButtonGroup(lockIconPanel, "LockIconGroup", new Dictionary<string, int> { { Framemanager.PosLockGlyph(0), 0 }, { Framemanager.PosLockGlyph(1), 1 } }, SettingsManager.LockIcon <= 1 ? SettingsManager.LockIcon : 0, "Segoe Fluent Icons, Segoe MDL2 Assets");

            // Filter/search icon (frame title bar): magnifier / funnel / boxed magnifier.
            StackPanel filterIconPanel = new StackPanel();
            filterIconPanel.Children.Add(new TextBlock { Text = "Filtre simgesi", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 0) });
            CreateIconRadioButtonGroup(filterIconPanel, "FilterIconGroup", new Dictionary<string, int> { { Framemanager.FilterGlyph(0), 0 }, { Framemanager.FilterGlyph(1), 1 }, { Framemanager.FilterGlyph(2), 2 } }, SettingsManager.FilterIcon, "Segoe Fluent Icons, Segoe MDL2 Assets");

            Grid.SetColumn(menuIconPanel, 0);
            Grid.SetColumn(lockIconPanel, 1);
            iconGrid.Children.Add(menuIconPanel);
            iconGrid.Children.Add(lockIconPanel);
            c.Children.Add(iconGrid);

            Grid filterIconGrid = new Grid { Margin = new Thickness(15, 0, 0, 15) };
            filterIconGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            filterIconGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(filterIconPanel, 0);
            filterIconGrid.Children.Add(filterIconPanel);
            c.Children.Add(filterIconGrid);

            // System tray icon style — visual radio group with a preview of each style (theme-aware
            // minimalist glyph, drawn in GDI+ at runtime). Parsed in the save handler via "TrayIconGroup".
            Grid trayGrid = new Grid { Margin = new Thickness(15, 0, 0, 12) };
            StackPanel trayContainer = new StackPanel();
            trayContainer.Children.Add(new TextBlock { Text = "Sistem tepsisi simgesi", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) });
            StackPanel trayGroup = new StackPanel { Orientation = Orientation.Horizontal, Tag = "TrayIconGroup" };
            string curTray = SettingsManager.TrayIconStyle ?? "Nested";
            foreach (var (style, caption) in new[] { ("Nested", "İç içe"), ("Stacked", "Üst üste"), ("Grid", "Izgara") })
            {
                var preview = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
                preview.Children.Add(BuildTrayPreview(style));
                preview.Children.Add(new TextBlock { Text = caption, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0) });
                var rb = new RadioButton { GroupName = "TrayIconStyle", Tag = style, Content = preview, IsChecked = curTray == style, Margin = new Thickness(0, 0, 16, 0), VerticalContentAlignment = VerticalAlignment.Center };
                trayGroup.Children.Add(rb);
            }
            trayContainer.Children.Add(trayGroup);
            trayGrid.Children.Add(trayContainer);
            c.Children.Add(trayGrid);

            t.Content = new ScrollViewer { Content = c, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _tabControl.Items.Add(t);
        }

        private static void CreateToolsTab()
        {
            TabItem t = new TabItem();
            StackPanel c = new StackPanel();
            CreateSectionHeader(c, "Araçlar", ColorTools);

            Grid g = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(45) });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(15) });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(45) });

            Button b1 = CreateStyledButton("Yedekle", ColorTools); b1.Click += (s, e) => BackupManager.BackupData();
            Button b2 = CreateStyledButton("Geri yükle...", Color.FromRgb(255, 152, 0)); b2.Click += (s, e) => RestoreBackup();
            Button b3 = CreateStyledButton("Yedek klasörünü aç", Color.FromRgb(0, 123, 191)); b3.Click += (s, e) => OpenBackupsFolder();
            Grid.SetRow(b1, 0); Grid.SetColumn(b1, 0);
            Grid.SetRow(b2, 0); Grid.SetColumn(b2, 2);
            Grid.SetRow(b3, 2); Grid.SetColumn(b3, 0); Grid.SetColumnSpan(b3, 3);
            g.Children.Add(b1); g.Children.Add(b2); g.Children.Add(b3);
            c.Children.Add(g);

            CreateCheckBox(c, "Otomatik yedek (günlük)", "EnableAutoBackup", SettingsManager.EnableAutoBackup);

            // Backup limits: automatic-backup retention count and the per-backup size cap.
            Grid backupCfg = new Grid { Margin = new Thickness(15, 4, 0, 4) };
            backupCfg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
            backupCfg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            backupCfg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            backupCfg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock lblCount = new TextBlock { Text = "Saklanacak otomatik yedek sayısı:", FontFamily = new FontFamily("Segoe UI"), FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) };
            TextBox tbCount = new TextBox { Name = "MaxBackupCountBox", Text = SettingsManager.MaxBackupCount.ToString(), Height = 24, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) };
            Grid.SetRow(lblCount, 0); Grid.SetColumn(lblCount, 0);
            Grid.SetRow(tbCount, 0); Grid.SetColumn(tbCount, 1);

            TextBlock lblSize = new TextBlock { Text = "En büyük yedek boyutu (MB):", FontFamily = new FontFamily("Segoe UI"), FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            TextBox tbSize = new TextBox { Name = "MaxBackupSizeBox", Text = SettingsManager.MaxBackupSizeMB.ToString(), Height = 24, VerticalContentAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblSize, 1); Grid.SetColumn(lblSize, 0);
            Grid.SetRow(tbSize, 1); Grid.SetColumn(tbSize, 1);

            backupCfg.Children.Add(lblCount); backupCfg.Children.Add(tbCount);
            backupCfg.Children.Add(lblSize); backupCfg.Children.Add(tbSize);
            c.Children.Add(backupCfg);

            // --- Maintenance Section ---
            Color darkPink = Color.FromRgb(199, 21, 133); // MediumVioletRed
            CreateSectionHeader(c, "Bakım", darkPink);

            Button btnBound = CreateStyledButton("Kutuları ekrana sığdır", darkPink);
            btnBound.Width = 255;
            btnBound.Height = 45;
            btnBound.Margin = new Thickness(0, 0, 0, 15);
            btnBound.HorizontalAlignment = HorizontalAlignment.Left;

            // Enable ONLY if Auto-Reposition is OFF (Manual Mode)
            btnBound.IsEnabled = !SettingsManager.AllowAutoReposition;

            if (btnBound.IsEnabled)
            {
                btnBound.Click += (s, e) =>
                {
                    // This calls the wrapper that handles the variable flipping
                    Framemanager.ForceRepositionallFrames();

                    MessageBoxesManager.ShowOKOnlyMessageBoxForm("Tüm kutular denetlendi ve geçerli ekran sınırları içine taşındı.", "Başarılı");
                };
            }
            else
            {
                btnBound.Opacity = 0.90;
                btnBound.ToolTip = "Otomatik yeniden konumlandırma etkin (gizli ayar). Kutular zaten otomatik yönetiliyor.";
            }

            c.Children.Add(btnBound);

            // --- Display Configurations ---
            CreateCheckBox(c, "Kutu düzenini her ekran yapılandırması için ayrı hatırla", "EnableDisplayLayoutMemory", SettingsManager.EnableDisplayLayoutMemory);

            TextBlock lblDisplays = new TextBlock
            {
                Text = "Kutu konumları her monitör yapılandırması için (yerleştirme, çözünürlük " +
                       "veya ölçek değişiklikleri, RDP) ayrı kaydedilir ve o yapılandırma geri geldiğinde geri yüklenir. " +
                       "İlk kez görülen bir yapılandırmanın düzeni, az önce kullandığınız düzenden oluşturulur." +
                       Environment.NewLine + Environment.NewLine +
                       "Şu anki: " + DisplayLayoutManager.DescribeCurrentDisplays(),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Opacity = 0.75,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(15, 0, 0, 12)
            };
            c.Children.Add(lblDisplays);

            Button btnForget = CreateStyledButton("Bu ekran için düzeni yeniden oluştur", darkPink);
            btnForget.Width = 320;
            btnForget.Height = 45;
            btnForget.Margin = new Thickness(0, 0, 0, 15);
            btnForget.HorizontalAlignment = HorizontalAlignment.Left;
            btnForget.ToolTip = "Kullanılan monitörler için hatırlanan düzeni siler ve en son kullandığınız diğer yapılandırmadan yeniden oluşturur.";
            btnForget.Click += (s, e) =>
            {
                if (!MessageBoxesManager.ShowCustomYesNoMessageBox(
                        "Şu an kullandığınız ekran yapılandırması için kaydedilmiş kutu düzeni silinip " +
                        "en son kullandığınız diğer yapılandırmadan yeniden oluşturulsun mu?",
                        "Düzeni yeniden oluştur")) return;

                DisplayLayoutManager.ForgetCurrentDisplayLayout();
                MessageBoxesManager.ShowOKOnlyMessageBoxForm(
                    "Bu ekran yapılandırmasının düzeni yeniden oluşturuldu.", "Düzeni yeniden oluştur");
            };
            c.Children.Add(btnForget);

            CreateSectionHeader(c, "Sıfırlama", Colors.Red);
            Button r1 = CreateStyledButton("Stilleri sıfırla", Color.FromRgb(108, 117, 125));
            r1.Width = 255; r1.Height = 45; r1.Margin = new Thickness(0, 0, 0, 15);
            r1.Click += (s, e) => { if (MessageBoxesManager.ShowCustomYesNoMessageBox("Tüm görsel özelleştirmeler sıfırlansın mı?", "Sıfırla")) { Framemanager.ResetAllCustomizations(); _optionsWindow.Close(); } };

            Button r2 = CreateStyledButton("Tüm verileri temizle", Color.FromRgb(220, 53, 69));
            r2.Width = 255; r2.Height = 45;
            r2.Click += (s, e) => PerformFullFactoryReset();

            StackPanel rs = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left };
            rs.Children.Add(r1); rs.Children.Add(r2);
            c.Children.Add(rs);

            t.Content = c;
            _tabControl.Items.Add(t);
        }





        private static void CreateProfilesTab()
        {
            TabItem t = new TabItem();
            Grid c = new Grid();
            c.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            c.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            StackPanel headerPanel = new StackPanel();
            CreateSectionHeader(headerPanel, "Profil yönetimi", ColorProfiles);

            // File safety: move the files stored inside frames (any profile) back to the Desktop.
            Button btnEmptyFrames = new Button
            {
                Content = "Kutuları masaüstüne boşalt...",
                Height = 32,
                Padding = new Thickness(14, 0, 14, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(15, 0, 0, 10),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(218, 220, 224)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = "Seçilen kutularda saklanan dosyaları masaüstünüze taşır. Kutular korunur."
            };
            btnEmptyFrames.Click += (s, e) => EmptyFramesDialog.ShowDialogOnUiThread();
            headerPanel.Children.Add(btnEmptyFrames);

            TextBlock vdHint = new TextBlock
            {
                Text = "İpucu: Bir profile Windows sanal masaüstüyle aynı adı verirseniz, o masaüstüne geçtiğinizde profil otomatik olarak etkinleşir (Genel → Sanal masaüstüne göre profili otomatik değiştir). Eşleşen profili olmayan masaüstleri Default profiline döner.",
                TextWrapping = TextWrapping.Wrap,
                FontStyle = FontStyles.Italic,
                Foreground = Brushes.Gray,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Margin = new Thickness(15, 0, 15, 10)
            };
            headerPanel.Children.Add(vdHint);

            Grid.SetRow(headerPanel, 0);
            c.Children.Add(headerPanel);

            // Profile management embedded directly in the tab (the same panel is hosted
            // by the tray menu's "Manage Profiles..." window, see ProfileManagerForm).
            ProfileManagerPanel profilePanel = new ProfileManagerPanel();
            Grid.SetRow(profilePanel, 1);
            c.Children.Add(profilePanel);

            t.Content = c;
            _tabControl.Items.Add(t);
        }

        private static readonly Dictionary<string, int> AvailableKeys = new Dictionary<string, int>
        {
            {"A", 0x41}, {"B", 0x42}, {"C", 0x43}, {"D", 0x44}, {"E", 0x45}, {"F", 0x46}, {"G", 0x47}, {"H", 0x48}, {"I", 0x49}, {"J", 0x4A}, {"K", 0x4B}, {"L", 0x4C}, {"M", 0x4D}, {"N", 0x4E}, {"O", 0x4F}, {"P", 0x50}, {"Q", 0x51}, {"R", 0x52}, {"S", 0x53}, {"T", 0x54}, {"U", 0x55}, {"V", 0x56}, {"W", 0x57}, {"X", 0x58}, {"Y", 0x59}, {"Z", 0x5A},
            {"0", 0x30}, {"1", 0x31}, {"2", 0x32}, {"3", 0x33}, {"4", 0x34}, {"5", 0x35}, {"6", 0x36}, {"7", 0x37}, {"8", 0x38}, {"9", 0x39},
            {"F1", 0x70}, {"F2", 0x71}, {"F3", 0x72}, {"F4", 0x73}, {"F5", 0x74}, {"F6", 0x75}, {"F7", 0x76}, {"F8", 0x77}, {"F9", 0x78}, {"F10", 0x79}, {"F11", 0x7A}, {"F12", 0x7B},
            {"Virgül (,)", 0xBC}, {"Nokta (.)", 0xBE}, {"Tilde (~)", 192}, {"Boşluk", 32}, {"Tab", 9}, {"Enter", 13}, {"Escape", 27}
        };

        private static void CreateHotkeysTab()
        {
            TabItem t = new TabItem();
            StackPanel c = new StackPanel();

            CreateSectionHeader(c, "Profil değiştirme", ColorHotkeys);
            CheckBox cbProf = CreateCheckBoxReturn(c, "Profil değiştirme kısayol tuşlarını etkinleştir", "EnableProfileHotkeys", SettingsManager.EnableProfileHotkeys);
            Grid gProf1 = CreateHotkeyEditor(c, "Doğrudan profil [0-9]", "ProfSwitch", SettingsManager.ProfileSwitchModifier, 0, false);
            Grid gProf2 = CreateHotkeyEditor(c, "Önceki profil", "ProfPrev", SettingsManager.ProfilePrevModifier, SettingsManager.ProfilePrevKey, true);
            Grid gProf3 = CreateHotkeyEditor(c, "Sonraki profil", "ProfNext", SettingsManager.ProfileNextModifier, SettingsManager.ProfileNextKey, true);

            // Bind initial state and live toggling
            gProf1.IsEnabled = gProf2.IsEnabled = gProf3.IsEnabled = cbProf.IsChecked == true;
            cbProf.Click += (s, e) => gProf1.IsEnabled = gProf2.IsEnabled = gProf3.IsEnabled = cbProf.IsChecked == true;

            TextBlock infoText = new TextBlock
            {
                Text = "Not: Genel kısayol tuşu değişikliklerinin etkili olması için uygulamanın yeniden başlatılması gerekir.",
                FontStyle = FontStyles.Italic,
                Foreground = Brushes.Gray,
                Margin = new Thickness(15, 20, 0, 0)
            };
            c.Children.Add(infoText);

            t.Content = new ScrollViewer { Content = c, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _tabControl.Items.Add(t);
        }

        private static Grid CreateHotkeyEditor(StackPanel p, string label, string namePrefix, string currentMod, int currentKey, bool hasKeySelector)
        {
            Grid g = new Grid { Margin = new Thickness(15, 5, 0, 15) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock lbl = new TextBlock { Text = label, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lbl, 0); g.Children.Add(lbl);

            StackPanel spMods = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            string curModLower = (currentMod ?? "").ToLower();

            CheckBox chkCtrl = new CheckBox { Name = namePrefix + "Ctrl", Content = "Ctrl", IsChecked = curModLower.Contains("ctrl") || curModLower.Contains("control"), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            CheckBox chkAlt = new CheckBox { Name = namePrefix + "Alt", Content = "Alt", IsChecked = curModLower.Contains("alt"), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            CheckBox chkShift = new CheckBox { Name = namePrefix + "Shift", Content = "Shift", IsChecked = curModLower.Contains("shift"), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            CheckBox chkWin = new CheckBox { Name = namePrefix + "Win", Content = "Win", IsChecked = curModLower.Contains("win"), Margin = new Thickness(0, 0, 15, 0), VerticalAlignment = VerticalAlignment.Center };

            spMods.Children.Add(chkCtrl);
            spMods.Children.Add(chkAlt);
            spMods.Children.Add(chkShift);
            spMods.Children.Add(chkWin);

            if (hasKeySelector)
            {
                spMods.Children.Add(new TextBlock { Text = "+", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
                ComboBox cmb = new ComboBox { Name = namePrefix + "Key", Width = 100, VerticalAlignment = VerticalAlignment.Center };
                foreach (var kvp in AvailableKeys)
                {
                    ComboBoxItem item = new ComboBoxItem { Content = kvp.Key, Tag = kvp.Value };
                    cmb.Items.Add(item);
                    if (kvp.Value == currentKey) cmb.SelectedItem = item;
                }
                if (cmb.SelectedIndex == -1 && cmb.Items.Count > 0) cmb.SelectedIndex = 0;
                spMods.Children.Add(cmb);
            }

            Grid.SetColumn(spMods, 1);
            g.Children.Add(spMods);
            p.Children.Add(g);
            return g;
        }


        private static void CreateSmartDesktopTab()
        {
            TabItem t = new TabItem();
            StackPanel c = new StackPanel();

            CreateSectionHeader(c, "Akıllı masaüstü (Otomatik düzen)", ColorSmartDesktop);

            CheckBox cbMain = CreateCheckBoxReturn(c, "Otomatik düzeni etkinleştir", "EnableAutoOrganize", SettingsManager.EnableAutoOrganize);

            CheckBox cbNotif = CreateCheckBoxReturn(c, "İşlem bildirimlerini göster", "EnableAutoOrganizeNotifications", SettingsManager.EnableAutoOrganizeNotifications);
            cbNotif.Margin = new Thickness(35, 0, 0, 8); // Indent it!
            cbNotif.IsEnabled = cbMain.IsChecked == true;

            CreateCheckBoxReturn(c, "Bilinmeyen uygulamaları çevrimiçi ara (uygulama adlarını Chocolatey'e gönderir)", "EnableOnlineCategoryLookup", SettingsManager.EnableOnlineCategoryLookup);

            // "Arrange Now": sort everything currently on the desktop into category
            // frames. Deliberately independent of the Enable Auto-Organize toggle
            // (that toggle only governs NEW arrivals) - always clickable.
            Button btnArrangeNow = new Button
            {
                Content = "Şimdi düzenle",
                Height = 32,
                Padding = new Thickness(14, 0, 14, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(15, 6, 0, 10),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(218, 220, 224)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = "Masaüstündeki tüm uygulama, belge ve görselleri şimdi kategori kutularına yerleştirir."
            };
            btnArrangeNow.Click += async (s, e) =>
            {
                btnArrangeNow.IsEnabled = false;
                btnArrangeNow.Content = "Düzenleniyor...";
                try
                {
                    var (items, categories) = await AppCategorizer.SortDesktopAsync();
                    SmartToast.Show("Masaüstü düzenlendi", $"{items} öğe {categories} kategoriye yerleştirildi");
                }
                catch (Exception ex)
                {
                    LogManager.Log(LogManager.LogLevel.Error, LogManager.LogCategory.General,
                        $"Arrange Now failed: {ex.Message}");
                    MessageBoxesManager.ShowOKOnlyMessageBoxForm($"Düzenleme başarısız: {ex.Message}", "Hata");
                }
                finally
                {
                    btnArrangeNow.Content = "Şimdi düzenle";
                    btnArrangeNow.IsEnabled = true;
                }
            };
            c.Children.Add(btnArrangeNow);

            // "Arrange Frames": size every frame to its content and tile them all on the
            // primary work area. Independent of Auto-Organize, always clickable.
            Button btnArrangeFrames = new Button
            {
                Content = "Kutuları düzenle",
                Height = 32,
                Padding = new Thickness(14, 0, 14, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(15, 0, 0, 10),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(218, 220, 224)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = "Her kutuyu simgelerine göre boyutlandırır ve tüm kutuları birincil monitöre düzenli yerleştirir."
            };
            btnArrangeFrames.Click += (s, e) =>
            {
                if (!MessageBoxesManager.ShowCustomYesNoMessageBox(
                        "Görünür tüm kutular içeriğine göre boyutlandırılıp birincil monitöre yeniden yerleştirilsin mi?\n\nKutu konumları ve boyutları değişecek.",
                        "Kutuları düzenle")) return;
                btnArrangeFrames.IsEnabled = false;
                btnArrangeFrames.Content = "Düzenleniyor...";
                try
                {
                    int count = FrameArranger.ArrangeAllFrames();
                    SmartToast.Show("Kutular düzenlendi", $"Birincil monitörde {count} kutu düzenlendi");
                }
                catch (Exception ex)
                {
                    LogManager.Log(LogManager.LogLevel.Error, LogManager.LogCategory.General,
                        $"Arrange Frames failed: {ex.Message}");
                    MessageBoxesManager.ShowOKOnlyMessageBoxForm($"Kutular düzenlenemedi: {ex.Message}", "Hata");
                }
                finally
                {
                    btnArrangeFrames.Content = "Kutuları düzenle";
                    btnArrangeFrames.IsEnabled = true;
                }
            };
            c.Children.Add(btnArrangeFrames);

            TextBlock infoText = new TextBlock
            {
                Text = "Not: Otomatik düzen, masaüstünüze eklenen yeni uygulama kısayollarını ve programları izler. " +
                       "Her yeni öğe otomatik olarak sınıflandırılır (Verimlilik, Yardımcı programlar, Oyunlar, VR, " +
                       "Geliştirici araçları, Güvenlik uygulamaları, Medya) ve kategori kutusuna taşınır; kutu " +
                       "gerektiğinde otomatik oluşturulur. Sınıflandırılamayan öğelere dokunulmaz. " +
                       "Otomatik düzen açık olsun ya da olmasın, masaüstündeki her şeyi istediğiniz zaman sıralamak için " +
                       "\"Şimdi düzenle\" düğmesini kullanın.",
                FontStyle = FontStyles.Italic,
                Foreground = Brushes.Gray,
                Margin = new Thickness(15, 20, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            c.Children.Add(infoText);

            t.Content = new ScrollViewer { Content = c, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _tabControl.Items.Add(t);
        }

        private static void CreateTextFramesTab()
        {
            TabItem t = new TabItem();
            StackPanel c = new StackPanel();

            CreateSectionHeader(c, "Metin kutuları", ColorTextFrames);
            c.Children.Add(new TextBlock
            {
                Text = "Metin kutuları, duvar kâğıdınızın üzerine canlı sistem bilgisi çizer (BGInfo gibi): {belirteçler} içeren " +
                       "bir şablon, seçtiğiniz yazı tipi, renk ve çizim modu; belirli aralıklarla yenilenir. Diğer tüm kutuların " +
                       "altında durur ve fareyi yok sayar. Taşımak ya da boyutlandırmak için sistem tepsisi menüsünden \"Kutu düzenleme modu\"nu açın.",
                TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray, FontStyle = FontStyles.Italic,
                Margin = new Thickness(15, 0, 15, 12)
            });

            ListBox list = new ListBox { Height = 220, Margin = new Thickness(15, 0, 15, 10), FontFamily = new FontFamily("Segoe UI"), FontSize = 13 };
            void Reload()
            {
                list.Items.Clear();
                foreach (dynamic f in FrameDataManager.FrameData)
                {
                    if (!TextFramemanager.IsTextFrame(f)) continue;
                    list.Items.Add(new ListBoxItem { Content = f.Title?.ToString() ?? "(adsız)", Tag = f.Id?.ToString() });
                }
            }
            Reload();
            c.Children.Add(list);

            string? SelectedId() => (list.SelectedItem as ListBoxItem)?.Tag?.ToString();

            Grid buttons = new Grid { Margin = new Thickness(15, 0, 15, 10), Height = 34 };
            for (int i = 0; i < 3; i++) buttons.ColumnDefinitions.Add(new ColumnDefinition());
            Button bAdd = CreateStyledButton("Metin kutusu ekle", ColorTextFrames); bAdd.Margin = new Thickness(0, 0, 5, 0);
            Button bEdit = CreateStyledButton("Özelleştir...", Color.FromRgb(0, 123, 191)); bEdit.Margin = new Thickness(5, 0, 5, 0);
            Button bRemove = CreateStyledButton("Kaldır", Color.FromRgb(234, 67, 53)); bRemove.Margin = new Thickness(5, 0, 0, 0);
            Grid.SetColumn(bAdd, 0); Grid.SetColumn(bEdit, 1); Grid.SetColumn(bRemove, 2);
            buttons.Children.Add(bAdd); buttons.Children.Add(bEdit); buttons.Children.Add(bRemove);
            c.Children.Add(buttons);

            // Text frames now share the common Customize dialog (text-mode controls only).
            void CustomizeTextFrame(string? id)
            {
                if (id == null) return;
                dynamic? frame = FrameDataManager.FindFrameById(id);
                if (frame == null) return;
                try { new CustomizeFrameFormManager(frame).ShowDialog(); }
                catch (Exception ex) { LogManager.Log(LogManager.LogLevel.Error, LogManager.LogCategory.UI, $"Text frame customize failed: {ex.Message}"); }
            }

            bAdd.Click += (s, e) =>
            {
                var wa = SystemParameters.WorkArea;
                dynamic frame = Framemanager.CreateTextFrame(wa.Left + 40, wa.Top + 40);
                Reload();
                foreach (ListBoxItem item in list.Items) if (item.Tag?.ToString() == frame.Id?.ToString()) list.SelectedItem = item;
                CustomizeTextFrame(frame.Id?.ToString());
                Reload();
            };
            bEdit.Click += (s, e) =>
            {
                CustomizeTextFrame(SelectedId());
                Reload();
            };
            list.MouseDoubleClick += (s, e) => { string? id = SelectedId(); if (id != null) { CustomizeTextFrame(id); Reload(); } };
            bRemove.Click += (s, e) =>
            {
                string? id = SelectedId();
                if (id == null) return;
                if (!MessageBoxesManager.ShowCustomYesNoMessageBox("Bu metin kutusu kaldırılsın mı?", "Kaldır")) return;
                Framemanager.DetachFrame(id);
                Reload();
            };

            t.Content = new ScrollViewer { Content = c, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _tabControl.Items.Add(t);
        }

        private static void CreateLookDeeperTab()
        {
            TabItem t = new TabItem();
            StackPanel c = new StackPanel();
            CreateSectionHeader(c, "Günlük", ColorLookDeeper);
            CheckBox logCb = CreateCheckBoxReturn(c, "Günlüğe kaydetmeyi etkinleştir", "EnableLogging", SettingsManager.IsLogEnabled);
            // Use Click event to ensure it only fires on user interaction, then SaveSettings immediately
            logCb.Click += (s, e) => {
                SettingsManager.IsLogEnabled = logCb.IsChecked == true;
                SettingsManager.SaveSettings(); // Force write to JSON immediately
            };
            Button b = CreateStyledButton("Günlüğü aç", ColorLookDeeper); b.Width = 100; b.Height = 25; b.HorizontalAlignment = HorizontalAlignment.Left;
            b.Click += (s, e) => OpenLogFile();
            c.Children.Add(b);

            CreateSectionHeader(c, "Günlük yapılandırması", ColorLookDeeper);
            CreateLogLevelComboBox(c);
            CreateSectionHeader(c, "Günlük kategorileri", ColorLookDeeper);

            // This method creates checkboxes for all Enums (except Error now)
            CreateLogCategoryCheckBoxes(c);

            t.Content = new ScrollViewer { Content = c, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _tabControl.Items.Add(t);
        }

        // --- Helpers ---
        private static void CreateSectionHeader(StackPanel p, string t, Color c) => p.Children.Add(new TextBlock { Text = t, FontFamily = new FontFamily("Segoe UI"), FontSize = 16, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(c), Margin = new Thickness(0, 10, 0, 15) });
        private static void CreateCheckBox(StackPanel p, string t, string n, bool c) => p.Children.Add(new CheckBox { Name = n, Content = t, IsChecked = c, FontFamily = new FontFamily("Segoe UI"), FontSize = 13, Margin = new Thickness(15, 8, 0, 8) });
        private static CheckBox CreateCheckBoxReturn(StackPanel p, string t, string n, bool c) { var cb = new CheckBox { Name = n, Content = t, IsChecked = c, FontFamily = new FontFamily("Segoe UI"), FontSize = 13, Margin = new Thickness(15, 8, 0, 8) }; p.Children.Add(cb); return cb; }

        // FIX: Added 'max' parameter (defaulting to 100) to fix the Tint sliders while supporting AutoHideTime
        private static void CreateSliderControl(StackPanel p, string l, string n, int v, int max = 100, int min = 1)
        {
            Grid g = new Grid { Margin = new Thickness(15, 5, 0, 5) };

            // --- UI FIX: Increased to 205px to bump the sliders and "Effect" label to the right, adding a nice gap ---
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(205) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            // Slightly widened the third column from 50 to 65 to comfortably fit the NumericTextBox without clipping
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) });

            // UI FIX: Changed HorizontalAlignment to Left so labels sit flush on the left margin
            TextBlock lbl = new TextBlock { Text = l, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 10, 0) };
            Slider sl = new Slider { Name = n, Minimum = min, Maximum = max, Value = v, TickFrequency = 1, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center };

            // --- TRIAL: Replaced TextBlock with interconnected NumericTextBox for micro-adjustments ---
            NumericTextBox nud = new NumericTextBox
            {
                Minimum = min,
                Maximum = max,
                Value = v,
                Width = 55, // Exactly half of the standard width used in CustomizeFrameForm, plus a tiny bit for 3-digit numbers
                Height = 20, // Strict compact height to preserve the original line spacing and avoid blowing up vertical space
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };

            // Two-way binding between Slider and NumericTextBox
            sl.ValueChanged += (s, e) => { if (nud.Value != (int)e.NewValue) nud.Value = (int)e.NewValue; };
            nud.ValueChanged += (s, e) => { if (sl.Value != nud.Value) sl.Value = nud.Value; };

            Grid.SetColumn(lbl, 0); Grid.SetColumn(sl, 1); Grid.SetColumn(nud, 2);
            g.Children.Add(lbl); g.Children.Add(sl); g.Children.Add(nud);
            p.Children.Add(g);
        }

        private static void CreateIconRadioButtonGroup(StackPanel p, string gName, Dictionary<string, int> icons, int sel, string fontFamily = "Segoe UI Symbol")
        {
            StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(15, 5, 0, 15), Tag = gName };
            foreach (var i in icons) sp.Children.Add(new RadioButton { Content = i.Key, Tag = i.Value, GroupName = gName, IsChecked = i.Value == sel, Margin = new Thickness(0, 0, 15, 0), FontSize = 16, FontFamily = new FontFamily(fontFamily) });
            p.Children.Add(sp);
        }

        /// <summary>A small WPF preview of a tray-icon style (light glyph on a dark taskbar swatch).</summary>
        private static FrameworkElement BuildTrayPreview(string style)
        {
            var light = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0xEA, 0xED));
            var dim = new SolidColorBrush(System.Windows.Media.Color.FromArgb(120, 0xE8, 0xEA, 0xED));
            var swatch = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x20, 0x21, 0x24)) };
            var host = new Grid();

            if (style == "Stacked")
            {
                host.Children.Add(new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(3), Background = dim, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(5, 5, 0, 0) });
                host.Children.Add(new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(3), Background = light, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 5, 5) });
            }
            else if (style == "Grid")
            {
                host.Children.Add(new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(4), BorderBrush = light, BorderThickness = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
                var dots = new System.Windows.Controls.Primitives.UniformGrid { Rows = 2, Columns = 2, Width = 12, Height = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                for (int i = 0; i < 4; i++) dots.Children.Add(new System.Windows.Shapes.Rectangle { Width = 3.5, Height = 3.5, RadiusX = 1, RadiusY = 1, Fill = light, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0.5) });
                host.Children.Add(dots);
            }
            else // Nested
            {
                var outer = new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(5), BorderBrush = light, BorderThickness = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                outer.Child = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(2), Background = light, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                host.Children.Add(outer);
            }

            swatch.Child = host;
            return swatch;
        }

        //New Arrangement

        private static void CreateColorAndEffectComboBoxes(StackPanel p, CheckBox chamCb)
        {
            Grid g = new Grid { Margin = new Thickness(15, 10, 0, 10) };

            // --- UI FIX: Hardcode total to 205px (45 + 160) to match the sliders. 
            // The 160px column holds a 140px dropdown, leaving exactly a 20px gap before "Effect" ---
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });

            TextBlock lblColor = new TextBlock { Text = "Renk", FontSize = 13, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 10, 0) };
            // Constrain Width to 140 and Left-align so it doesn't stretch to fill the 160px column, creating the gap automatically
            ComboBox cbColor = new ComboBox { Name = "ColorComboBox", Width = 140, HorizontalAlignment = HorizontalAlignment.Left, Height = 25, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            foreach (string c in new[] { "Gray", "Black", "White", "Beige", "Green", "Purple", "Fuchsia", "Yellow", "Orange", "Red", "Blue", "Bismark", "Transparent" }) cbColor.Items.Add(c);
            UiNames.Translate(cbColor);
            cbColor.SelectedItem = SettingsManager.SelectedColor;

            // --- BUG FIX: Disable Color dropdown if Chameleon mode is ON ---
            cbColor.IsEnabled = chamCb.IsChecked != true;
            chamCb.Click += (s, e) => cbColor.IsEnabled = chamCb.IsChecked != true;
            // ---------------------------------------------------------------

            // UI FIX: Starts perfectly flush at the new 205px mark
            TextBlock lblEffect = new TextBlock { Text = "Efekt", FontSize = 13, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 10, 0) };
            ComboBox cbEffect = new ComboBox { Name = "LaunchEffectComboBox", Width = 140, HorizontalAlignment = HorizontalAlignment.Left, Height = 25, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            foreach (string e in new[] { "Zoom", "Bounce", "FadeOut", "SlideUp", "Rotate", "Agitate", "GrowAndFly", "Pulse", "Elastic", "Flip3D", "Spiral", "Shockwave", "Matrix", "Supernova", "Teleport" }) cbEffect.Items.Add(e);
            UiNames.Translate(cbEffect);
            cbEffect.SelectedIndex = (int)SettingsManager.LaunchEffect;

            Grid.SetColumn(lblColor, 0); g.Children.Add(lblColor);
            Grid.SetColumn(cbColor, 1); g.Children.Add(cbColor);
            Grid.SetColumn(lblEffect, 2); g.Children.Add(lblEffect);
            Grid.SetColumn(cbEffect, 3); g.Children.Add(cbEffect);

            p.Children.Add(g);
        }

        private static Button CreateStyledButton(string t, Color c) => new Button { Content = t, FontFamily = new FontFamily("Segoe UI"), FontSize = 13, FontWeight = FontWeights.Bold, Background = new SolidColorBrush(c), Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };

        private static void CreateLogLevelComboBox(StackPanel p)
        {
            Grid g = new Grid { Margin = new Thickness(0, 10, 0, 10) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            g.Children.Add(new TextBlock { Text = "Min. günlük düzeyi", FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
            ComboBox cb = new ComboBox { Name = "LogLevelComboBox", Height = 25, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            foreach (var l in new[] { "Debug", "Info", "Warn", "Error" }) cb.Items.Add(l);
            UiNames.Translate(cb);
            cb.SelectedItem = SettingsManager.MinLogLevel.ToString();
            Grid.SetColumn(cb, 1); g.Children.Add(cb); p.Children.Add(g);
        }

        // --- Log Categories (Optimized & Fixed) ---
        private static void CreateLogCategoryCheckBoxes(StackPanel p)
        {
            Grid g = new Grid { Name = "LogCategoryGrid" };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            StackPanel l = new StackPanel(); StackPanel r = new StackPanel();

            // FIX: Filter out the "Error" category from UI, as it's a Level, not a Category
            var cats = Enum.GetValues(typeof(LogManager.LogCategory))
                .Cast<LogManager.LogCategory>()
                .Where(c => c != LogManager.LogCategory.Error) // Hide Error
                .ToList();

            int half = (cats.Count + 1) / 2;

            for (int i = 0; i < cats.Count; i++)
            {
                var cb = new CheckBox { Content = UiNames.Display(cats[i].ToString()), Tag = cats[i], IsChecked = SettingsManager.EnabledLogCategories.Contains(cats[i]), FontSize = 13, Margin = new Thickness(15, 8, 0, 8) };
                if (i < half) l.Children.Add(cb); else r.Children.Add(cb);
            }

            Grid.SetColumn(l, 0); Grid.SetColumn(r, 1);
            g.Children.Add(l); g.Children.Add(r);
            p.Children.Add(g);
        }

        // --- APPLYING ---
        // There is no Save/Cancel: every control change is applied (and persisted) immediately.
        // Changes are coalesced through a short timer so slider drags don't re-apply per pixel.
        private static System.Windows.Threading.DispatcherTimer _applyTimer;
        private static bool _hotkeysChangedWhileOpen;

        private static void WireApplyOnChange()
        {
            _hotkeysChangedWhileOpen = false;
            _applyTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _applyTimer.Tick += (s, e) => { _applyTimer.Stop(); ApplyOptions(); };

            // Routed events bubble up from every CheckBox/RadioButton/ComboBox/Slider in the tabs.
            _optionsWindow.AddHandler(System.Windows.Controls.Primitives.ToggleButton.ClickEvent, new RoutedEventHandler((s, e) => ScheduleApply()));
            _optionsWindow.AddHandler(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent, new SelectionChangedEventHandler((s, e) => { if (e.OriginalSource is ComboBox) ScheduleApply(); }));
            _optionsWindow.AddHandler(System.Windows.Controls.Primitives.RangeBase.ValueChangedEvent, new RoutedPropertyChangedEventHandler<double>((s, e) => { if (e.OriginalSource is Slider) ScheduleApply(); }));
        }

        private static void ScheduleApply()
        {
            if (_applyTimer == null) return;
            _applyTimer.Stop();
            _applyTimer.Start();
        }

        private static void FlushPendingApply()
        {
            if (_applyTimer == null) return;
            bool pending = _applyTimer.IsEnabled;
            _applyTimer.Stop();
            _applyTimer = null;
            if (pending) ApplyOptions();
        }

        private static void ApplyOptions()
        {
            if (_tabControl == null) return;
            try
            {
                bool tempPortalImageState = SettingsManager.ShowBackgroundImageOnPortalFrames;
                bool newPortalWatermarkState = false;
                bool newShowInTrayState = false;

                // 1. General
                var generalContent = (StackPanel)((ScrollViewer)((TabItem)_tabControl.Items[0]).Content).Content;
                foreach (var child in generalContent.Children)
                {
                    if (child is CheckBox cb)
                    {
                        if (cb.Name == "StartWithWindows" && cb.IsChecked != TrayManager.IsStartWithWindows) TrayManager.Instance?.ToggleStartWithWindows(cb.IsChecked == true);
                        if (cb.Name == "SingleClickToLaunch") SettingsManager.SingleClickToLaunch = cb.IsChecked == true;
                        if (cb.Name == "EnableSnapNearFrames") SettingsManager.IsSnapEnabled = cb.IsChecked == true;
                        if (cb.Name == "KeepFramedItemsOnDesktop")
                        {
                            SettingsManager.KeepFramedItemsOnDesktop = cb.IsChecked == true;
                            DesktopMigration.StartIfNeeded(); // switched on: move stored items out now
                        }
                        if (cb.Name == "UseRecycleBin") SettingsManager.UseRecycleBin = cb.IsChecked == true;
                        if (cb.Name == "EnableTrayIcon")
                        {
                            newShowInTrayState = cb.IsChecked == true;
                            SettingsManager.ShowInTray = newShowInTrayState;
                            if (TrayManager.Instance != null) TrayManager.Instance.GetType().GetField("Showintray", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(TrayManager.Instance, newShowInTrayState);
                        }

                        // NEW: Context Menu Registry Update
                        if (cb.Name == "EnableContextMenu")
                        {
                            bool newState = cb.IsChecked == true;
                            if (SettingsManager.EnableContextMenu != newState)
                            {
                                SettingsManager.EnableContextMenu = newState;
                                RegistryHelper.ToggleContextMenu(newState);
                            }
                        }

                        // Moved from Style Tab (Choices)
                        if (cb.Name == "EnablePortalWatermark") { newPortalWatermarkState = cb.IsChecked == true; SettingsManager.ShowBackgroundImageOnPortalFrames = newPortalWatermarkState; }
                        if (cb.Name == "DisableFrameScrollbars") SettingsManager.DisableFrameScrollbars = cb.IsChecked == true;
                    }
                }


                // 2. Style
                var styleContent = (StackPanel)((ScrollViewer)((TabItem)_tabControl.Items[1]).Content).Content;
                foreach (var child in styleContent.Children)
                {
                    if (child is CheckBox cb)
                    {
                        if (cb.Name == "EnableChameleon") SettingsManager.EnableChameleonMode = cb.IsChecked == true;
                        // NEW: Auto-Hide & Fade Options (Moved from General)
                        if (cb.Name == "AutoHideFrames") { SettingsManager.AutoHideFrames = cb.IsChecked == true; Framemanager.ResetAutoHideTimer(); }
                        if (cb.Name == "FramesFadeOutFx") SettingsManager.FramesFadeOutFx = cb.IsChecked == true;

                        // NEW: Desktop Icon Visibility
                        if (cb.Name == "HideDesktopElementsOnStart")
                        {
                            bool hideOnStart = cb.IsChecked == true;
                            if (SettingsManager.HideDesktopElementsOnStart != hideOnStart)
                            {
                                SettingsManager.HideDesktopElementsOnStart = hideOnStart;
                                DesktopIconManager.SetDesktopIconsVisible(!hideOnStart);
                            }
                        }
                        if (cb.Name == "HideDesktopElementsOnAllFramesHide") SettingsManager.HideDesktopElementsOnAllFramesHide = cb.IsChecked == true;
                        if (cb.Name == "ToggleDesktopIconsOnDoubleClick") SettingsManager.ToggleDesktopIconsOnDoubleClick = cb.IsChecked == true;

                        // NEW: Frames behavior
                        if (cb.Name == "EnableToggleFramesHotkey") SettingsManager.EnableToggleFramesHotkey = cb.IsChecked == true;
                        if (cb.Name == "PortalDetailsStriped") SettingsManager.PortalDetailsStriped = cb.IsChecked == true;
                    }
                    else if (child is Grid g)
                    {
                        var tint = g.Children.OfType<Slider>().FirstOrDefault(s => s.Name == "TintSlider"); if (tint != null) SettingsManager.TintValue = (int)tint.Value; var mtint = g.Children.OfType<Slider>().FirstOrDefault(s => s.Name == "MenuTintSlider"); if (mtint != null) SettingsManager.MenuTintValue = (int)mtint.Value;
                        var col = g.Children.OfType<ComboBox>().FirstOrDefault(c => c.Name == "ColorComboBox"); if (col?.SelectedItem != null) SettingsManager.SelectedColor = col.SelectedItem.ToString();
                        var eff = g.Children.OfType<ComboBox>().FirstOrDefault(c => c.Name == "LaunchEffectComboBox"); if (eff != null) SettingsManager.LaunchEffect = (LaunchEffectsManager.LaunchEffect)eff.SelectedIndex;
                        var imgMode = g.Children.OfType<ComboBox>().FirstOrDefault(c => c.Name == "ImageDropModeComboBox"); if (imgMode?.SelectedItem is ComboBoxItem imi && imi.Tag != null) SettingsManager.ImageDropMode = imi.Tag.ToString();

                        // Parse Sliders (Moved from General)
                        var autoHideTime = g.Children.OfType<Slider>().FirstOrDefault(s => s.Name == "AutoHideTimeSlider"); if (autoHideTime != null) { SettingsManager.AutoHideTime = (int)autoHideTime.Value; Framemanager.ResetAutoHideTimer(); }
                        var fadeOutTime = g.Children.OfType<Slider>().FirstOrDefault(s => s.Name == "FadeOutTimeSlider"); if (fadeOutTime != null) SettingsManager.FadeOutTime = (int)fadeOutTime.Value;
                        var fadeOutAlpha = g.Children.OfType<Slider>().FirstOrDefault(s => s.Name == "FadeOutAlphaSlider"); if (fadeOutAlpha != null) SettingsManager.FadeOutFxTargetAlpha = fadeOutAlpha.Value / 100.0;
                        var fadeWake = g.Children.OfType<Slider>().FirstOrDefault(s => s.Name == "FadeWakeDelaySlider"); if (fadeWake != null) SettingsManager.FadeWakeDelayMs = (int)fadeWake.Value;
                        var autoRollTime = g.Children.OfType<Slider>().FirstOrDefault(s => s.Name == "AutoRollTimeSlider"); if (autoRollTime != null) SettingsManager.AutoRollTime = (int)autoRollTime.Value;

                        // Parse Icons from the new side-by-side nested Grid layout
                        foreach (var innerChild in g.Children.OfType<StackPanel>())
                        {
                            foreach (var rbSp in innerChild.Children.OfType<StackPanel>())
                            {
                                if (rbSp.Tag?.ToString() == "MenuIconGroup") foreach (RadioButton rb in rbSp.Children.OfType<RadioButton>()) if (rb.IsChecked == true) SettingsManager.MenuIcon = (int)rb.Tag;
                                if (rbSp.Tag?.ToString() == "LockIconGroup") foreach (RadioButton rb in rbSp.Children.OfType<RadioButton>()) if (rb.IsChecked == true) SettingsManager.LockIcon = (int)rb.Tag;
                                if (rbSp.Tag?.ToString() == "FilterIconGroup") foreach (RadioButton rb in rbSp.Children.OfType<RadioButton>()) if (rb.IsChecked == true) SettingsManager.FilterIcon = (int)rb.Tag;
                                if (rbSp.Tag?.ToString() == "TrayIconGroup") foreach (RadioButton rb in rbSp.Children.OfType<RadioButton>()) if (rb.IsChecked == true) SettingsManager.TrayIconStyle = (string)rb.Tag;
                            }
                        }
                    }
                }

                // 3. Tools
                var toolsContent = (StackPanel)((TabItem)_tabControl.Items[2]).Content;
                foreach (var child in toolsContent.Children)
                {
                    if (child is CheckBox cb && cb.Name == "EnableAutoBackup") SettingsManager.EnableAutoBackup = cb.IsChecked == true;
                    else if (child is CheckBox dlm && dlm.Name == "EnableDisplayLayoutMemory") SettingsManager.EnableDisplayLayoutMemory = dlm.IsChecked == true;
                    else if (child is Grid toolsGrid)
                    {
                        var cntBox = toolsGrid.Children.OfType<TextBox>().FirstOrDefault(t => t.Name == "MaxBackupCountBox");
                        if (cntBox != null && int.TryParse(cntBox.Text, out int cnt))
                            SettingsManager.MaxBackupCount = Math.Max(1, Math.Min(999, cnt));
                        var sizeBox = toolsGrid.Children.OfType<TextBox>().FirstOrDefault(t => t.Name == "MaxBackupSizeBox");
                        if (sizeBox != null && int.TryParse(sizeBox.Text, out int mb))
                            SettingsManager.MaxBackupSizeMB = Math.Max(1, Math.Min(100000, mb));
                    }
                }

                // 4. Hotkeys (NEW)
                var hotkeysContent = (StackPanel)((ScrollViewer)((TabItem)_tabControl.Items[4]).Content).Content;
                bool hotkeysChanged = false;
                foreach (var child in hotkeysContent.Children)
                {
                    if (child is CheckBox hotkeyCb)
                    {
                        if (hotkeyCb.Name == "EnableProfileHotkeys" && SettingsManager.EnableProfileHotkeys != (hotkeyCb.IsChecked == true)) { SettingsManager.EnableProfileHotkeys = hotkeyCb.IsChecked == true; hotkeysChanged = true; }
                    }

                    if (child is Grid g && g.Children.Count > 1 && g.Children[1] is StackPanel spMods)
                    {
                        string prefix = "";
                        foreach (var elem in spMods.Children)
                        {
                            if (elem is CheckBox cb && cb.Name.EndsWith("Ctrl"))
                            {
                                prefix = cb.Name.Substring(0, cb.Name.Length - 4);
                                break;
                            }
                        }
                        if (!string.IsNullOrEmpty(prefix))
                        {
                            List<string> mods = new List<string>();
                            int key = 0;
                            foreach (var elem in spMods.Children)
                            {
                                if (elem is CheckBox cb && cb.IsChecked == true)
                                {
                                    if (cb.Name.EndsWith("Ctrl")) mods.Add("Control");
                                    else if (cb.Name.EndsWith("Alt")) mods.Add("Alt");
                                    else if (cb.Name.EndsWith("Shift")) mods.Add("Shift");
                                    else if (cb.Name.EndsWith("Win")) mods.Add("Win");
                                }
                                if (elem is ComboBox cmb && cmb.SelectedItem is ComboBoxItem item && item.Tag is int val)
                                {
                                    key = val;
                                }
                            }
                            string modString = string.Join(", ", mods);

                            if (prefix == "ProfSwitch") { if (SettingsManager.ProfileSwitchModifier != modString) { SettingsManager.ProfileSwitchModifier = modString; hotkeysChanged = true; } }
                            if (prefix == "ProfPrev") { if (SettingsManager.ProfilePrevModifier != modString || SettingsManager.ProfilePrevKey != key) { SettingsManager.ProfilePrevModifier = modString; SettingsManager.ProfilePrevKey = key; hotkeysChanged = true; } }
                            if (prefix == "ProfNext") { if (SettingsManager.ProfileNextModifier != modString || SettingsManager.ProfileNextKey != key) { SettingsManager.ProfileNextModifier = modString; SettingsManager.ProfileNextKey = key; hotkeysChanged = true; } }
                        }
                    }
                }

                if (hotkeysChanged)
                {
                    // Propagate the new hotkeys across all existing profiles; the restart notice is shown once on close.
                    SettingsManager.BroadcastHotkeysToAllProfiles();
                    _hotkeysChangedWhileOpen = true;
                }

                // 5. Smart Desktop (Auto-Organize)
                var smartDesktopContent = (StackPanel)((ScrollViewer)((TabItem)_tabControl.Items[5]).Content).Content;
                foreach (var child in smartDesktopContent.Children)
                {
                    if (child is CheckBox cb && cb.Name == "EnableAutoOrganize")
                    {
                        bool wasEnabled = SettingsManager.EnableAutoOrganize;
                        SettingsManager.EnableAutoOrganize = cb.IsChecked == true;

                        // Sync with the Tray icon context menu!
                        TrayManager.Instance?.UpdateAutoOrganizeMenuCheck(SettingsManager.EnableAutoOrganize);

                        // Live toggle the background engine
                        if (!wasEnabled && SettingsManager.EnableAutoOrganize) AutoOrganizeManager.Start();
                        else if (wasEnabled && !SettingsManager.EnableAutoOrganize) AutoOrganizeManager.Stop();
                    }
                    if (child is CheckBox cbn && cbn.Name == "EnableAutoOrganizeNotifications")
                    {
                        SettingsManager.EnableAutoOrganizeNotifications = cbn.IsChecked == true;
                    }
                    if (child is CheckBox cbo && cbo.Name == "EnableOnlineCategoryLookup")
                    {
                        SettingsManager.EnableOnlineCategoryLookup = cbo.IsChecked == true;
                    }
                }

                // 6. Look Deeper (Logs) - Index shifted to 6
                var logContent = (StackPanel)((ScrollViewer)((TabItem)_tabControl.Items[6]).Content).Content;
                var newEnabledCategories = new List<LogManager.LogCategory>();

                // FIX: Force enable the hidden "Error" category so existing log calls don't break.
                // It will only be filtered by the "Minimum Log Level" dropdown now.
                newEnabledCategories.Add(LogManager.LogCategory.Error);

                foreach (var child in logContent.Children)
                {
                    if (child is CheckBox cb)
                    {
                        if (cb.Name == "EnableLogging") SettingsManager.IsLogEnabled = cb.IsChecked == true;
                    }
                    else if (child is Grid g)
                    {
                        var lvl = g.Children.OfType<ComboBox>().FirstOrDefault(c => c.Name == "LogLevelComboBox");
                        if (lvl?.SelectedItem != null && Enum.TryParse<LogManager.LogLevel>(lvl.SelectedItem.ToString(), out var ll)) SettingsManager.SetMinLogLevel(ll);

                        if (g.Name == "LogCategoryGrid")
                        {
                            foreach (var stack in g.Children.OfType<StackPanel>())
                            {
                                // FIX: Renamed inner variable 'catBox' to prevent conflict
                                foreach (var catBox in stack.Children.OfType<CheckBox>())
                                {
                                    if (catBox.IsChecked == true && catBox.Tag is LogManager.LogCategory cat)
                                    {
                                        newEnabledCategories.Add(cat);
                                        // Sync the boolean for background validation
                                        if (cat == LogManager.LogCategory.BackgroundValidation)
                                            SettingsManager.EnableBackgroundValidationLogging = true;
                                    }
                                }
                            }
                        }
                    }
                }




                if (!newEnabledCategories.Contains(LogManager.LogCategory.BackgroundValidation))
                    SettingsManager.EnableBackgroundValidationLogging = false;

                SettingsManager.SetEnabledLogCategories(newEnabledCategories);
                SettingsManager.SaveSettings();
                LogManager.Log(LogManager.LogLevel.Info, LogManager.LogCategory.Settings, "Options applied");

                if (tempPortalImageState != newPortalWatermarkState) _ = TrayManager.reloadallFrames(); // fire-and-forget reload
                TrayManager.Instance?.UpdateTrayIcon();
                WallpaperColorManager.CheckForWallpaperChange(); // wallpaper may have changed while Chameleon was off
                Utility.UpdateFrameVisuals();
                Framemanager.RefreshAllPortalDetails(); // apply global striping change to open portals

				// --- NEW: Broadcast Idle Fade-Out Settings to all active frames ---
				var allFrames = System.Windows.Application.Current.Windows.OfType<NonActivatingWindow>();
                foreach (var frame in allFrames)
                {
					frame.RefreshIdleSettings();
                }

                // --- NEW: Broadcast Auto-Roll Settings ---
                Framemanager.RefreshAutoRollSettings();

                // --- NEW: Broadcast Scrollbar Settings ---
                Framemanager.RefreshScrollbarSettings();
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, LogManager.LogCategory.Settings, $"Error applying options: {ex.Message}");
                MessageBoxesManager.ShowOKOnlyMessageBoxForm($"Hata: {ex.Message}", "Seçenekler hatası");
            }
        }

        private static void RestoreBackup()
        {
            try
            {
                // Backups are zip archives now. Legacy plain-folder backups still restore:
                // switch the filter to "All files" and pick any file inside the old backup
                // folder (e.g. its frames.json) — RestoreFromBackup uses its parent folder.
                var d = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Yedek arşivleri (*.zip)|*.zip|Tüm dosyalar (*.*)|*.*",
                    DefaultExt = ".zip",
                    InitialDirectory = BackupManager.GetBackupsFolderPath(),
                    Title = "Geri yüklenecek yedeği seçin",
                    RestoreDirectory = true
                };

                if (d.ShowDialog() == true)
                {
                    BackupManager.RestoreFromBackup(d.FileName);
                    _optionsWindow.Close();
                    _ = TrayManager.reloadallFrames(); // fire-and-forget reload
                }
            }
            catch (Exception ex)
            {
                MessageBoxesManager.ShowOKOnlyMessageBoxForm($"Geri yükleme başarısız: {ex.Message}", "Hata");
            }
        }
        private static void OpenBackupsFolder()
        {
            // Use the centralized BackupManager helper
            BackupManager.OpenBackupsFolder();
        }

        private static void OpenLogFile()
        {
            try
            {
                string p = System.IO.Path.Combine(AppPaths.DataRoot, "Desktop_Frames.log");
                if (System.IO.File.Exists(p)) Process.Start(new ProcessStartInfo { FileName = p, UseShellExecute = true });
                else MessageBoxesManager.ShowOKOnlyMessageBoxForm("Günlük dosyası bulunamadı.", "Bilgi");
            }
            catch { }
        }

        private static void PerformFullFactoryReset()
        {
            if (MessageBoxesManager.ShowCustomYesNoMessageBox(
                "Bu işlem GEÇERLİ PROFİLİN TÜM kutularını ve ayarlarını kaldırır.\n\n" +
                "Kutularda saklanan dosyalar ve kısayollar SİLİNMEZ; önce masaüstünüze geri " +
                "taşınır. Ayrıca güvenlik amaçlı bir yedek oluşturulur.\n\nDevam edilsin mi?", "Fabrika ayarlarına sıfırla"))
            {
                // KISS: Hijack cursor to show processing
                System.Windows.Application.Current?.Dispatcher.Invoke(() => System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait);
                try
                {
                    // 1. Create a safety backup before clearing
                    string ts = DateTime.Now.ToString("yyMMddHHmm");
                    BackupManager.CreateBackup($"{ts}_backup_reset", silent: true);

                    // 2. Dump every stored file back to the Desktop — the app never mass-deletes
                    //    user files; if the user wants them gone they do it themselves, outside
                    //    the app. Covers the frame store and the legacy per-profile Shortcuts
                    //    folder; failures leave the file in place and are logged.
                    int moved = FrameFileOperations.MoveProfileFilesToDesktop(ProfileManager.CurrentProfileName);

                    string legacyShortcuts = ProfileManager.GetProfileFilePath("Shortcuts");
                    if (System.IO.Directory.Exists(legacyShortcuts))
                    {
                        foreach (string entry in System.IO.Directory.GetFileSystemEntries(legacyShortcuts))
                        {
                            try
                            {
                                FrameStore.MoveIntoFolder(FrameFileOperations.DesktopDir, entry, copy: false);
                                moved++;
                            }
                            catch (Exception mvEx)
                            {
                                LogManager.Log(LogManager.LogLevel.Warn, LogManager.LogCategory.Error,
                                    $"Factory reset: could not move '{entry}' to the desktop: {mvEx.Message}");
                            }
                        }
                    }

                    // 3. Wipe app-internal folders only (caches and the last-deleted stash —
                    //    never user content, which was moved out above).
                    var failedFolders = new List<string>();
                    foreach (string f in new[] { "Temp Shortcuts", "Last Frame Deleted", "CopiedItem" })
                    {
                        string p = ProfileManager.GetProfileFilePath(f);
                        if (System.IO.Directory.Exists(p))
                        {
                            try
                            {
                                System.IO.Directory.Delete(p, true);
                                System.IO.Directory.CreateDirectory(p); // Recreate empty folder
                            }
                            catch (Exception dirEx)
                            {
                                failedFolders.Add(f);
                                LogManager.Log(LogManager.LogLevel.Error, LogManager.LogCategory.Error,
                                    $"Factory reset: failed to wipe folder '{f}' ({p}): {dirEx.Message}");
                            }
                        }
                    }

                    // 4. Wipe Profile-Specific Config Files (OVERWRITE INSTEAD OF DELETE)
                    // FIX: Pointed to frames.json and wrote empty array to prevent read crashes
                    string fj = ProfileManager.GetProfileFilePath("frames.json");
                    System.IO.File.WriteAllText(fj, "[]");

                    string oj = ProfileManager.GetProfileFilePath("options.json");
                    System.IO.File.WriteAllText(oj, "{}");

                    // 5. Force a clean OS-level restart (Guarantees all UI clears properly)
                    // Surface any folders that could not be wiped instead of pretending success.
                    string movedNote = moved > 0 ? $"\nSaklanan {moved} dosya masaüstünüze taşındı." : "";
                    string resetMessage = failedFolders.Count == 0
                        ? $"Fabrika ayarlarına sıfırlama tamamlandı.{movedNote}\nUygulama şimdi yeniden başlatılacak."
                        : $"Fabrika ayarlarına sıfırlama uyarılarla tamamlandı.{movedNote}\nTamamen temizlenemedi: {string.Join(", ", failedFolders)}.\nAyrıntılar için günlüğe bakın. Uygulama şimdi yeniden başlatılacak.";
                    MessageBoxesManager.ShowOKOnlyMessageBoxForm(resetMessage, failedFolders.Count == 0 ? "Sıfırlama başarılı" : "Sıfırlama tamamlanamadı");

                    string appPath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c ping 127.0.0.1 -n 3 > nul & start \"\" \"{appPath}\"",
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });

                    // Instantly kill current process
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                }
                catch (Exception ex)
                {
                    LogManager.Log(LogManager.LogLevel.Error, LogManager.LogCategory.Error, $"Factory reset failed: {ex.Message}");
                    MessageBoxesManager.ShowOKOnlyMessageBoxForm($"Sıfırlama başarısız: {ex.Message}", "Hata");
                }
                finally
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() => System.Windows.Input.Mouse.OverrideCursor = null);
                }
            }
        }
    }
}