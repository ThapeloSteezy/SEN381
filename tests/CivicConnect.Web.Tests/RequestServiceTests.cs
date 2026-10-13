using CivicConnect.Web.Application.Interfaces;
using CivicConnect.Web.Application.Services;
using CivicConnect.Web.Domain.Entities;
using CivicConnect.Web.Domain.Enums;

namespace CivicConnect.Web.Tests;

public class RequestServiceTests
{
    private static readonly UserRef Requester = new() { Id = "u1", Email = "naledi@example.org", Name = "Naledi" };
    private static readonly UserRef Supervisor = new() { Id = "sup", Email = "pieter@example.org", Name = "Pieter" };
    private static readonly UserRef Thandi = new() { Id = "s1", Email = "thandi@example.org", Name = "Thandi" };
    private static readonly UserRef Johan = new() { Id = "s2", Email = "johan@example.org", Name = "Johan" };

    private readonly InMemoryStore _store = new();
    private readonly RequestService _service;

    public RequestServiceTests()
    {
        var users = new InMemoryUsers(
            new ApplicationUser { Id = "s1", Email = "thandi@example.org", DisplayName = "Thandi", Role = UserRoles.Staff },
            new ApplicationUser { Id = "s2", Email = "johan@example.org", DisplayName = "Johan", Role = UserRoles.Staff },
            new ApplicationUser { Id = "u1", Email = "naledi@example.org", DisplayName = "Naledi", Role = UserRoles.Requester });
        _service = new RequestService(_store, users, new StatusTransitionPolicy());
    }

    private Task<ServiceRequest> NewRequest(string title = "Leaking tap")
        => _service.CreateAsync(Requester, title, "Kitchen tap drips all day.", 5, "kitchen", RequestPriority.Medium);

    [Fact]
    public async Task Create_stores_room_and_records_submission_in_history()
    {
        var request = await NewRequest();

        Assert.Equal("kitchen", _store.Requests.Single().RoomId);
        var entry = Assert.Single(request.History);
        Assert.Equal("Submitted", entry.ActionType);
        Assert.Equal(RequestStatus.New, entry.ToStatus);
    }

    [Fact]
    public async Task Create_without_title_uses_first_sentence_of_description()
    {
        var request = await _service.CreateAsync(Requester, "", "The kitchen tap will not stop dripping. It started on Monday.", 5, "kitchen", RequestPriority.Low);

        Assert.Equal("The kitchen tap will not stop dripping", request.Title);
    }

