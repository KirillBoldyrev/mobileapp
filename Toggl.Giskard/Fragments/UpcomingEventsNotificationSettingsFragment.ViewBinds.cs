using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;

namespace Toggl.Giskard.Fragments
{
    public sealed partial class UpcomingEventsNotificationSettingsFragment
    {
        private RecyclerView recyclerView;

        protected override void InitializeViews(View view)
        {
            recyclerView = view.FindViewById<RecyclerView>(Resource.Id.CalendarsRecyclerView);
        }
    }
}
