using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CalendarApp.Application.Abstraction;
using CalendarApp.Domain.Abstraction;
using CalendarApp.Domain.Entities;
using CalendarApp.Application.Models;
using System.Security.Claims;

namespace CalendarApp.Web.Controllers
{
    [Authorize]
    public class EventsController : Controller
    {
        private readonly IEventAppService _eventAppService;
        private readonly IRecurrenceAppService _recurrenceAppService;
        private readonly ICalendarViewAppService _calendarViewAppService;
        private readonly IUserAppService _userAppService;

        public EventsController(IEventAppService eventAppService, IRecurrenceAppService recurrenceAppService, ICalendarViewAppService calendarViewAppService, IUserAppService userAppService)
        {
            _eventAppService = eventAppService;
            _recurrenceAppService = recurrenceAppService;
            _calendarViewAppService = calendarViewAppService;
            _userAppService = userAppService;
        }

        public async Task<IActionResult> Index(string view = "list", int year = 0, int month = 0, DateTime? date = null)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var username = User.Identity?.Name ?? "";

            // Get all events for the user (owner or attendee with accepted status)
            var allEvents = await _eventAppService.SelectAllAsync();
            var userEvents = allEvents.Where(e => e.OwnerId == userId || 
                e.EventAttendees.Any(ea => ea.UserId == userId && ea.Status == AttendeeStatus.Accepted))
                .OrderBy(e => e.StartTime)
                .ToList();

            ViewBag.Username = username;

            if (view == "monthly")
            {
                var displayDate = date ?? ((year > 0 && month > 0) ? new DateTime(year, month, 1) : DateTime.Now);

                // 42-day grid starts before the month; weeks start Monday
                var firstDayOfMonth = new DateTime(displayDate.Year, displayDate.Month, 1);
                var firstDayOfWeek = firstDayOfMonth.AddDays(-(((int)firstDayOfMonth.DayOfWeek + 6) % 7));
                var displayEvents = _recurrenceAppService.Expand(userEvents, firstDayOfWeek, firstDayOfWeek.AddDays(42));

                var monthlyViewModel = _calendarViewAppService.BuildMonthlyCalendar(displayEvents, displayDate);
                monthlyViewModel.CurrentView = "monthly";
                ViewBag.UserEvents = userEvents;
                return View("Index", monthlyViewModel);
            }

            if (view == "daily" || view == "weekly")
            {
                var anchor = (date ?? DateTime.Now).Date;
                List<DateTime> gridDays;
                if (view == "daily")
                {
                    gridDays = new List<DateTime> { anchor };
                }
                else
                {
                    // week starts Monday
                    var weekStart = anchor.AddDays(-(((int)anchor.DayOfWeek + 6) % 7));
                    gridDays = Enumerable.Range(0, 7).Select(i => weekStart.AddDays(i)).ToList();
                }

                var displayEvents = _recurrenceAppService.Expand(userEvents, gridDays[0], gridDays[^1].Date.AddDays(1));

                var gridViewModel = _calendarViewAppService.BuildTimeGrid(displayEvents, gridDays);
                gridViewModel.CurrentView = view;
                gridViewModel.CurrentUserId = userId;
                ViewBag.UserEvents = userEvents;
                return View("Index", gridViewModel);
            }

            // Expand over a bounded window; unbounded rules would grow forever
            var listStart = DateTime.Today.AddDays(-30);
            var listEnd = DateTime.Today.AddMonths(6);
            var listEvents = _recurrenceAppService.Expand(userEvents, listStart, listEnd)
                .OrderBy(e => e.StartTime)
                .ToList();

            var listViewModel = _calendarViewAppService.BuildList();
            ViewBag.UserEvents = listEvents;
            return View("Index", listViewModel);
        }

