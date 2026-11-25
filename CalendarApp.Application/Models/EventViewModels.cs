using System.ComponentModel.DataAnnotations;
using CalendarApp.Domain.Entities;
using CalendarApp.Domain.Abstraction;
using CalendarApp.Domain.Validations;

namespace CalendarApp.Application.Models
{
    public class EventCreateViewModel
    {
        [Required, TextLengthRange(3, 255)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [Required]
        public DateTime StartTime { get; set; } = DateTime.Now;

        [Required, DateRange]
        public DateTime EndTime { get; set; } = DateTime.Now.AddHours(1);

        public bool AllDay { get; set; } = false;

        [StringLength(255)]
        public string? Location { get; set; }

        public bool IsRecurring { get; set; } = false;

        [RequiredIf("IsRecurring", true), StringLength(255)]
        public string? RecurrenceRule { get; set; }

        [DateNotBefore("StartTime")]
        public DateTime? RecurrenceEnd { get; set; }

        public DateTime? ReminderTime { get; set; }

        [RegularExpression("^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{3})$", ErrorMessage = "Color must be a valid hex color (e.g. #3788d8).")]
        [StringLength(7)]
        public string? Color { get; set; } = "#3788d8";

        public EventStatus Status { get; set; } = EventStatus.Confirmed;

        [StringLength(1000), EmailList]
        public string? InviteEmails { get; set; } = null;

        public string ReturnView { get; set; } = "list";
        public DateTime? ReturnDate { get; set; }
    }

    public class EventEditViewModel
    {
        [Required]
        public int Id { get; set; }

        [Required, TextLengthRange(3, 255)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [Required, DateRange]
        public DateTime EndTime { get; set; }

        public bool AllDay { get; set; } = false;

        [StringLength(255)]
        public string? Location { get; set; }

        public bool IsRecurring { get; set; } = false;

        [RequiredIf("IsRecurring", true), StringLength(255)]
        public string? RecurrenceRule { get; set; }

        [DateNotBefore("StartTime")]
        public DateTime? RecurrenceEnd { get; set; }

        [Required]
        public int OwnerId { get; set; }

        public DateTime? ReminderTime { get; set; }

        [RegularExpression("^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{3})$", ErrorMessage = "Color must be a valid hex color (e.g. #3788d8).")]
        [StringLength(7)]
        public string? Color { get; set; }

        public EventStatus Status { get; set; } = EventStatus.Confirmed;

        [StringLength(1000), EmailList]
        public string? InviteEmails { get; set; } = null;

        public List<int> RemoveAttendeeIds { get; set; } = new List<int>();
        public List<EventAttendee> CurrentAttendees { get; set; } = new List<EventAttendee>();

        // Per-occurrence editing: "all" = the whole series, "this" = one occurrence only
        public string Scope { get; set; } = "all";
        public DateTime? OccurrenceDate { get; set; }

        public string ReturnView { get; set; } = "details";
        public DateTime? ReturnDate { get; set; }
    }

    public class CalendarDayViewModel
    {
        public DateTime Date { get; set; }
        public bool IsCurrentMonth { get; set; }
        public bool IsToday { get; set; }
        public List<Event> Events { get; set; } = new List<Event>();
    }

    public class AdminEventCreateViewModel : EventCreateViewModel
    {
        [Required]
        [Display(Name = "Event Owner")]
        public int OwnerId { get; set; }

        public IList<UserWithRoleDto> AllUsers { get; set; } = new List<UserWithRoleDto>();
    }

    public class MonthlyCalendarViewModel
    {
        public DateTime CurrentDate { get; set; }
        public DateTime DisplayMonth { get; set; }
        public List<CalendarDayViewModel> Days { get; set; } = new List<CalendarDayViewModel>();
        public string CurrentView { get; set; } = "list";

        // Used by the time-grid view to enable drag & drop only for events the current user owns (server enforces again)
        public int CurrentUserId { get; set; }
    }
}