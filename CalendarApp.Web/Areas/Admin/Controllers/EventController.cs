using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CalendarApp.Application.Abstraction;
using CalendarApp.Domain.Abstraction;
using CalendarApp.Domain.Entities;
using CalendarApp.Application.Models;
using System.Security.Claims;

namespace CalendarApp.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class EventController : Controller
    {
        private readonly IEventAppService _eventAppService;
        private readonly IUserAppService _userAppService;
        private readonly IRecurrenceAppService _recurrenceAppService;

        public EventController(IEventAppService eventAppService, IUserAppService userAppService, IRecurrenceAppService recurrenceAppService)
        {
            _eventAppService = eventAppService;
            _userAppService = userAppService;
            _recurrenceAppService = recurrenceAppService;
        }

        public async Task<IActionResult> Select(int? ownerId)
        {
            var events = ownerId.HasValue
                ? await _eventAppService.SelectByOwnerAsync(ownerId.Value)
                : await _eventAppService.SelectAllAsync();

            ViewBag.Users = await _userAppService.GetUsersByIdsAsync(events.Select(e => e.OwnerId));

            var allUsers = await _userAppService.GetAllUsersAsync();
            ViewBag.AllUsers = allUsers;
            ViewBag.SelectedOwnerId = ownerId;

            return View(events);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? ownerId)
        {
            var allUsers = await _userAppService.GetAllUsersAsync();
            var currentAdminId = GetCurrentUserId();

            var viewModel = new AdminEventCreateViewModel
            {
                AllUsers = allUsers.ToList(),
                OwnerId = ownerId ?? currentAdminId,
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddHours(1),
                Color = "#3788d8"
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Create(AdminEventCreateViewModel viewModel)
        {
            // Re-populate owner dropdown for view return
            var allUsers = await _userAppService.GetAllUsersAsync();
            viewModel.AllUsers = allUsers.ToList();

            if (ModelState.IsValid)
            {
                var @event = new Event
                {
                    Title = viewModel.Title,
                    Description = viewModel.Description,
                    StartTime = viewModel.StartTime,
                    EndTime = viewModel.EndTime,
                    AllDay = viewModel.AllDay,
                    Location = viewModel.Location,
                    IsRecurring = viewModel.IsRecurring,
                    RecurrenceRule = viewModel.IsRecurring ? viewModel.RecurrenceRule : null,
                    RecurrenceEnd = viewModel.IsRecurring ? viewModel.RecurrenceEnd : null,
                    OwnerId = viewModel.OwnerId,
                    ReminderTime = viewModel.ReminderTime,
                    Color = viewModel.Color,
                    Status = viewModel.Status
                };

                try
                {
                    await _eventAppService.CreateAsync(@event);

                    var invitedCount = await InviteByEmailsAsync(@event.Id, viewModel.InviteEmails, viewModel.OwnerId);

                    var ownerName = allUsers.FirstOrDefault(u => u.Id == viewModel.OwnerId)?.UserName ?? "Unknown";
                    TempData["Success"] = $"Event created for {ownerName} with {invitedCount} additional attendees!";
                    return RedirectToAction(nameof(Select), new { ownerId = viewModel.OwnerId });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error creating event: " + ex.Message);
                }
            }

            return View(viewModel);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        // Resolves the comma-separated invite list to users and invites them.
        // Emails that belong to the event owner are ignored (owner attends by definition).
        private async Task ApplyAttendeeChangesAsync(EventEditViewModel viewModel, int ownerId)
        {
            foreach (var attendeeId in viewModel.RemoveAttendeeIds)
            {
                await _eventAppService.RemoveAttendeeFromEvent(viewModel.Id, attendeeId);
            }

            await InviteByEmailsAsync(viewModel.Id, viewModel.InviteEmails, ownerId);
        }

        private async Task<int> InviteByEmailsAsync(int eventId, string? inviteEmails, int ownerId)
        {
            if (string.IsNullOrEmpty(inviteEmails))
            {
                return 0;
            }

            var emailList = inviteEmails
                .Split(',')
                .Select(e => e.Trim())
                .Where(e => !string.IsNullOrEmpty(e) && e.Contains("@"))
                .ToList();

            if (!emailList.Any())
            {
                return 0;
            }

            var usersToInvite = await _userAppService.FindUsersByEmailsAsync(emailList);
            var userIdsToInvite = usersToInvite
                .Where(u => u.Id != ownerId)
                .Select(u => u.Id)
                .ToList();

            if (userIdsToInvite.Any())
            {
                await _eventAppService.InviteUsersToEvent(eventId, userIdsToInvite);
            }

            return userIdsToInvite.Count;
        }

        public async Task<IActionResult> Details(int id, DateTime? occurrenceDate)
        {
            var @event = await _eventAppService.GetByIdAsync(id);
            if (@event == null)
            {
                return NotFound();
            }

            ViewBag.Users = await _userAppService.GetUsersByIdsAsync(@event.EventAttendees.Select(ea => ea.UserId).Append(@event.OwnerId));
            ViewBag.RecurrenceDescription = _recurrenceAppService.Describe(@event);

            // Recurring series: resolve the requested occurrence (exception overrides applied)
            if (@event.IsRecurring && occurrenceDate.HasValue)
            {
                var target = occurrenceDate.Value.Date;
                var occurrences = _recurrenceAppService.Expand(@event, target, target.AddDays(1));
                var occurrence = occurrences.FirstOrDefault(o => o.StartTime.Date == target) ?? occurrences.FirstOrDefault();
                if (occurrence != null)
                {
                    ViewBag.IsOccurrence = true;
                    return View(occurrence);
                }
                ViewBag.OccurrenceMissing = true;
            }

            return View(@event);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, DateTime? occurrenceDate, string? scope)
        {
            var @event = await _eventAppService.GetByIdAsync(id);
            if (@event == null)
            {
                return NotFound();
            }

            var allUsers = await _userAppService.GetAllUsersAsync();
            ViewBag.OwnerName = allUsers.FirstOrDefault(u => u.Id == @event.OwnerId)?.UserName ?? "Unknown";
            ViewBag.Users = await _userAppService.GetUsersByIdsAsync(@event.EventAttendees.Select(ea => ea.UserId).Append(@event.OwnerId));

            var viewModel = new EventEditViewModel
            {
                Id = @event.Id,
                Title = @event.Title,
                Description = @event.Description,
                StartTime = @event.StartTime,
                EndTime = @event.EndTime,
                AllDay = @event.AllDay,
                Location = @event.Location,
                IsRecurring = @event.IsRecurring,
                RecurrenceRule = @event.RecurrenceRule,
                RecurrenceEnd = @event.RecurrenceEnd,
                OwnerId = @event.OwnerId,
                ReminderTime = @event.ReminderTime,
                Color = @event.Color,
                Status = @event.Status,
                CurrentAttendees = @event.EventAttendees.Where(ea => ea.Role != AttendeeRole.Owner).ToList()
            };

            // Occurrence link: prefill the single occurrence, defaulting to "this" scope
            if (@event.IsRecurring && occurrenceDate.HasValue)
            {
                var target = occurrenceDate.Value.Date;
                var occurrences = _recurrenceAppService.Expand(@event, target, target.AddDays(1));
                var occurrence = occurrences.FirstOrDefault(o => o.StartTime.Date == target) ?? occurrences.FirstOrDefault();
                if (occurrence != null)
                {
                    viewModel.Scope = string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase) ? "all" : "this";
                    viewModel.OccurrenceDate = occurrence.StartTime.Date;
                    viewModel.Title = occurrence.Title;
                    viewModel.Description = occurrence.Description;
                    viewModel.StartTime = occurrence.StartTime;
                    viewModel.EndTime = occurrence.EndTime;
                    viewModel.AllDay = occurrence.AllDay;
                    viewModel.Location = occurrence.Location;
                    viewModel.ReminderTime = occurrence.ReminderTime;
                    viewModel.Color = occurrence.Color;
                    viewModel.Status = occurrence.Status;
                }
            }

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(EventEditViewModel viewModel)
        {
            var existingEvent = await _eventAppService.GetByIdAsync(viewModel.Id);
            if (existingEvent == null)
            {
                return NotFound();
            }

            // Owner is always taken from the stored event, never from the form
            var @event = new Event
            {
                Id = viewModel.Id,
                Title = viewModel.Title,
                Description = viewModel.Description,
                StartTime = viewModel.StartTime,
                EndTime = viewModel.EndTime,
                AllDay = viewModel.AllDay,
                Location = viewModel.Location,
                IsRecurring = viewModel.IsRecurring,
                RecurrenceRule = viewModel.IsRecurring ? viewModel.RecurrenceRule : null,
                RecurrenceEnd = viewModel.IsRecurring ? viewModel.RecurrenceEnd : null,
                OwnerId = existingEvent.OwnerId,
                ReminderTime = viewModel.ReminderTime,
                Color = viewModel.Color,
                Status = viewModel.Status
            };

            if (ModelState.IsValid)
            {
                try
                {
                    // Per-occurrence edit: store an exception row, leave the series master untouched
                    if (existingEvent.IsRecurring
                        && viewModel.Scope == "this"
                        && viewModel.OccurrenceDate is DateTime occurrenceDay)
                    {
                        await _eventAppService.UpsertOccurrenceExceptionAsync(new EventOccurrenceException
                        {
                            MasterEventId = existingEvent.Id,
                            OriginalDate = occurrenceDay.Date,
                            IsCancelled = false,
                            Title = viewModel.Title,
                            Description = viewModel.Description,
                            StartTime = viewModel.StartTime,
                            EndTime = viewModel.EndTime,
                            AllDay = viewModel.AllDay,
                            Location = viewModel.Location,
                            ReminderTime = viewModel.ReminderTime,
                            Color = viewModel.Color,
                            Status = viewModel.Status,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        });
                        await ApplyAttendeeChangesAsync(viewModel, existingEvent.OwnerId);
                        TempData["Success"] = "Occurrence saved!";
                        return RedirectToAction(nameof(Details), new { id = existingEvent.Id, occurrenceDate = occurrenceDay.Date });
                    }

                    await _eventAppService.UpdateAsync(@event);
                    TempData["Success"] = "Event updated successfully!";

                    await ApplyAttendeeChangesAsync(viewModel, existingEvent.OwnerId);

                    return RedirectToAction(nameof(Select), new { ownerId = existingEvent.OwnerId });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error updating event: " + ex.Message);
                }
            }

            var allUsers = await _userAppService.GetAllUsersAsync();
            ViewBag.OwnerName = allUsers.FirstOrDefault(u => u.Id == existingEvent.OwnerId)?.UserName ?? "Unknown";
            viewModel.CurrentAttendees = existingEvent.EventAttendees
                .Where(ea => ea.Role != AttendeeRole.Owner)
                .ToList();
            ViewBag.Users = await _userAppService.GetUsersByIdsAsync(existingEvent.EventAttendees.Select(ea => ea.UserId).Append(existingEvent.OwnerId));

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id, int? ownerId)
        {
            bool deleted = await _eventAppService.DeleteAsync(id);
            if (deleted)
            {
                return RedirectToAction(nameof(Select), new { ownerId });
            }
            return NotFound();
        }
    }
}
