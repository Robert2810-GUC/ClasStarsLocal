using System;

namespace My.ClasStars.Components
{
    public enum ToastLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class ToastMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Message { get; set; }
        public ToastLevel Level { get; set; }
        public int Duration { get; set; } = 4000;
    }

    /// <summary>
    /// Provides a simple global toast notification surface.
    /// </summary>
    public class ToastService
    {
        public event Action<ToastMessage> OnShow;

        public void ShowToast(string message, ToastLevel level = ToastLevel.Info, int duration = 4000)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var toast = new ToastMessage
            {
                Message = message,
                Level = level,
                Duration = Math.Clamp(duration, 2000, 10000)
            };

            OnShow?.Invoke(toast);
        }
    }
}
