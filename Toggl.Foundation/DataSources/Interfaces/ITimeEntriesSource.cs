using System;
using Toggl.Foundation.DTOs;
using Toggl.PrimeRadiant;
using Toggl.PrimeRadiant.Models;

namespace Toggl.Foundation.DataSources
{
    public interface ITimeEntriesSource : IRepository<IDatabaseTimeEntry>
    {
        IObservable<IDatabaseTimeEntry> CurrentlyRunningTimeEntry { get; }

        IObservable<IDatabaseTimeEntry> TimeEntryCreated { get; }

        IObservable<IDatabaseTimeEntry> TimeEntryUpdated { get; }

        IObservable<IDatabaseTimeEntry> TimeEntryDeleted { get; }

        IObservable<bool> IsEmpty { get; }

        IObservable<IDatabaseTimeEntry> Start(StartTimeEntryDTO dto);
         IObservable<IDatabaseTimeEntry> Stop(DateTimeOffset stopTime);

        IObservable<IDatabaseTimeEntry> Update(EditTimeEntryDto dto);
    }
}
