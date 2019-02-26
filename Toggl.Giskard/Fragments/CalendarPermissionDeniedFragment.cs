using Android.OS;
using Android.Views;
using Toggl.Foundation.MvvmCross.ViewModels.Calendar;

namespace Toggl.Giskard.Fragments
{
    public sealed partial class CalendarPermissionDeniedFragment : ReactiveDialogFragment<CalendarPermissionDeniedViewModel>
    {
        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate(Resource.Layout.CalendarPermissionDeniedFragment, container, false);
            return view;
        }
    }
}
