using Android.OS;
using Android.Views;
using Toggl.Foundation.MvvmCross.ViewModels.Calendar;
using Toggl.Giskard.Extensions.Reactive;
using Toggl.Multivac.Extensions;

namespace Toggl.Giskard.Fragments
{
    public sealed partial class CalendarPermissionDeniedFragment : ReactiveDialogFragment<CalendarPermissionDeniedViewModel>
    {
        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate(Resource.Layout.CalendarPermissionDeniedFragment, container, false);
            InitializeViews(view);

            continueButton.Rx().Tap()
                .Subscribe(ViewModel.Close.Inputs)
                .DisposedBy(DisposeBag);

            allowAccessButton.Rx().Tap()
                .Subscribe(ViewModel.EnableAccess.Inputs)
                .DisposedBy(DisposeBag);

            return view;
        }
    }
}
