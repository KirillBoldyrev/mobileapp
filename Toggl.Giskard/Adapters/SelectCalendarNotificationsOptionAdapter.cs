using System;
using Android.Views;
using Toggl.Foundation.MvvmCross.ViewModels.Selectable;
using Toggl.Giskard.ViewHolders;

namespace Toggl.Giskard.Adapters
{
    public sealed class SelectCalendarNotificationsOptionAdapter : BaseRecyclerAdapter<SelectableCalendarNotificationsOptionViewModel>
    {
        protected override BaseRecyclerViewHolder<SelectableCalendarNotificationsOptionViewModel> CreateViewHolder(ViewGroup parent, LayoutInflater inflater, int viewType)
        {
            var inflatedView = inflater.Inflate(Resource.Layout.SelectCalendarNotificationsOptionItem, parent, false);
            return new SelectCalendarNotificationsOptionViewHolder(inflatedView);
        }
    }
}
