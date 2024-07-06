﻿using GGDeals.Api.Services;
using GGDeals.Menu.AddGames.MVVM;
using GGDeals.Menu.Failures;
using GGDeals.Menu.Failures.File;
using GGDeals.Menu.Failures.MVVM;
using GGDeals.Services;
using GGDeals.Settings;
using GGDeals.Settings.MVVM;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GGDeals.Queue;

namespace GGDeals
{
	public class GGDeals : GenericPlugin
	{
		private const string FailuresFileName = "failures.json";
		private const string QueueFileName = "queue.json";
		private static readonly ILogger Logger = LogManager.GetLogger();

		private readonly IPlayniteAPI _api;
		private readonly StartupSettingsValidator _startupSettingsValidator;
		private readonly AddFailuresManager _addFailuresManager;
		private readonly PersistentProcessingQueue _persistentProcessingQueue;

		private readonly JsonSerializerSettings _apiJsonSerializerSettings = new JsonSerializerSettings
		{
			ContractResolver = new DefaultContractResolver
			{
				NamingStrategy = new SnakeCaseNamingStrategy()
			},
		};

		private GGDealsSettingsViewModel _settings;

		public override Guid Id { get; } = Guid.Parse("2af05ded-085c-426b-a10e-8e03185092bf");

		public GGDeals(IPlayniteAPI api) : base(api)
		{
			_api = api;
			var failuresFilePath = Path.Combine(GetPluginUserDataPath(), FailuresFileName);
			var addFailuresFileService = new AddFailuresFileService(failuresFilePath);
			_addFailuresManager = new AddFailuresManager(addFailuresFileService);
			_persistentProcessingQueue = new PersistentProcessingQueue(
				new QueuePersistence(Path.Combine(GetPluginUserDataPath(), QueueFileName)),
				AddGamesToGGCollectionAsync);

			Properties = new GenericPluginProperties
			{
				HasSettings = true
			};

			PlayniteApi.Database.Games.ItemCollectionChanged += async (_, gamesAddedArgs) =>
			{
				await _persistentProcessingQueue.Enqueue(gamesAddedArgs.AddedItems.Select(x => x.Id).ToList());
			};

			var pluginSettingsPersistence = new PluginSettingsPersistence(this);
			_startupSettingsValidator = new StartupSettingsValidator(pluginSettingsPersistence, new SettingsMigrator(pluginSettingsPersistence));
		}

		public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
		{
			_startupSettingsValidator.EnsureCorrectVersionSettingsExist();
		}

		public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
		{
			_persistentProcessingQueue.ProcessInBackground();
		}

		public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
		{
			yield return new GameMenuItem
			{
				Description = ResourceProvider.GetString("LOC_GGDeals_GameMenuItemAddToGGDealsCollection"),
				Action = actionArgs => { AddGamesToGGCollection(actionArgs.Games); }
			};
		}

		public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
		{
			yield return new MainMenuItem
			{
				Description = ResourceProvider.GetString("LOC_GGDeals_MainMenuItemAddToGGDealsCollection"),
				MenuSection = "@GG.deals",
				Action = actionArgs =>
				{
					var addGamesViewModel = new AddGamesViewModel(this);
					ShowDialog(
						new AddGamesView(addGamesViewModel),
						150,
						500,
						ResourceProvider.GetString("LOC_GGDeals_AddGamesTitle"),
						false);
				}
			};

			yield return new MainMenuItem
			{
				Description = ResourceProvider.GetString("LOC_GGDeals_MainMenuItemShowFailures"),
				MenuSection = "@GG.deals",
				Action = actionArgs =>
				{
					var showAddFailuresViewModel = new ShowAddFailuresViewModel(this, _addFailuresManager);
					ShowDialog(
						new ShowAddFailuresView(showAddFailuresViewModel),
						768,
						768,
						ResourceProvider.GetString("LOC_GGDeals_ShowAddFailuresTitle"),
						true);
				}
			};
		}

		public override ISettings GetSettings(bool firstRunSettings)
		{
			return _settings ?? (_settings = new GGDealsSettingsViewModel(this));
		}

		public override UserControl GetSettingsView(bool firstRunSettings)
		{
			return new GGDealsSettingsView(PlayniteApi);
		}

		public void AddGamesToGGCollection(IReadOnlyCollection<Game> games)
		{
			Task.Run(async () =>
			{
				await AddGamesToGGCollectionAsync(games.Select(x => x.Id).ToList());
			});
		}

		public async Task AddGamesToGGCollectionAsync(IReadOnlyCollection<Guid> gameIds)
		{
			if (!gameIds.Any())
			{
				return;
			}

			try
			{
				var games = _api.Database.Games.Where(x => gameIds.Contains(x.Id)).ToList();
				var settings = LoadPluginSettings<GGDealsSettings>();
				using (var ggDealsApiClient = new GGDealsApiClient(settings, _apiJsonSerializerSettings))
				{
					var gameToAddFilter = new GameToAddFilter(settings);
					var libraryToGGLauncherMap = new LibraryToGGLauncherMap();
					var gameToGameWithLauncherConverter = new GameToGameWithLauncherConverter(libraryToGGLauncherMap);
					var requestDataBatcher = new RequestDataBatcher(_apiJsonSerializerSettings);

					var addGamesService = new AddGamesService(settings, gameToAddFilter,
						gameToGameWithLauncherConverter, requestDataBatcher, ggDealsApiClient);

					var ggDealsService =
						new GGDealsService(settings, PlayniteApi, addGamesService, _addFailuresManager);
					// TODO: Implement cancellation token source
					await ggDealsService.AddGamesToLibrary(games, CancellationToken.None);
				}
			}
			catch (Exception ex)
			{
				Logger.Error(ex, "Failed to add games to GG.deals collection.");
			}
		}

		private void ShowDialog(UserControl view, double height, double width, string title, bool showMaximizeButton)
		{
			var window = _api.Dialogs.CreateWindow(new WindowCreationOptions()
			{
				ShowCloseButton = true,
				ShowMaximizeButton = showMaximizeButton,
				ShowMinimizeButton = false,
			});

			window.Height = height;
			window.Width = width;
			window.Title = title;

			window.Content = view;
			if (view.DataContext is IViewModelForWindow viewModelForWindow)
			{
				viewModelForWindow.AssociateWindow(window);
			}

			window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
			window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

			window.ShowDialog();
		}
	}
}