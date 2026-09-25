using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Enums;
using HotelManagement.Tests.Helpers;
using Xunit;

namespace HotelManagement.Tests.Integration;

/// <summary>
/// Housekeepers work their own hotel's tasks; management plans them
/// </summary>
public class HousekeepingIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly TestApi _api;

    public HousekeepingIntegrationTests(CustomWebApplicationFactory factory)
    {
        _api = new TestApi(factory.CreateClient());
    }

    private Task<HttpResponseMessage> CreateTaskAsync(string token, int roomId, string? assignee = null) =>
        _api.SendAsync(HttpMethod.Post, "/api/Housekeeping", token, new CreateHousekeepingTaskDto
        {
            RoomId = roomId,
            AssignedToUserId = assignee,
            Type = HousekeepingTaskType.CleanRoom,
            ScheduledFor = DateTime.UtcNow.Date
        });

    private async Task<(string Token, string UserId)> CreateHousekeeperAsync(int hotelId)
    {
        var email = $"hk{Guid.NewGuid():N}@test.com";
        var token = await TestAuth.GetTokenAsync(_api.Client, "Housekeeper", email, hotelId);
        return (token, await _api.GetUserIdAsync(email));
    }

    [Fact]
    public async Task Housekeeper_CleansTheRoomAfterCheckout_AndItBecomesAvailable()
    {
        var hotel = await _api.CreateHotelAsync();
        var (housekeeperToken, _) = await CreateHousekeeperAsync(hotel.HotelId);
        var stay = await _api.WalkInAsync(hotel);
        await _api.PostAsync<ReservationDto>($"/api/Reservations/{stay.Id}/checkout", hotel.AdminToken);

        await _api.PostAsync<object>($"/api/Housekeeping/hotel/{hotel.HotelId}/generate-daily?date={DateTime.UtcNow.Date:yyyy-MM-dd}", hotel.AdminToken);

        var tasks = await _api.GetAsync<List<HousekeepingTaskDto>>($"/api/Housekeeping/hotel/{hotel.HotelId}", housekeeperToken);
        var task = tasks.Should().ContainSingle(t => t.RoomId == hotel.RoomId).Subject;

        (await _api.PostAsync<HousekeepingTaskDto>($"/api/Housekeeping/{task.Id}/start", housekeeperToken))
            .Status.Should().Be(HousekeepingTaskStatus.InProgress);
        (await _api.PostAsync<HousekeepingTaskDto>($"/api/Housekeeping/{task.Id}/complete", housekeeperToken))
            .Status.Should().Be(HousekeepingTaskStatus.Completed);

        (await _api.GetAsync<RoomDto>($"/api/Rooms/{hotel.RoomId}", hotel.AdminToken)).Status.Should().Be(RoomStatus.Available);

        // Generating again the same day doesn't duplicate the task
        await _api.PostAsync<object>($"/api/Housekeeping/hotel/{hotel.HotelId}/generate-daily?date={DateTime.UtcNow.Date:yyyy-MM-dd}", hotel.AdminToken);
        (await _api.GetAsync<List<HousekeepingTaskDto>>($"/api/Housekeeping/hotel/{hotel.HotelId}", hotel.AdminToken))
            .Should().ContainSingle();
    }

    [Fact]
    public async Task Housekeeper_CannotPlanTasksOrSeePerformance()
    {
        var hotel = await _api.CreateHotelAsync();
        var (housekeeperToken, _) = await CreateHousekeeperAsync(hotel.HotelId);

        (await CreateTaskAsync(housekeeperToken, hotel.RoomId)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _api.SendAsync(HttpMethod.Post, $"/api/Housekeeping/hotel/{hotel.HotelId}/generate-daily", housekeeperToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _api.SendAsync(HttpMethod.Get, $"/api/Housekeeping/hotel/{hotel.HotelId}/performance", housekeeperToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HousekeeperOfAnotherHotel_CannotSeeOrWorkTheseTasks()
    {
        var hotel = await _api.CreateHotelAsync();
        var other = await _api.CreateHotelAsync();
        var (outsiderToken, _) = await CreateHousekeeperAsync(other.HotelId);
        var created = await CreateTaskAsync(hotel.AdminToken, hotel.RoomId);
        var task = await created.Content.ReadFromJsonAsync<HousekeepingTaskDto>();

        (await _api.SendAsync(HttpMethod.Get, $"/api/Housekeeping/hotel/{hotel.HotelId}", outsiderToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _api.SendAsync(HttpMethod.Post, $"/api/Housekeeping/{task!.Id}/complete", outsiderToken))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CreateTaskAsync(other.AdminToken, hotel.RoomId)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Tasks_CanBeAssignedToTheHotelsOwnStaff()
    {
        var hotel = await _api.CreateHotelAsync();
        var (_, housekeeperId) = await CreateHousekeeperAsync(hotel.HotelId);

        var response = await CreateTaskAsync(hotel.AdminToken, hotel.RoomId, housekeeperId);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<HousekeepingTaskDto>())!.AssignedToUserId.Should().Be(housekeeperId);
    }

    [Fact]
    public async Task Tasks_CannotBeAssignedToOtherHotelsStaffOrUnknownUsers()
    {
        var hotel = await _api.CreateHotelAsync();
        var other = await _api.CreateHotelAsync();
        var (_, outsiderId) = await CreateHousekeeperAsync(other.HotelId);

        (await CreateTaskAsync(hotel.AdminToken, hotel.RoomId, outsiderId)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CreateTaskAsync(hotel.AdminToken, hotel.RoomId, "no-such-user")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var task = await (await CreateTaskAsync(hotel.AdminToken, hotel.RoomId)).Content.ReadFromJsonAsync<HousekeepingTaskDto>();
        (await _api.SendAsync(HttpMethod.Put, $"/api/Housekeeping/{task!.Id}", hotel.AdminToken, new UpdateHousekeepingTaskDto { AssignedToUserId = outsiderId }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // An empty id still unassigns
        (await _api.SendAsync(HttpMethod.Put, $"/api/Housekeeping/{task.Id}", hotel.AdminToken, new UpdateHousekeepingTaskDto { AssignedToUserId = "" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Guests_HaveNoAccessToHousekeeping()
    {
        var hotel = await _api.CreateHotelAsync();
        var guestToken = await TestAuth.GetTokenAsync(_api.Client, "Guest");

        (await _api.SendAsync(HttpMethod.Get, $"/api/Housekeeping/hotel/{hotel.HotelId}", guestToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
