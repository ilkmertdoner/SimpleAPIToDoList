using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;

namespace TaskManagerApi.Service
{
    public class GoogleCalendarService
    {
        private readonly string[] Scopes = { CalendarService.Scope.Calendar };
        private readonly string ApplicationName = "TaskManager";

        public async Task AddTaskToUserCalendarAsync(string email, string title, string description, DateTime dueDate)
        {
            GoogleCredential credential;
            using (var stream = new FileStream("google-credentials.json", FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(Scopes);
            }

            var service = new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName
            });

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

            EventsResource.InsertRequest request = service.Events.Insert(newEvent, email);
            request.SendUpdates = EventsResource.InsertRequest.SendUpdatesEnum.All;
            await request.ExecuteAsync();
        }
    }
}
