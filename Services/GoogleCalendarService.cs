using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

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
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
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
            var events = ParseICalEvents(icsContent);

            var now = DateTime.Now.Date;
            var currentTime = DateTime.Now;
            var tomorrow = now.AddDays(1);

            var todayEvents = events
                .Where(e => e.Start.Date == now)
                .OrderBy(e => e.Start)
                .Select(e => new
                {
                    time = e.IsAllDay ? "Journée" : e.Start.ToString("HH'h'mm"),
                    title = e.Summary,
                    // Barrer seulement quand l'événement est terminé (pas juste commencé)
                    isPast = !e.IsAllDay && e.End < currentTime
                })
                .ToList();

            var tomorrowEvents = events
                .Where(e => e.Start.Date == tomorrow)
                .OrderBy(e => e.Start)
                .Select(e => new
                {
                    time = e.IsAllDay ? "Journée" : e.Start.ToString("HH'h'mm"),
                    title = e.Summary
                })
                .ToList();

            return new { today = todayEvents, tomorrow = tomorrowEvents };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération du calendrier iCal");
            return new
            {
                today = new[] { new { time = "⚠️", title = "Erreur de chargement" } },
                tomorrow = Array.Empty<object>()
            };
        }
    }

    private List<CalendarEvent> ParseICalEvents(string icsContent)
    {
        var events = new List<CalendarEvent>();
        var eventBlocks = Regex.Matches(icsContent, @"BEGIN:VEVENT(.*?)END:VEVENT", RegexOptions.Singleline);

        foreach (Match block in eventBlocks)
        {
            var content = block.Groups[1].Value;

            var summary = ExtractValue(content, "SUMMARY");
            if (string.IsNullOrEmpty(summary))
                summary = "(Sans titre)";

            var dtStart = ExtractDateTimeValue(content, "DTSTART");
            if (dtStart == null)
                continue;

            var dtEnd = ExtractDateTimeValue(content, "DTEND");
            // Si pas de DTEND, utiliser Start + 1 heure par défaut (ou fin de journée pour all-day)
            DateTime endTime;
            if (dtEnd != null)
            {
                endTime = dtEnd.Value.DateTime;
            }
            else if (dtStart.Value.IsAllDay)
            {
                endTime = dtStart.Value.DateTime.AddDays(1);
            }
            else
            {
                endTime = dtStart.Value.DateTime.AddHours(1);
            }

            events.Add(new CalendarEvent
            {
                Summary = summary,
                Start = dtStart.Value.DateTime,
                End = endTime,
                IsAllDay = dtStart.Value.IsAllDay
            });
        }

        return events;
    }

    private string? ExtractValue(string content, string property)
    {
        // Handle properties that may have parameters (like SUMMARY;LANGUAGE=fr:)
        var pattern = $@"^{property}[^:]*:(.+?)(?:\r?\n(?![ \t])|$)";
        var match = Regex.Match(content, pattern, RegexOptions.Multiline);
        if (match.Success)
        {
            var value = match.Groups[1].Value.Trim();
            // Handle line continuations (lines starting with space or tab)
            value = Regex.Replace(value, @"\r?\n[ \t]", "");
            // Unescape common escaped characters
            value = value.Replace("\\n", "\n").Replace("\\,", ",").Replace("\\;", ";").Replace("\\\\", "\\");
            return value;
        }
        return null;
    }

    private (DateTime DateTime, bool IsAllDay)? ExtractDateTimeValue(string content, string property = "DTSTART")
    {
        // Try with timezone or as datetime
        var patterns = new[]
        {
            $@"{property}(?:;TZID=[^:]+)?:(\d{{8}}T\d{{6}})",  // DateTime format: 20231225T140000
            $@"{property}(?:;VALUE=DATE)?:(\d{{8}})(?!\d)",   // Date only format: 20231225
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(content, pattern);
            if (match.Success)
            {
                var value = match.Groups[1].Value;
                if (value.Length == 8) // Date only
                {
                    if (DateTime.TryParseExact(value, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date))
                        return (date, true);
                }
                else if (value.Length == 15) // DateTime
                {
                    if (DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss", null, System.Globalization.DateTimeStyles.None, out var dateTime))
                        return (dateTime, false);
                }
            }
        }

        return null;
    }

    private class CalendarEvent
    {
        public string Summary { get; set; } = "";
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public bool IsAllDay { get; set; }
    }
}
