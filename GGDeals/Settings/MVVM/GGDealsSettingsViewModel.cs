﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using GGDeals.Website.Url;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace GGDeals.Settings.MVVM
{
    public class GGDealsSettingsViewModel : ObservableObject, ISettings
    {
        private readonly ILogger _logger = LogManager.GetLogger();
        private readonly GGDeals _plugin;
        private GGDealsSettings _settings;
        private GGDealsSettings _editingClone;

        public GGDealsSettingsViewModel(GGDeals plugin)
        {
            _plugin = plugin;

            var savedSettings = plugin.LoadPluginSettings<GGDealsSettings>();
            Settings = savedSettings ?? new GGDealsSettings();
        }

        public GGDealsSettings Settings
        {
            get => _settings;
            set
            {
                _settings = value;
                OnPropertyChanged();
            }
        }

        public void BeginEdit()
        {
            _editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            Settings = _editingClone;
        }

        public void EndEdit()
        {
            _plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            // Code execute when user decides to confirm changes made since BeginEdit was called.
            // Executed before EndEdit is called and EndEdit is not called if false is returned.
            // List of errors is presented to user if verification fails.
            errors = new List<string>();
            return true;
        }

        public ICommand Authenticate => new RelayCommand(() =>
        {
            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                var homePageResolver = new HomePageResolver();
                var homePage = homePageResolver.Resolve();
                using (var webView = _plugin.PlayniteApi.WebViews.CreateView(520, 670))
                {
                    webView.LoadingChanged += (s, e) =>
                    {
                        if (e.IsLoading == false)
                        {
                        }
                    };
                    webView.Navigate(homePage);
                    webView.OpenDialog();
                }
            }));
        });
    }
}