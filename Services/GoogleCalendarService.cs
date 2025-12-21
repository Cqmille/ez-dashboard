using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Options;

namespace MamyDashboard.Services;

public class GoogleCalendarService
{
    private readonly CalendarService? _calendarService;
    private readonly string _calendarId;
    private readonly ILogger<GoogleCalendarService> _logger;

    public GoogleCalendarService(IOptions<AppSettings> settings, ILogger<GoogleCalendarService> logger)
    {
        _logger = logger;
        _calendarId = settings.Value.GoogleCalendarId;
        var credentialsPath = settings.Value.GoogleCredentialsPath;

        if (string.IsNullOrEmpty(_calendarId) || !File.Exists(credentialsPath))
        {
            _logger.LogWarning("Google Calendar non configuré. CalendarId: {CalendarId}, CredentialsPath: {Path}", 
                _calendarId, credentialsPath);
            return;
        }

        try
        {
            var credential = GoogleCredential
                .FromFile(credentialsPath)
                .CreateScoped(CalendarService.Scope.CalendarReadonly);

            _calendarService = new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "MamyDashboard"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'initialisation de Google Calendar");
        }
    }

    public async Task<object> GetEventsAsync()
    {
        if (_calendarService == null)
        {
            return new
            {
                today = new[] { new { time = "⚠️", title = "Calendrier non configuré" } },
                tomorrow = Array.Empty<object>()
            };
        }

        var now = DateTime.Now.Date;
        var tomorrow = now.AddDays(1);
        var dayAfter = now.AddDays(2);

        var request = _calendarService.Events.List(_calendarId);
        request.TimeMinDateTimeOffset = now;
        request.TimeMaxDateTimeOffset = dayAfter;
        request.SingleEvents = true;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

        var events = await request.ExecuteAsync();

        var todayEvents = new List<object>();
        var tomorrowEvents = new List<object>();

        foreach (var evt in events.Items ?? Enumerable.Empty<Event>())
        {
            var start = evt.Start.DateTime ?? DateTime.Parse(evt.Start.Date);
            var eventObj = new
            {
                time = evt.Start.DateTime.HasValue 
                    ? start.ToString("HH'h'mm") 
                    : "Journée",
                title = evt.Summary ?? "(Sans titre)"
            };

            if (start.Date == now)
                todayEvents.Add(eventObj);
            else if (start.Date == tomorrow)
                tomorrowEvents.Add(eventObj);
        }

        return new { today = todayEvents, tomorrow = tomorrowEvents };
    }
}
