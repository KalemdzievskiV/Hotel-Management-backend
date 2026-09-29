using System.Net;
using FluentAssertions;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Enums;
using HotelManagement.Tests.Helpers;
using Xunit;

namespace HotelManagement.Tests.Integration;

/// <summary>
/// Who hears about what: guests' bookings reach the hotel, the hotel's decisions reach the
/// guest, tasks reach the housekeeper. Every notification is in the in-app list; a push goes to
/// the user's phones unless they turned that type off.
/// </summary>
public class NotificationsIntegrationTests : IClassFixture<PushRecordingWebApplicationFactory>
{
    private readonly TestApi _api;
    private readonly RecordingPushQueue _pushes;

    public NotificationsIntegrationTests(PushRecordingWebApplicationFactory factory)
    {
        _api = new TestApi(factory.CreateClient());
        _pushes = factory.Pushes;
    }

    private static string NewPushToken() => $"ExponentPushToken[{Guid.NewGuid():N}]";

    private async Task<string> RegisterPhoneAsync(string userToken)
    {
        var pushToken = NewPushToken();
        (await _api.SendAsync(HttpMethod.Post, "/api/Notifications/devices", userToken, new RegisterDeviceDto
        {
            Token = pushToken,
            Platform = "android"
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        return pushToken;
    }

    private Task<NotificationPageDto> ListAsync(string token) =>
        _api.GetAsync<NotificationPageDto>("/api/Notifications", token);

    private async Task<(string Token, string UserId)> StaffAsync(string role, int hotelId)
    {
        var email = $"{role.ToLowerInvariant()}{Guid.NewGuid():N}@test.com";
        var token = await TestAuth.GetTokenAsync(_api.Client, role, email, hotelId);
        return (token, await _api.GetUserIdAsync(email));
    }

    private Task<ReservationDto> GuestBooksAsync(TestApi.Hotel hotel, string guestToken, int startsInDays = 10) =>
        _api.PostAsync<ReservationDto>("/api/Reservations", guestToken, new CreateReservationDto
        {
            HotelId = hotel.HotelId,
            RoomId = hotel.RoomId,
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.Date.AddDays(startsInDays),
            CheckOutDate = DateTime.UtcNow.Date.AddDays(startsInDays + 2),
            NumberOfGuests = 1
        });

    private Task<HousekeepingTaskDto> CreateTaskAsync(TestApi.Hotel hotel, string? assignee, HousekeepingTaskPriority priority = HousekeepingTaskPriority.Normal) =>
        _api.PostAsync<HousekeepingTaskDto>("/api/Housekeeping", hotel.AdminToken, new CreateHousekeepingTaskDto
        {
            RoomId = hotel.RoomId,
            AssignedToUserId = assignee,
            Type = HousekeepingTaskType.CleanRoom,
            Priority = priority,
            ScheduledFor = DateTime.UtcNow.Date
        });

    #region Bookings

    [Fact]
    public async Task GuestBooking_NotifiesTheHotelsOwnerAndManagers_Only()
    {
        var hotel = await _api.CreateHotelAsync();
        var (managerToken, _) = await StaffAsync("Manager", hotel.HotelId);
        var (housekeeperToken, _) = await StaffAsync("Housekeeper", hotel.HotelId);
        var otherHotel = await _api.CreateHotelAsync();
        var guestToken = await TestAuth.GetTokenAsync(_api.Client, "Guest");
        var ownerPhone = await RegisterPhoneAsync(hotel.AdminToken);

        var booking = await GuestBooksAsync(hotel, guestToken);

        foreach (var token in new[] { hotel.AdminToken, managerToken })
        {
            var list = await ListAsync(token);
            var notification = list.Items.Should().ContainSingle().Subject;
            notification.Type.Should().Be(NotificationType.NewBooking);
            notification.ReservationId.Should().Be(booking.Id);
            notification.Body.Should().Contain("Room 101");
            list.UnreadCount.Should().Be(1);
        }

        (await ListAsync(housekeeperToken)).Items.Should().BeEmpty();
        (await ListAsync(otherHotel.AdminToken)).Items.Should().BeEmpty();
        (await ListAsync(guestToken)).Items.Should().BeEmpty("nobody is told about what they did themselves");

        var push = _pushes.SentTo(ownerPhone).Should().ContainSingle().Subject;
        push.Title.Should().Be("New booking to approve");
        push.Data["reservationId"].Should().Be(booking.Id);
    }

    [Fact]
    public async Task HotelConfirmsAndCancels_GuestIsTold()
    {
        var hotel = await _api.CreateHotelAsync();
        var guestToken = await TestAuth.GetTokenAsync(_api.Client, "Guest");
        var guestPhone = await RegisterPhoneAsync(guestToken);
        var booking = await GuestBooksAsync(hotel, guestToken);

        await _api.PostAsync<ReservationDto>($"/api/Reservations/{booking.Id}/confirm", hotel.AdminToken);
        await _api.PostAsync<ReservationDto>($"/api/Reservations/{booking.Id}/cancel", hotel.AdminToken,
            new { reason = "Room under repair" });

        var list = await ListAsync(guestToken);
        list.Items.Select(n => n.Type).Should().Equal(NotificationType.BookingCancelled, NotificationType.BookingConfirmed);
        list.Items[0].Body.Should().Contain("Room under repair");
        _pushes.SentTo(guestPhone).Select(p => p.Title)
            .Should().Equal("Your booking is confirmed", "Your booking was cancelled");
    }

    [Fact]
    public async Task GuestCancels_HotelIsTold()
    {
        var hotel = await _api.CreateHotelAsync();
        var guestToken = await TestAuth.GetTokenAsync(_api.Client, "Guest");
        var booking = await GuestBooksAsync(hotel, guestToken);

        await _api.PostAsync<ReservationDto>($"/api/Reservations/{booking.Id}/cancel", guestToken, new { reason = "Plans changed" });

        (await ListAsync(hotel.AdminToken)).Items.Select(n => n.Type)
            .Should().Equal(NotificationType.BookingCancelledByGuest, NotificationType.NewBooking);
        (await ListAsync(guestToken)).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task StaffBookingForAGuestWithoutAccount_NotifiesNobody()
    {
        var hotel = await _api.CreateHotelAsync();

        var booking = await _api.BookAsync(hotel);
        await _api.PostAsync<ReservationDto>($"/api/Reservations/{booking.Id}/cancel", hotel.AdminToken, new { reason = "Test" });

        (await ListAsync(hotel.AdminToken)).Items.Should().BeEmpty();
    }

    #endregion

    #region Housekeeping

    [Fact]
    public async Task AssigningATask_NotifiesTheAssignee()
    {
        var hotel = await _api.CreateHotelAsync();
        var (housekeeperToken, housekeeperId) = await StaffAsync("Housekeeper", hotel.HotelId);
        var phone = await RegisterPhoneAsync(housekeeperToken);

        var task = await CreateTaskAsync(hotel, housekeeperId, HousekeepingTaskPriority.Urgent);

        var notification = (await ListAsync(housekeeperToken)).Items.Should().ContainSingle().Subject;
        notification.Type.Should().Be(NotificationType.TaskAssigned);
        notification.HousekeepingTaskId.Should().Be(task.Id);
        notification.Title.Should().Be("Urgent task: Room 101");
        _pushes.SentTo(phone).Should().ContainSingle().Which.Data["taskId"].Should().Be(task.Id);
    }

    [Fact]
    public async Task ReassigningOrRaisingToUrgent_NotifiesWhoeverHasTheTask()
    {
        var hotel = await _api.CreateHotelAsync();
        var (firstToken, firstId) = await StaffAsync("Housekeeper", hotel.HotelId);
        var (secondToken, secondId) = await StaffAsync("Housekeeper", hotel.HotelId);
        var task = await CreateTaskAsync(hotel, assignee: null);

        await _api.SendAsync<HousekeepingTaskDto>(HttpMethod.Put, $"/api/Housekeeping/{task.Id}", hotel.AdminToken,
            new UpdateHousekeepingTaskDto { AssignedToUserId = firstId });
        await _api.SendAsync<HousekeepingTaskDto>(HttpMethod.Put, $"/api/Housekeeping/{task.Id}", hotel.AdminToken,
            new UpdateHousekeepingTaskDto { AssignedToUserId = secondId });
        await _api.SendAsync<HousekeepingTaskDto>(HttpMethod.Put, $"/api/Housekeeping/{task.Id}", hotel.AdminToken,
            new UpdateHousekeepingTaskDto { Priority = HousekeepingTaskPriority.Urgent });
        // Editing notes changes nothing for the assignee
        await _api.SendAsync<HousekeepingTaskDto>(HttpMethod.Put, $"/api/Housekeeping/{task.Id}", hotel.AdminToken,
            new UpdateHousekeepingTaskDto { Notes = "Extra towels" });

        (await ListAsync(firstToken)).Items.Select(n => n.Type).Should().Equal(NotificationType.TaskAssigned);
        (await ListAsync(secondToken)).Items.Select(n => n.Type)
            .Should().Equal(NotificationType.TaskUrgent, NotificationType.TaskAssigned);
    }

    [Fact]
    public async Task AssigningATaskToYourself_NotifiesNobody()
    {
        var hotel = await _api.CreateHotelAsync();
        var (managerToken, managerId) = await StaffAsync("Manager", hotel.HotelId);

        await CreateTaskAsync(hotel with { AdminToken = managerToken }, managerId);

        (await ListAsync(managerToken)).Items.Should().BeEmpty();
    }

    #endregion

    #region Reading the list

    [Fact]
    public async Task MarkingRead_UpdatesTheUnreadCount_OnlyForYourOwnNotifications()
    {
        var hotel = await _api.CreateHotelAsync();
        var guestToken = await TestAuth.GetTokenAsync(_api.Client, "Guest");
        await GuestBooksAsync(hotel, guestToken);
        await GuestBooksAsync(hotel, guestToken, startsInDays: 20);
        var first = (await ListAsync(hotel.AdminToken)).Items[0];

        (await _api.SendAsync(HttpMethod.Post, $"/api/Notifications/{first.Id}/read", guestToken))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _api.SendAsync(HttpMethod.Post, $"/api/Notifications/{first.Id}/read", hotel.AdminToken))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await ListAsync(hotel.AdminToken);
        list.UnreadCount.Should().Be(1);
        list.Items.Single(n => n.Id == first.Id).IsRead.Should().BeTrue();

        (await _api.SendAsync(HttpMethod.Post, "/api/Notifications/read-all", hotel.AdminToken))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _api.GetAsync<UnreadCount>("/api/Notifications/unread-count", hotel.AdminToken)).Count.Should().Be(0);
    }

    private record UnreadCount(int Count);

    [Fact]
    public async Task List_IsPaged_NewestFirst()
    {
        var hotel = await _api.CreateHotelAsync();
        var guestToken = await TestAuth.GetTokenAsync(_api.Client, "Guest");
        for (var i = 0; i < 3; i++)
            await GuestBooksAsync(hotel, guestToken, startsInDays: 10 + i * 5);

        var page = await _api.GetAsync<NotificationPageDto>("/api/Notifications?page=1&pageSize=2", hotel.AdminToken);

        page.Items.Should().HaveCount(2);
        page.TotalCount.Should().Be(3);
        page.HasMore.Should().BeTrue();
        page.Items[0].ReservationId.Should().BeGreaterThan(page.Items[1].ReservationId!.Value);
    }

    [Fact]
    public async Task List_RequiresSignIn()
    {
        (await _api.Client.GetAsync("/api/Notifications")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Devices and settings

    [Fact]
    public async Task RegisterDevice_RejectsTokensThatArentExpoTokens()
    {
        var guestToken = await TestAuth.GetTokenAsync(_api.Client, "Guest");

        (await _api.SendAsync(HttpMethod.Post, "/api/Notifications/devices", guestToken,
            new RegisterDeviceDto { Token = "not-a-token", Platform = "android" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _api.SendAsync(HttpMethod.Post, "/api/Notifications/devices", guestToken,
            new RegisterDeviceDto { Token = NewPushToken(), Platform = "windows" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task APhoneBelongsToWhoeverSignedInLast_AndSignOutStopsItsPushes()
    {
        var hotel = await _api.CreateHotelAsync();
        var firstGuest = await TestAuth.GetTokenAsync(_api.Client, "Guest");
        var secondGuest = await TestAuth.GetTokenAsync(_api.Client, "Guest");
        var phone = NewPushToken();
        var device = new RegisterDeviceDto { Token = phone, Platform = "ios" };

        await _api.SendAsync(HttpMethod.Post, "/api/Notifications/devices", firstGuest, device);
        await _api.SendAsync(HttpMethod.Post, "/api/Notifications/devices", secondGuest, device);
        // Someone else can't remove it
        await _api.SendAsync(HttpMethod.Delete, "/api/Notifications/devices", firstGuest, new UnregisterDeviceDto { Token = phone });

        var firstBooking = await GuestBooksAsync(hotel, firstGuest);
        await _api.PostAsync<ReservationDto>($"/api/Reservations/{firstBooking.Id}/confirm", hotel.AdminToken);
        _pushes.SentTo(phone).Should().BeEmpty("the phone now belongs to the second guest");

        var secondBooking = await GuestBooksAsync(hotel, secondGuest, startsInDays: 20);
        await _api.PostAsync<ReservationDto>($"/api/Reservations/{secondBooking.Id}/confirm", hotel.AdminToken);
        _pushes.SentTo(phone).Should().ContainSingle();

        await _api.SendAsync(HttpMethod.Delete, "/api/Notifications/devices", secondGuest, new UnregisterDeviceDto { Token = phone });
        await _api.PostAsync<ReservationDto>($"/api/Reservations/{secondBooking.Id}/cancel", hotel.AdminToken, new { reason = "Test" });
        _pushes.SentTo(phone).Should().ContainSingle("signed out phones get no pushes");
        (await ListAsync(secondGuest)).Items.Should().HaveCount(2, "the list still has everything");
    }

    [Fact]
    public async Task Preferences_ListTheTypesForYourRole_AndTurningOneOffStopsItsPush()
    {
        var hotel = await _api.CreateHotelAsync();
        var guestToken = await TestAuth.GetTokenAsync(_api.Client, "Guest");
        var phone = await RegisterPhoneAsync(guestToken);

        var preferences = await _api.GetAsync<List<NotificationPreferenceDto>>("/api/Notifications/preferences", guestToken);
        preferences.Select(p => p.Type).Should().Equal(NotificationType.BookingConfirmed, NotificationType.BookingCancelled);
        preferences.Should().OnlyContain(p => p.PushEnabled);

        var updated = await _api.SendAsync<List<NotificationPreferenceDto>>(HttpMethod.Put, "/api/Notifications/preferences", guestToken,
            new[]
            {
                new NotificationPreferenceDto { Type = NotificationType.BookingConfirmed, PushEnabled = false },
                // Not a guest's type: ignored
                new NotificationPreferenceDto { Type = NotificationType.NewBooking, PushEnabled = false }
            });
        updated.Should().ContainSingle(p => !p.PushEnabled).Which.Type.Should().Be(NotificationType.BookingConfirmed);

        var booking = await GuestBooksAsync(hotel, guestToken);
        await _api.PostAsync<ReservationDto>($"/api/Reservations/{booking.Id}/confirm", hotel.AdminToken);
        await _api.PostAsync<ReservationDto>($"/api/Reservations/{booking.Id}/cancel", hotel.AdminToken, new { reason = "Test" });

        _pushes.SentTo(phone).Select(p => p.Title).Should().Equal("Your booking was cancelled");
        (await ListAsync(guestToken)).Items.Should().HaveCount(2);

        var staffTypes = await _api.GetAsync<List<NotificationPreferenceDto>>("/api/Notifications/preferences", hotel.AdminToken);
        staffTypes.Select(p => p.Type).Should().Contain(NotificationType.NewBooking).And.NotContain(NotificationType.BookingConfirmed);
    }

    #endregion
}