    [Fact]
    public async Task Create_rejects_a_room_that_is_not_on_the_plan()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(Requester, "Leaking tap", "Kitchen tap drips all day.", 5, "basement", RequestPriority.Medium));
        Assert.Equal("roomId", ex.ParamName);
        Assert.Empty(_store.Requests);
    }

    [Fact]
    public async Task Assign_moves_new_request_to_assigned_and_records_the_assignee()
    {
        var request = await NewRequest();

        var result = await _service.AssignAsync(request.Id, RequestStatus.New, "s1", Supervisor, "Plumber needed");

        Assert.True(result.Success);
        var stored = _store.Requests.Single();
        Assert.Equal(RequestStatus.Assigned, stored.Status);
        Assert.Equal("s1", stored.AssignedToId);
        Assert.Equal("Thandi", stored.AssignedTo?.Name);
        var entry = stored.History.Last();
        Assert.Equal("Assigned", entry.ActionType);
        Assert.Equal("s1", entry.AssignedTo?.Id);
        Assert.Equal("Plumber needed", entry.Notes);
        Assert.Equal(["u1", "s1"], stored.Feedback.Select(f => f.RecipientUserId));
    }

    [Fact]
    public async Task Assign_to_someone_who_is_not_staff_is_rejected()
    {
        var request = await NewRequest();

        var result = await _service.AssignAsync(request.Id, RequestStatus.New, "u1", Supervisor, null);

        Assert.False(result.Success);
        Assert.Equal(RequestStatus.New, _store.Requests.Single().Status);
        Assert.Equal(0, _store.Updates);
    }

    [Fact]
    public async Task Reassign_keeps_the_status_and_changes_the_assignee()
    {
        var request = await NewRequest();
        await _service.AssignAsync(request.Id, RequestStatus.New, "s1", Supervisor, null);

        var result = await _service.AssignAsync(request.Id, RequestStatus.Assigned, "s2", Supervisor, null);

        Assert.True(result.Success);
        var stored = _store.Requests.Single();
        Assert.Equal(RequestStatus.Assigned, stored.Status);
        Assert.Equal("s2", stored.AssignedToId);
        Assert.Equal("Reassigned", stored.History.Last().ActionType);
    }

    [Fact]
    public async Task Status_cannot_be_set_to_assigned_without_picking_a_person()
    {
        var request = await NewRequest();

        var result = await _service.ChangeStatusAsync(request.Id, RequestStatus.New, RequestStatus.Assigned, Supervisor, true, null);

        Assert.False(result.Success);
        Assert.Equal(RequestStatus.New, _store.Requests.Single().Status);
    }

    [Fact]
    public async Task Skipping_a_step_is_rejected_and_leaves_the_request_unchanged()
    {
        var request = await NewRequest();

        var result = await _service.ChangeStatusAsync(request.Id, RequestStatus.New, RequestStatus.Resolved, Supervisor, true, null);

        Assert.False(result.Success);
        Assert.Contains("can only move to Assigned", result.Error);
        var stored = _store.Requests.Single();
        Assert.Equal(RequestStatus.New, stored.Status);
        Assert.Single(stored.History);
        Assert.Equal(0, _store.Updates);
    }

    [Fact]
    public async Task Stale_form_is_rejected_when_someone_else_moved_the_request()
    {
        var request = await NewRequest();
        await _service.AssignAsync(request.Id, RequestStatus.New, "s1", Supervisor, null);

        var result = await _service.AssignAsync(request.Id, RequestStatus.New, "s2", Supervisor, null);

        Assert.False(result.Success);
        Assert.Contains("already moved to Assigned", result.Error);
        Assert.Equal(1, _store.Updates);
    }

    [Fact]
    public async Task Assigned_staff_member_can_start_and_finish_the_job()
    {
        var request = await NewRequest();
        await _service.AssignAsync(request.Id, RequestStatus.New, "s1", Supervisor, null);

        var started = await _service.ChangeStatusAsync(request.Id, RequestStatus.Assigned, RequestStatus.InProgress, Thandi, false, null);
        var fixedIt = await _service.ChangeStatusAsync(request.Id, RequestStatus.InProgress, RequestStatus.Resolved, Thandi, false, "Washer replaced");

        Assert.True(started.Success);
        Assert.True(fixedIt.Success);
        var stored = _store.Requests.Single();
        Assert.Equal(RequestStatus.Resolved, stored.Status);
        Assert.Equal("Washer replaced", stored.History.Last().Notes);
    }

    [Fact]
    public async Task Staff_member_who_is_not_assigned_cannot_update_the_job()
    {
        var request = await NewRequest();
        await _service.AssignAsync(request.Id, RequestStatus.New, "s1", Supervisor, null);

        var result = await _service.ChangeStatusAsync(request.Id, RequestStatus.Assigned, RequestStatus.InProgress, Johan, false, null);

        Assert.False(result.Success);
        Assert.Equal(RequestStatus.Assigned, _store.Requests.Single().Status);
    }

    [Fact]
    public async Task Only_the_supervisor_can_close_a_fixed_request()
    {
        var request = await NewRequest();
        await _service.AssignAsync(request.Id, RequestStatus.New, "s1", Supervisor, null);
        await _service.ChangeStatusAsync(request.Id, RequestStatus.Assigned, RequestStatus.InProgress, Thandi, false, null);
        await _service.ChangeStatusAsync(request.Id, RequestStatus.InProgress, RequestStatus.Resolved, Thandi, false, null);

        var byStaff = await _service.ChangeStatusAsync(request.Id, RequestStatus.Resolved, RequestStatus.Closed, Thandi, false, null);
        var bySupervisor = await _service.ChangeStatusAsync(request.Id, RequestStatus.Resolved, RequestStatus.Closed, Supervisor, true, null);

        Assert.False(byStaff.Success);
        Assert.True(bySupervisor.Success);
        Assert.Equal(RequestStatus.Closed, _store.Requests.Single().Status);
    }

    [Fact]
    public async Task Assigned_staff_member_gets_an_unread_update_until_they_open_it()
    {
        var request = await NewRequest();
        await _service.AssignAsync(request.Id, RequestStatus.New, "s1", Supervisor, null);

        Assert.Equal(1, await _service.CountUnreadUpdatesAsync("s1"));
        var update = Assert.Single(await _service.ListUpdatesAsync("s1"));
        Assert.Equal("Pieter gave you a new job.", update.Feedback.Message);
        Assert.Equal(1, await _service.CountUnreadUpdatesAsync("u1"));

        await _service.MarkUpdatesReadAsync(request.Id, "s1");

        Assert.Equal(0, await _service.CountUnreadUpdatesAsync("s1"));
        Assert.Equal(1, await _service.CountUnreadUpdatesAsync("u1"));
    }

    [Fact]
    public async Task Supervisor_is_told_when_the_job_is_fixed()
    {
        var request = await NewRequest();
        await _service.AssignAsync(request.Id, RequestStatus.New, "s1", Supervisor, null);
        await _service.ChangeStatusAsync(request.Id, RequestStatus.Assigned, RequestStatus.InProgress, Thandi, false, null);
        await _service.ChangeStatusAsync(request.Id, RequestStatus.InProgress, RequestStatus.Resolved, Thandi, false, null);

        var update = Assert.Single(await _service.ListUpdatesAsync("sup"));
        Assert.Equal("Thandi fixed this. It is ready to close.", update.Feedback.Message);
    }

    private sealed class InMemoryUsers(params ApplicationUser[] users) : IUserDirectory
    {
        public Task<ApplicationUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult(users.FirstOrDefault(u => u.Email == email));

        public Task<ApplicationUser?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(users.FirstOrDefault(u => u.Id == id));

        public Task<IReadOnlyList<ApplicationUser>> ListByRoleAsync(string role, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ApplicationUser>>(users.Where(u => u.Role == role).ToList());

        public Task InsertAsync(ApplicationUser user, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class InMemoryStore : IRequestStore
    {
        public List<ServiceRequest> Requests { get; } = [];
        public int Updates { get; private set; }

        public Task<ServiceRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Requests.FirstOrDefault(r => r.Id == id));

        public Task<IReadOnlyList<ServiceRequest>> GetVisibleToUserAsync(string userId, bool staffAccess, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServiceRequest>>(Requests.Where(r => staffAccess || r.SubmittedById == userId || r.AssignedToId == userId).ToList());

        public Task<IReadOnlyList<RequestCategory>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RequestCategory>>([new RequestCategory { Id = 5, Name = "Maintenance" }]);

        public Task<RequestCategory?> GetCategoryByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<RequestCategory?>(id == 5 ? new RequestCategory { Id = 5, Name = "Maintenance" } : null);

        public Task InsertAsync(ServiceRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }

        public Task<bool> TryUpdateAsync(ServiceRequest request, long previousVersion, CancellationToken cancellationToken = default)
        {
            Updates++;
            request.Version = previousVersion + 1;
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<ServiceRequest>> GetWithFeedbackForAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServiceRequest>>(Requests.Where(r => r.Feedback.Any(f => f.RecipientUserId == userId)).ToList());

        public Task<long> CountWithUnreadFeedbackForAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult((long)Requests.Count(r => r.Feedback.Any(f => f.RecipientUserId == userId && !f.IsRead)));

        public Task MarkFeedbackReadAsync(Guid requestId, string userId, CancellationToken cancellationToken = default)
        {
            foreach (var item in Requests.Single(r => r.Id == requestId).Feedback.Where(f => f.RecipientUserId == userId))
                item.IsRead = true;
            return Task.CompletedTask;
        }

        public Task EnsureCategoriesAsync(IReadOnlyList<RequestCategory> categories, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