        public async Task<IActionResult> Details(int id, DateTime? occurrenceDate, string returnView = "list", DateTime? returnDate = null)
        {
            ViewBag.ReturnView = returnView;
            ViewBag.ReturnDate = returnDate;

            var @event = await _eventAppService.GetByIdAsync(id);
            if (@event == null)
            {
                return NotFound();
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            if (@event.OwnerId != userId && !@event.EventAttendees.Any(ea => ea.UserId == userId))
            {
                return Forbid();
            }

            var myAttendance = @event.EventAttendees.FirstOrDefault(ea => ea.UserId == userId);
            ViewBag.CanDecline = myAttendance != null && myAttendance.Role != AttendeeRole.Owner && myAttendance.Status != AttendeeStatus.Declined;
            ViewBag.IsDeclined = myAttendance?.Status == AttendeeStatus.Declined;
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

        [HttpPost]
        public async Task<IActionResult> Decline(int id, string returnView = "list", DateTime? returnDate = null)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            if (await _eventAppService.DeclineEventAsync(id, userId))
            {
                TempData["Success"] = "You declined this event.";
            }
            else
            {
                TempData["Error"] = "This event cannot be declined.";
            }

            return RedirectToAction("Details", new { id, returnView, returnDate = returnDate?.ToString("yyyy-MM-dd") });
        }

        [HttpGet]
        public IActionResult Create(DateTime? date = null, bool allDay = false, string view = "list", DateTime? returnDate = null)
        {
            var startTime = date ?? DateTime.Now;
            var viewModel = new EventCreateViewModel
            {
                StartTime = startTime,
                EndTime = startTime.AddHours(1),
                AllDay = allDay,
                Color = "#3788d8",
                ReturnView = view,
                // Return context = calendar view anchor; absent means requested start (or now)
                ReturnDate = returnDate ?? date ?? DateTime.Now
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Create(EventCreateViewModel viewModel)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

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
                OwnerId = userId,
                ReminderTime = viewModel.ReminderTime,
                Color = viewModel.Color,
                Status = viewModel.Status
            };

            if (ModelState.IsValid)
            {
                try
                {
                    await _eventAppService.CreateAsync(@event);

                    if (!string.IsNullOrEmpty(viewModel.InviteEmails))
                    {
                        var emailList = viewModel.InviteEmails
                            .Split(',')
                            .Select(e => e.Trim())
                            .Where(e => !string.IsNullOrEmpty(e) && e.Contains("@"))
                            .ToList();

                        if (emailList.Any())
                        {
                            var usersToInvite = await _userAppService.FindUsersByEmailsAsync(emailList);
                            var userIdsToInvite = usersToInvite.Select(u => u.Id).ToList();

                            if (userIdsToInvite.Any())
                            {
                                await _eventAppService.InviteUsersToEvent(@event.Id, userIdsToInvite);
                            }
                        }
                    }

                    // No success toast: Index renders none, and TempData would leak into the next page that does
                    var returnView = viewModel.ReturnView;
                    if (returnView != "daily" && returnView != "weekly" && returnView != "monthly")
                    {
                        returnView = "list";
                    }
                    return RedirectToAction("Index", new { view = returnView, date = viewModel.ReturnDate });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error creating event: " + ex.Message);
                }
            }

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, string? scope, DateTime? occurrenceDate, string returnView = "details", DateTime? returnDate = null)
        {
            var @event = await _eventAppService.GetByIdAsync(id);
            if (@event == null)
            {
                return NotFound();
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            if (@event.OwnerId != userId)
            {
                return Forbid();
            }

            var currentAttendees = @event.EventAttendees
                .Where(ea => ea.Role != AttendeeRole.Owner)
                .ToList();

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
                CurrentAttendees = currentAttendees,
                ReturnView = returnView,
                ReturnDate = returnDate ?? @event.StartTime
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
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var existingEvent = await _eventAppService.GetByIdAsync(viewModel.Id);
            if (existingEvent == null)
            {
                return NotFound();
            }

            if (existingEvent.OwnerId != userId)
            {
                return Forbid();
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
                        && viewModel.OccurrenceDate is DateTime occurrenceDate)
                    {
                        await _eventAppService.UpsertOccurrenceExceptionAsync(new EventOccurrenceException
                        {
                            MasterEventId = existingEvent.Id,
                            OriginalDate = occurrenceDate.Date,
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
                    }
                    else
                    {
                        await _eventAppService.UpdateAsync(@event);
                    }

                    // Service ignores unknown ids and never removes the owner
                    foreach (var attendeeId in viewModel.RemoveAttendeeIds)
                    {
                        await _eventAppService.RemoveAttendeeFromEvent(viewModel.Id, attendeeId);
                    }

                    if (!string.IsNullOrEmpty(viewModel.InviteEmails))
                    {
                        var emailList = viewModel.InviteEmails
                            .Split(',')
                            .Select(e => e.Trim())
                            .Where(e => !string.IsNullOrEmpty(e) && e.Contains("@"))
                            .ToList();

                        if (emailList.Any())
                        {
                            var usersToInvite = await _userAppService.FindUsersByEmailsAsync(emailList);
                            var userIdsToInvite = usersToInvite.Select(u => u.Id).ToList();

                            if (userIdsToInvite.Any())
                            {
                                await _eventAppService.InviteUsersToEvent(viewModel.Id, userIdsToInvite);
                            }
                        }
                    }

                    var returnView = viewModel.ReturnView;
                    if (returnView != "daily" && returnView != "weekly" && returnView != "monthly"
                        && returnView != "list" && returnView != "details")
                    {
                        returnView = "details";
                    }
                    if (returnView == "details")
                    {
                        return RedirectToAction("Details", new { id = viewModel.Id });
                    }
                    return RedirectToAction("Index", new { view = returnView, date = viewModel.ReturnDate });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error updating event: " + ex.Message);
                }
            }

            viewModel.CurrentAttendees = existingEvent.EventAttendees
                .Where(ea => ea.Role != AttendeeRole.Owner)
                .ToList();
            ViewBag.Users = await _userAppService.GetUsersByIdsAsync(existingEvent.EventAttendees.Select(ea => ea.UserId).Append(existingEvent.OwnerId));

            return View(viewModel);
        }

        // Drag & drop move: non-recurring rescheduled directly; one occurrence of a recurring series stored as exception row
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveOccurrence(int id, DateTime? occurrenceDate, DateTime newStart, DateTime newEnd, bool allDay = false)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var existingEvent = await _eventAppService.GetByIdAsync(id);
            if (existingEvent == null)
            {
                return NotFound();
            }

            // Only the owner may reschedule an event (same rule as Edit)
            if (existingEvent.OwnerId != userId)
            {
                return Forbid();
            }

            try
            {
                var moved = await _eventAppService.MoveOccurrenceAsync(id, occurrenceDate, newStart, newEnd, allDay);
                return moved ? NoContent() : NotFound();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id, string? scope, DateTime? occurrenceDate, string? returnView, DateTime? returnDate)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var existingEvent = await _eventAppService.GetByIdAsync(id);
            if (existingEvent == null)
            {
                return NotFound();
            }

            if (existingEvent.OwnerId != userId)
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Delete only one occurrence of a recurring series
                    if (existingEvent.IsRecurring
                        && string.Equals(scope, "this", StringComparison.OrdinalIgnoreCase)
                        && occurrenceDate.HasValue)
                    {
                        await _eventAppService.UpsertOccurrenceExceptionAsync(new EventOccurrenceException
                        {
                            MasterEventId = existingEvent.Id,
                            OriginalDate = occurrenceDate.Value.Date,
                            IsCancelled = true,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        });
                        TempData["Success"] = "Occurrence deleted.";
                        return RedirectToAction(nameof(Index), BuildIndexRoute(returnView, returnDate));
                    }

                    bool deleted = await _eventAppService.DeleteAsync(id);
                    if (deleted)
                    {
                        TempData["Success"] = "Event deleted successfully!";
                        return RedirectToAction(nameof(Index), BuildIndexRoute(returnView, returnDate));
                    }
                    ModelState.AddModelError("", "Error deleting event");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error deleting event: " + ex.Message);
                }
            }
            return RedirectToAction(nameof(Index), BuildIndexRoute(returnView, returnDate));
        }

        // daily/weekly/monthly pass through; anything else falls back to the list view
        private static object BuildIndexRoute(string? returnView, DateTime? returnDate)
        {
            var view = returnView == "daily" || returnView == "weekly" || returnView == "monthly"
                ? returnView
                : "list";
            return new { view, date = returnDate };
        }

        [HttpGet]
        public async Task<IActionResult> SearchUsers(string term)
        {
            if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 3)
            {
                return Json(Enumerable.Empty<object>());
            }

            var users = await _userAppService.SearchUsersAsync(term.Trim());
            return Json(users.Select(u => new { u.Id, u.UserName, u.Email }));
        }
    }
}