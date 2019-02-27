using System.Reactive.Linq;
using Android.OS;
using Android.Support.V7.Widget;
using Android.Views;
using Toggl.Foundation.MvvmCross.ViewModels.Selectable;
using Toggl.Foundation.MvvmCross.ViewModels.Settings;
using Toggl.Giskard.Adapters;
using Toggl.Giskard.Extensions.Reactive;
using Toggl.Multivac;
using Toggl.Multivac.Extensions;

namespace Toggl.Giskard.Fragments
{
    public sealed partial class UpcomingEventsNotificationSettingsFragment : ReactiveDialogFragment<UpcomingEventsNotificationSettingsViewModel>
    {
        private SelectCalendarNotificationsOptionAdapter adapter;

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var contextThemeWrapper = new ContextThemeWrapper(Activity, Resource.Style.TogglDialog);
            var wrappedInflater = inflater.CloneInContext(contextThemeWrapper);

            var view = wrappedInflater.Inflate(Resource.Layout.UpcomingEventsNotificationSettingsFragment, container, false);
            InitializeViews(view);

            setupRecyclerView();

            adapter
                .ItemTapObservable
                .Subscribe(ViewModel.SelectOption.Inputs)
                .DisposedBy(DisposeBag);

            return view;
        }

        public override void OnResume()
        {
            base.OnResume();
            var layoutParams = Dialog.Window.Attributes;
            layoutParams.Width = ViewGroup.LayoutParams.MatchParent;
            layoutParams.Height = ViewGroup.LayoutParams.WrapContent;
            Dialog.Window.Attributes = layoutParams;
        }

        private void setupRecyclerView()
        {
            adapter = new SelectCalendarNotificationsOptionAdapter();
            adapter.Items = ViewModel.AvailableOptions;
            recyclerView.SetAdapter(adapter);
            recyclerView.SetLayoutManager(new LinearLayoutManager(Context));
        }
    }
}
