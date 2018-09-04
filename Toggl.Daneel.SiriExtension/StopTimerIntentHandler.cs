﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using Toggl.Ultrawave;
using Toggl.Ultrawave.Network;
using Toggl.Daneel.Intents;
using Foundation;
using Toggl.Multivac.Models;
using SiriExtension.Models;
using SiriExtension.Exceptions;

namespace SiriExtension
{
    public class StopTimerIntentHandler : StopTimerIntentHandling
    {
        #if USE_PRODUCTION_API
        private const ApiEnvironment environment = ApiEnvironment.Production;
        #else
        private const ApiEnvironment environment = ApiEnvironment.Staging;
        #endif

        private static TogglApi togglApi;

        public StopTimerIntentHandler()
        {
        }

        public override void ConfirmStopTimer(StopTimerIntent intent, Action<StopTimerIntentResponse> completion)
        {
            StopTimerIntentHandler.togglApi = getTogglAPI();
            if (togglApi == null)
            {
                completion(new StopTimerIntentResponse(StopTimerIntentResponseCode.FailureNoApiToken, null));
                return;
            }

            completion(new StopTimerIntentResponse(StopTimerIntentResponseCode.Ready, null));
        }

        public override void HandleStopTimer(StopTimerIntent intent, Action<StopTimerIntentResponse> completion)
        {
            StopTimerIntentHandler.togglApi.TimeEntries.GetAll()                
                    .Select(getRunningTimeEntry)
                    .SelectMany(stopTimeEntry)
                    .Subscribe(
                    te =>
                    {
                        /*
                        var response = StopTimerIntentResponse.SuccessIntentResponseWithEntry_description(
                            te.Description,
                            $"(te.Duration) seconds"
                        );
                        */
                        // Once Xamarin's bug if fixed we have to use tha above response instead of this one.
                        var response = new StopTimerIntentResponse(StopTimerIntentResponseCode.Success, null);
                        completion(response);
                    },
                    exception =>
                    {
                        completion(responseFromException(exception));
                    });
        }

        private TogglApi getTogglAPI()
        {
            var apiToken = getAPIToken();
            if (apiToken == null)
            {
                return null;
            }

            var version = NSBundle.MainBundle.InfoDictionary["CFBundleShortVersionString"].ToString();
            var userAgent = new UserAgent("Daneel", $"{version}.SiriExtension");
            return new TogglApi(new ApiConfiguration(environment, Credentials.WithApiToken(apiToken), userAgent));
        }

        private string getAPIToken()
        {
            var bundleId = NSBundle.MainBundle.BundleIdentifier;
            bundleId = bundleId.Substring(0, bundleId.LastIndexOf("."));
            var userDefaults = new NSUserDefaults($"group.{bundleId}.extensions", NSUserDefaultsType.SuiteName);
            var apiToken = userDefaults.StringForKey("api-token");

            if (apiToken == null)
            {
                return null;
            }

            return apiToken;
        }

        private ITimeEntry getRunningTimeEntry(IList<ITimeEntry> timeEntries)
        {
            try
            {
                var runningTE = timeEntries.Where(te => te.Duration == null).First();
                return runningTE;
            } catch {
                throw new NoRunningEntryException();
            }
        }

        private IObservable<ITimeEntry> stopTimeEntry(ITimeEntry timeEntry)
        {
            var duration = (long)(DateTime.Now - timeEntry.Start).TotalSeconds;
            return StopTimerIntentHandler.togglApi.TimeEntries.Update(
                TimeEntry.from(timeEntry).with(duration)
            );
        }

        private StopTimerIntentResponse responseFromException(Exception exception)
        {            
            if (exception is NoRunningEntryException)
                return new StopTimerIntentResponse(StopTimerIntentResponseCode.FailureNoRunningEntry, null);

            return new StopTimerIntentResponse(StopTimerIntentResponseCode.Failure, null);
        }
    }
}
