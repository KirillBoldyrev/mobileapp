using Android.Views;
using Android.Widget;

namespace Toggl.Giskard.Fragments
{
    public sealed partial class CalendarPermissionDeniedFragment
    {
        private Button continueButton;
        private Button allowAccessButton;

        protected override void InitializeViews(View view)
        {
            continueButton = view.FindViewById<Button>(Resource.Id.Continue);
            allowAccessButton = view.FindViewById<Button>(Resource.Id.AllowAccess);
        }
    }
}
