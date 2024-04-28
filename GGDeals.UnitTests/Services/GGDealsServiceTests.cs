﻿using System;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Threading.Tasks;
using AutoFixture.Xunit2;
using GGDeals.Services;
using GGDeals.Website;
using Moq;
using Playnite.SDK;
using Playnite.SDK.Models;
using Xunit;

namespace GGDeals.UnitTests.Services
{
    public class GGDealsServiceTests
    {
        [Theory]
        [AutoMoqData]
        public async Task AddGamesToLibrary_ShowsInfoNotification_WhenCheckLoginThrowsException(
            [Frozen] Mock<IGGWebsite> ggWebsiteMock,
            [Frozen] Mock<INotificationsAPI> notificationsApiMock,
            [Frozen] Mock<IPlayniteAPI> playniteApiMock,
            AuthenticationException exception,
            List<Game> games,
            GGDealsService sut)
        {
            // Arrange
            ggWebsiteMock.Setup(x => x.CheckLoggedIn()).ThrowsAsync(exception);
            playniteApiMock.Setup(x => x.Notifications).Returns(notificationsApiMock.Object);

            // Act
            await sut.AddGamesToLibrary(games);

            // Assert
            notificationsApiMock.Verify(
                x => x.Add("gg-deals-auth-error", It.IsAny<string>(),
                    It.Is<NotificationType>(n => n == NotificationType.Info)), Times.Once);
        }

        [Theory]
        [AutoMoqData]
        public async Task AddGamesToLibrary_ShowsErrorNotification_WhenTryAddToCollectionThrowsException(
            [Frozen] Mock<IAddAGameService> addAGameServiceMock,
            [Frozen] Mock<INotificationsAPI> notificationsApiMock,
            [Frozen] Mock<IPlayniteAPI> playniteApiMock,
            Exception exception,
            List<Game> games,
            GGDealsService sut)
        {
            // Arrange
            addAGameServiceMock.Setup(x => x.TryAddToCollection(It.IsAny<Game>())).ThrowsAsync(exception);
            playniteApiMock.Setup(x => x.Notifications).Returns(notificationsApiMock.Object);

            // Act
            await sut.AddGamesToLibrary(games);

            // Assert
            notificationsApiMock.Verify(
                x => x.Add("gg-deals-generic-error", It.IsAny<string>(),
                    It.Is<NotificationType>(n => n == NotificationType.Error)), Times.Once);
        }

        [Theory]
        [AutoMoqData]
        public async Task AddGamesToLibrary_ShowsInfoNotification_WhenGamePageCouldNotBeFound(
            [Frozen] Mock<IAddAGameService> addAGameServiceMock,
            [Frozen] Mock<INotificationsAPI> notificationsApiMock,
            [Frozen] Mock<IPlayniteAPI> playniteApiMock,
            List<Game> games,
            GGDealsService sut)
        {
            // Arrange
            addAGameServiceMock.Setup(x => x.TryAddToCollection(It.IsAny<Game>())).ReturnsAsync(false);
            playniteApiMock.Setup(x => x.Notifications).Returns(notificationsApiMock.Object);

            // Act
            await sut.AddGamesToLibrary(games);

            // Assert
            notificationsApiMock.Verify(
                x => x.Add("gg-deals-gamepagenotfound", It.IsAny<string>(),
                    It.Is<NotificationType>(n => n == NotificationType.Info)), Times.Once);
        }
    }
}