using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.VisualBasic;

namespace TaskManagerApi.Service
{
    public class GoogleCalendarService
    {
        private readonly string[] Scopes = { CalendarService.Scope.Calendar };
        private readonly string ApplicationName = "TaskManager";

        private CalendarService GetCalendarService()
        {
            GoogleCredential credential;

            using (var stream = new FileStream("google-credentials.json", FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(Scopes);
            }

            return new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName
            });
        }

        public async Task<string> AddTaskToUserCalendarAsync(string userEmail, string title, string description, DateTime dueDate)
        {
            var service = GetCalendarService();

            Event newEvent = new Event()
            {
                Summary = title,
                Description = description,
                Start = new EventDateTime()
                {
                    DateTime = dueDate,
                    TimeZone = "Europe/Istanbul"
                },

                End = new EventDateTime()
                {
                    DateTime = dueDate.Date.AddHours(1),
                    TimeZone = "Europe/Istanbul"
                },
            };

            EventsResource.InsertRequest request = service.Events.Insert(newEvent, userEmail);
            var createdEvent = await request.ExecuteAsync();

            return createdEvent.Id;
        }

        public async Task UpdateTaskInUserCalendarAsync(string userEmail, string eventId, string title, 
            string description, DateTime dueDate)
        {
            var service = GetCalendarService();

            Event existingEvent = await service.Events.Get(userEmail, eventId).ExecuteAsync();

            string startStr = dueDate.ToString("yyyy-MM-ddTHH:mm:ss");
            string endStr = dueDate.AddHours(1).ToString("yyyy-MM-ddTHH:mm:ss");

            existingEvent.Summary = title;
            existingEvent.Description = description;
            existingEvent.Start = new EventDateTime()
            {
                DateTimeRaw = startStr,
                TimeZone = "Europe/Istanbul",
            };
            existingEvent.End = new EventDateTime()
            {
                DateTimeRaw = endStr,
                TimeZone = "Europe/Istanbul",
            };

            EventsResource.UpdateRequest request = service.Events.Update(existingEvent, userEmail, eventId);
            await request.ExecuteAsync();
        }

        public async Task DeleteTaskFromUserCalendarAsync(string userEmail, string eventId)
        {
            var service = GetCalendarService();
            EventsResource.DeleteRequest request = service.Events.Delete(userEmail, eventId);
            await request.ExecuteAsync();
        }
    }
}
