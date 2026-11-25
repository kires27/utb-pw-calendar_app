using CalendarApp.Application.Abstraction;
using CalendarApp.Domain.Abstraction;
using CalendarApp.Domain.Entities;
using CalendarApp.Domain.Repositories;

namespace CalendarApp.Application.Implementation
{
    public class EventAppService : IEventAppService
    {
        private readonly IEventRepository _eventRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly IUserAppService _userAppService;

        public EventAppService(IEventRepository eventRepository, INotificationRepository notificationRepository, IUserAppService userAppService)
        {
            _eventRepository = eventRepository;
            _notificationRepository = notificationRepository;
            _userAppService = userAppService;
        }

        public Task<IList<Event>> SelectAllAsync()
        {
            return _eventRepository.GetAllAsync();
        }

        public Task<IList<Event>> SelectByOwnerAsync(int ownerId)
        {
            return _eventRepository.GetByOwnerAsync(ownerId);
        }

        public Task<Event?> GetByIdAsync(int id)
        {
            return _eventRepository.GetByIdAsync(id);
        }

        public Task<EventOccurrenceException?> GetOccurrenceExceptionAsync(int masterEventId, DateTime originalDate)
        {
            return _eventRepository.GetOccurrenceExceptionAsync(masterEventId, originalDate);
        }

        public Task UpsertOccurrenceExceptionAsync(EventOccurrenceException exception)
        {
            return _eventRepository.UpsertOccurrenceExceptionAsync(exception);
        }

        public async Task<bool> MoveOccurrenceAsync(int eventId, DateTime? occurrenceDate, DateTime newStart, DateTime newEnd, bool allDay)
        {
            var @event = await _eventRepository.GetByIdAsync(eventId);
            if (@event == null)
            {
                return false;
            }

            // End must not be before start; only non-all-day events may be zero-length
            if (newEnd < newStart || (!allDay && newEnd == newStart))
            {
                throw new ArgumentException("End time must be after start time.");
            }

            if (@event.IsRecurring)
            {
                if (!occurrenceDate.HasValue)
                {
                    return false;
                }

                // Preserve text/color overrides; a drag only changes schedule fields
                var existing = await _eventRepository.GetOccurrenceExceptionAsync(eventId, occurrenceDate.Value.Date);

                await _eventRepository.UpsertOccurrenceExceptionAsync(new EventOccurrenceException
                {
                    MasterEventId = eventId,
                    OriginalDate = occurrenceDate.Value.Date,
                    IsCancelled = false,
                    Title = existing?.Title,
                    Description = existing?.Description,
                    StartTime = newStart,
                    EndTime = newEnd,
                    AllDay = allDay,
                    Location = existing?.Location,
                    ReminderTime = existing?.ReminderTime,
                    Color = existing?.Color,
                    Status = existing?.Status,
                    CreatedAt = existing?.CreatedAt ?? DateTime.Now,
                    UpdatedAt = DateTime.Now
                });
            }
            else
            {
                await _eventRepository.UpdateScheduleAsync(eventId, newStart, newEnd, allDay);
            }

            return true;
        }

        public async Task CreateAsync(Event @event)
        {
            ValidateEventBusinessRules(@event);

            await _eventRepository.AddAsync(@event);

            var ownerAttendee = new EventAttendee
            {
                EventId = @event.Id,
                UserId = @event.OwnerId,
                Role = AttendeeRole.Owner,
                Status = AttendeeStatus.Accepted,
                UpdatedAt = DateTime.UtcNow
            };

            await _eventRepository.AddAttendeeAsync(ownerAttendee);
        }

        public Task<bool> DeleteAsync(int id)
        {
            return _eventRepository.DeleteAsync(id);
        }

        public async Task UpdateAsync(Event @event)
        {
            ValidateEventBusinessRules(@event);
            await _eventRepository.UpdateAsync(@event);
        }

        private static void ValidateEventBusinessRules(Event @event)
        {
            // End must not be before start; only non-all-day events may be zero-length
            if (@event.EndTime < @event.StartTime || (!@event.AllDay && @event.EndTime == @event.StartTime))
            {
                throw new ArgumentException("End time must be after start time for a non-all-day event.");
            }

            // Recurrence may not end before the day the series starts.
            if (@event.RecurrenceEnd.HasValue && @event.RecurrenceEnd.Value < @event.StartTime.Date)
            {
                throw new ArgumentException("Recurrence end cannot be before the event start.");
            }
        }

        public async Task<bool> InviteUsersToEvent(int eventId, List<int> userIds)
        {
            var @event = await _eventRepository.GetByIdAsync(eventId);
            if (@event == null)
            {
                return false;
            }

            var owner = await _userAppService.GetUserByIdAsync(@event.OwnerId);

            foreach (var userId in userIds)
            {
                var existingAttendee = await _eventRepository.GetAttendeeAsync(eventId, userId);

                if (existingAttendee == null)
                {
                    await _eventRepository.AddAttendeeAsync(new EventAttendee
                    {
                        EventId = eventId,
                        UserId = userId,
                        Status = AttendeeStatus.Invited,
                        Role = AttendeeRole.Participant,
                        UpdatedAt = DateTime.UtcNow
                    });

                    await _notificationRepository.AddAsync(new Notification
                    {
                        UserId = userId,
                        EventId = eventId,
                        Type = NotificationType.EventInvite,
                        Message = $"{owner?.UserName ?? "Someone"} invited you to '{@event.Title}'",
                        IsRead = false,
                        NotifyAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            return true;
        }

        public async Task<bool> RemoveAttendeeFromEvent(int eventId, int userId)
        {
            // The owner cannot be removed from their own event
            var attendee = await _eventRepository.GetAttendeeAsync(eventId, userId);
            if (attendee == null || attendee.Role == AttendeeRole.Owner)
            {
                return false;
            }

            return await _eventRepository.RemoveAttendeeAsync(eventId, userId);
        }

        public async Task<bool> DeclineEventAsync(int eventId, int userId)
        {
            var attendee = await _eventRepository.GetAttendeeAsync(eventId, userId, includeEvent: true);

            if (attendee == null || attendee.Role == AttendeeRole.Owner || attendee.Status == AttendeeStatus.Declined)
            {
                return false;
            }

            await _eventRepository.SetAttendeeStatusAsync(eventId, userId, AttendeeStatus.Declined);

            // Decline also closes any unread invitations for this event
            await _notificationRepository.MarkInvitesReadAsync(userId, eventId);

            var decliner = await _userAppService.GetUserByIdAsync(userId);

            await _notificationRepository.AddAsync(new Notification
            {
                UserId = attendee.Event!.OwnerId,
                EventId = eventId,
                Type = NotificationType.EventUpdate,
                Message = $"{decliner?.UserName ?? "Someone"} declined '{attendee.Event.Title}'",
                IsRead = false,
                NotifyAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            return true;
        }
    }
}
