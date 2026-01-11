using Microsoft.Extensions.Options;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace MamyDashboard.Services;

public class GoogleCalendarService
{
    private readonly HttpClient _httpClient;
    private readonly string _iCalUrl;
    private readonly ILogger<GoogleCalendarService> _logger;

    public GoogleCalendarService(IOptions<AppSettings> settings, ILogger<GoogleCalendarService> logger)
    {
        _logger = logger;
        _iCalUrl = settings.Value.ICalUrl;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<object> GetEventsAsync()
    {
        if (string.IsNullOrEmpty(_iCalUrl))
        {
            return new
            {
                today = new[] { new { time = "⚠️", title = "URL iCal non configurée" } },
                tomorrow = Array.Empty<object>()
            };
        }

        try
        {
            var icsContent = await _httpClient.GetStringAsync(_iCalUrl);
            var calendar = Calendar.Load(icsContent);

            if (calendar == null)
            {
                return new
                {
                    today = new[] { new { time = "⚠️", title = "Erreur de chargement du calendrier" } },
                    tomorrow = Array.Empty<object>()
                };
            }

            var now = DateTime.Now;
            var todayDate = now.Date;
            var tomorrowDate = todayDate.AddDays(1);
            var rangeEnd = todayDate.AddDays(2);

            // Récupération des occurrences (y compris les événements récurrents)
            var startSearch = new CalDateTime(todayDate);
            var endSearch = new CalDateTime(rangeEnd);

            var occurrences = calendar
                .GetOccurrences(startSearch, endSearch)
                .OrderBy(o => o.Period.StartTime.Value)
                .ToList();

            var todayEvents = occurrences
                .Where(o => GetLocalDate(o.Period.StartTime) == todayDate)
                .Select(o => MapEvent(o, now))
                .Where(e => e != null)
                .ToList();

            var tomorrowEvents = occurrences
                .Where(o => GetLocalDate(o.Period.StartTime) == tomorrowDate)
                .Select(o => MapEvent(o, now))
                .Where(e => e != null)
                .ToList();

            return new { today = todayEvents, tomorrow = tomorrowEvents };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération du calendrier");
            return new
            {
                today = new[] { new { time = "⚠️", title = "Erreur de chargement" } },
                tomorrow = Array.Empty<object>()
            };
        }
    }

    private DateTime GetLocalDate(IDateTime dateTime)
    {
        if (dateTime == null || dateTime.Value == DateTime.MinValue)
            return DateTime.MinValue;

        // Convertir en heure locale si nécessaire
        var dt = dateTime.Value;

        // Si c'est une date UTC, la convertir en heure locale
        if (dt.Kind == DateTimeKind.Utc)
        {
            dt = dt.ToLocalTime();
        }
        else if (dt.Kind == DateTimeKind.Unspecified)
        {
            // Si le fuseau horaire n'est pas spécifié, on suppose qu'il s'agit d'une heure locale
            dt = DateTime.SpecifyKind(dt, DateTimeKind.Local);
        }

        return dt;
    }

    private object? MapEvent(Occurrence occurrence, DateTime now)
    {
        try
        {
            var evt = occurrence.Source as CalendarEvent;
            if (evt == null)
                return null;

            // Récupérer les dates de début et fin
            var startTime = GetLocalDate(occurrence.Period.StartTime);
            var endTime = GetLocalDate(occurrence.Period.EndTime);

            if (startTime == DateTime.MinValue)
                return null;

            // Déterminer si c'est un événement "toute la journée"
            bool isAllDay = evt.IsAllDay;

            // Créer l'événement
            return new
            {
                time = isAllDay ? "Journée" : startTime.ToString("HH'h'mm"),
                title = evt.Summary ?? "(Sans titre)",
                // Un événement est passé uniquement s'il n'est pas "toute la journée" ET si l'heure actuelle est après la fin
                isPast = !isAllDay && now > endTime,
                // Un événement est "en cours" s'il n'est pas "toute la journée" ET si l'heure actuelle est entre le début et la fin
                isOngoing = !isAllDay && now >= startTime && now <= endTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erreur lors du mapping d'un événement");
            return null;
        }
    }
}
