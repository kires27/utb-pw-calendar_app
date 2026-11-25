using CalendarApp.Application.Abstraction;
using CalendarApp.Application.Models;
using CalendarApp.Domain.Abstraction;
using CalendarApp.Domain.Entities;
using CalendarApp.Domain.Repositories;

namespace CalendarApp.Application.Implementation
{
    public class NotificationAppService : INotificationAppService
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IUserAppService _userAppService;

        public NotificationAppService(INotificationRepository notificationRepository, IEventRepository eventRepository, IUserAppService userAppService)
        {
            _notificationRepository = notificationRepository;
            _eventRepository = eventRepository;
            _userAppService = userAppService;
        }

        public Task<IList<Notification>> GetUserNotificationsAsync(int userId)
        {
            return _notificationRepository.GetByUserAsync(userId);
        }

        public async Task<IList<AdminNotificationViewModel>> SelectAllForAdminAsync()
        {
            var notifications = await _notificationRepository.GetAllAsync();

            var userNames = (await _userAppService.GetUsersByIdsAsync(
                    notifications.Select(n => n.UserId).Distinct()))
                .ToDictionary(u => u.Key, u => u.Value.UserName);

            return notifications
                .Select(n => new AdminNotificationViewModel
                {
                    Notification = n,
                    RecipientUserName = userNames.TryGetValue(n.UserId, out var userName)
                        ? userName
                        : $"User {n.UserId}"
                })
                .ToList();
        }

        public Task<bool> DeleteAsync(int id)
        {
            return _notificationRepository.DeleteAsync(id);
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _notificationRepository.CountUnreadAsync(userId);
        }

        public async Task<bool> MarkAsReadAsync(int notificationId, int userId)
        {
            var notification = await _notificationRepository.GetByIdForUserAsync(notificationId, userId);
            if (notification == null)
            {
                return false;
            }

            notification.IsRead = true;
            notification.UpdatedAt = DateTime.UtcNow;
            await _notificationRepository.UpdateAsync(notification);
            return true;
        }

        public async Task<bool> AcceptInvitationAsync(int notificationId, int userId)
        {
            var notification = await _notificationRepository.GetByIdForUserAsync(notificationId, userId);
            if (notification?.Event == null)
            {
                return false;
            }

            var eventAttendee = await _eventRepository.GetAttendeeAsync(notification.EventId!.Value, userId);
            if (eventAttendee?.Status == AttendeeStatus.Declined)
            {
                return false;
            }

            if (eventAttendee != null)
            {
                await _eventRepository.SetAttendeeStatusAsync(notification.EventId.Value, userId, AttendeeStatus.Accepted);
            }

            notification.IsRead = true;
            notification.UpdatedAt = DateTime.UtcNow;
            await _notificationRepository.UpdateAsync(notification);
            return true;
        }

        public async Task<bool> DeclineInvitationAsync(int notificationId, int userId)
        {
            var notification = await _notificationRepository.GetByIdForUserAsync(notificationId, userId);
            if (notification?.Event == null)
            {
                return false;
            }

            var eventAttendee = await _eventRepository.GetAttendeeAsync(notification.EventId!.Value, userId);
            if (eventAttendee != null)
            {
                await _eventRepository.SetAttendeeStatusAsync(notification.EventId.Value, userId, AttendeeStatus.Declined);
            }

            notification.IsRead = true;
            notification.UpdatedAt = DateTime.UtcNow;
            await _notificationRepository.UpdateAsync(notification);
            return true;
        }
    }
}
