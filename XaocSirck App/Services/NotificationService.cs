using Microsoft.Toolkit.Uwp.Notifications;

namespace XaocSirck_App.Services;

public static class NotificationService
{
    public static void ShowUpdatingNotification()
    {
        new ToastContentBuilder()
            .AddText("Updating XaocSirck")
            .Show();
    }
}
