using System;
using Foundation;
using Toggl.Foundation.Extensions;
using Toggl.Foundation.MvvmCross.ViewModels.Selectable;
using Toggl.Multivac;
using UIKit;

namespace Toggl.Daneel.Cells.Settings
{
    public sealed partial class UpcomingEventsOptionCell : BaseTableViewCell<SelectableCalendarNotificationsOptionViewModel>
    {
        public static readonly string Identifier = nameof(UpcomingEventsOptionCell);
        public static readonly NSString Key = new NSString(nameof(UpcomingEventsOptionCell));
        public static readonly UINib Nib;

        static UpcomingEventsOptionCell()
        {
            Nib = UINib.FromName(nameof(UpcomingEventsOptionCell), NSBundle.MainBundle);
        }

        public UpcomingEventsOptionCell(IntPtr handle) : base(handle)
        {
        }

        public override void AwakeFromNib()
        {
            base.AwakeFromNib();
            SelectionStyle = UITableViewCellSelectionStyle.None;
        }

        protected override void UpdateView()
        {
            SelectedImageView.Hidden = !Item.Selected;
            TitleLabel.Text = Item.Option.Title();
        }
    }
}
