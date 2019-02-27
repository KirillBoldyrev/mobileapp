using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using MvvmCross.Navigation;
using MvvmCross.ViewModels;
using Toggl.Foundation.Extensions;
using Toggl.Foundation.MvvmCross.ViewModels.Selectable;
using Toggl.Foundation.Services;
using Toggl.Multivac;
using Toggl.Multivac.Extensions;
using Toggl.PrimeRadiant.Settings;

namespace Toggl.Foundation.MvvmCross.ViewModels.Settings
{
    [Preserve(AllMembers = true)]
    public sealed class UpcomingEventsNotificationSettingsViewModel : MvxViewModelResult<Unit>
    {
        private readonly IMvxNavigationService navigationService;
        private readonly IUserPreferences userPreferences;
        private readonly IRxActionFactory rxActionFactory;

        public IList<SelectableCalendarNotificationsOptionViewModel> AvailableOptions { get; }

        public InputAction<SelectableCalendarNotificationsOptionViewModel> SelectOption { get; }
        public UIAction Close { get; }

        public UpcomingEventsNotificationSettingsViewModel(
            IMvxNavigationService navigationService,
            IUserPreferences userPreferences,
            IRxActionFactory rxActionFactory)
        {
            Ensure.Argument.IsNotNull(navigationService, nameof(navigationService));
            Ensure.Argument.IsNotNull(userPreferences, nameof(userPreferences));
            Ensure.Argument.IsNotNull(rxActionFactory, nameof(rxActionFactory));

            this.navigationService = navigationService;
            this.userPreferences = userPreferences;
            this.rxActionFactory = rxActionFactory;

            var options = new[] {
                CalendarNotificationsOption.Disabled,
                CalendarNotificationsOption.WhenEventStarts,
                CalendarNotificationsOption.FiveMinutes,
                CalendarNotificationsOption.TenMinutes,
                CalendarNotificationsOption.FifteenMinutes,
                CalendarNotificationsOption.ThirtyMinutes,
                CalendarNotificationsOption.OneHour
            };

            AvailableOptions = options.Select(toSelectableOption).ToList();

            SelectOption = rxActionFactory.FromAction<SelectableCalendarNotificationsOptionViewModel>(onSelectOption);
            Close = rxActionFactory.FromAction(() => navigationService.Close(this, Unit.Default));
        }

        public override async Task Initialize()
        {
            await base.Initialize();
            var selectedOption = await userPreferences.CalendarNotificationsSettings().FirstAsync();
            AvailableOptions.ForEach(opt => opt.Selected = opt.Option == selectedOption);
        }

        private void onSelectOption(SelectableCalendarNotificationsOptionViewModel selectableOption)
        {
            var enabled = selectableOption.Option != CalendarNotificationsOption.Disabled;

            userPreferences.SetCalendarNotificationsEnabled(enabled);

            if (enabled)
                userPreferences.SetTimeSpanBeforeCalendarNotifications(selectableOption.Option.Duration());

            navigationService.Close(this, Unit.Default);
        }

        private SelectableCalendarNotificationsOptionViewModel toSelectableOption(CalendarNotificationsOption option)
            => new SelectableCalendarNotificationsOptionViewModel(option, false);
    }
}
