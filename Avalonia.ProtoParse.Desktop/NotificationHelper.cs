using System;
using Avalonia.Controls.Notifications;
using Notification = Ursa.Controls.Notification;
using WindowNotificationManager = Ursa.Controls.WindowNotificationManager;

namespace Avalonia.ProtoParse.Desktop;

internal static class NotificationHelper
{
    public static WindowNotificationManager? Notification;

    public static async System.Threading.Tasks.Task ShowAsync(Notification notification, TimeSpan? expiration = null)
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            Notification?.Show(notification,
                showIcon: true,
                showClose: true,
                expiration: notification.Expiration,
                type: notification.Type));
    }

    /// <summary>
    /// 普通消息
    /// </summary>
    /// <param name="message"></param>
    /// <param name="title"></param>
    /// <param name="expiration"></param>
    public static async System.Threading.Tasks.Task ShowInfoAsync(
        string message = "",
        string title = "通知",
        TimeSpan? expiration = null)
    {
        expiration ??= TimeSpan.FromSeconds(5);

        await ShowAsync(new Notification(
            title: title,
            expiration: expiration.Value,
            content: message,
            type: NotificationType.Information));
    }

    /// <summary>
    /// 成功消息
    /// </summary>
    /// <param name="message"></param>
    /// <param name="title"></param>
    /// <param name="expiration"></param>
    public static async System.Threading.Tasks.Task ShowSuccessAsync(
        string message = "",
        string title = "通知",
        TimeSpan? expiration = null)
    {
        expiration ??= TimeSpan.FromSeconds(5);
        await ShowAsync(new Notification(
            title: title,
            expiration: expiration.Value,
            content: message,
            type: NotificationType.Success));
    }

    /// <summary>
    /// 错误消息
    /// </summary>
    /// <param name="message"></param>
    /// <param name="title"></param>
    /// <param name="expiration"></param>
    public static async System.Threading.Tasks.Task ShowErrorAsync(
        string message = "",
        string title = "异常",
        TimeSpan? expiration = null)
    {
        expiration ??= TimeSpan.FromSeconds(5);

        await ShowAsync(new Notification(
            title: title,
            content: message,
            expiration: expiration.Value,
            type: NotificationType.Error));
    }
}