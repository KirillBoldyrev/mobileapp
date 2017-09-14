﻿using System;
using CoreGraphics;
using MvvmCross.Binding.BindingContext;
using MvvmCross.Binding.iOS;
using MvvmCross.iOS.Views;
using MvvmCross.iOS.Views.Presenters.Attributes;
using MvvmCross.Plugins.Color.iOS;
using MvvmCross.Plugins.Visibility;
using Toggl.Daneel.Extensions;
using Toggl.Foundation.MvvmCross.Converters;
using Toggl.Foundation.MvvmCross.Helper;
using Toggl.Foundation.MvvmCross.ViewModels;
using UIKit;

namespace Toggl.Daneel.ViewControllers
{
    [MvxRootPresentation(WrapInNavigationController = true)]
    public partial class MainViewController : MvxViewController<MainViewModel>
    {
        private const float animationAngle = 0.1f;
        private readonly UIButton reportsButton = new UIButton(new CGRect(0, 0, 40, 40));
        private readonly UIButton settingsButton = new UIButton(new CGRect(0, 0, 40, 40));
        private readonly UIImageView titleImage = new UIImageView(UIImage.FromBundle("togglLogo"));

        public MainViewController() 
            : base(nameof(MainViewController), null)
        {
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            SpiderBroImageView.Layer.AnchorPoint = new CGPoint(0.5f, 0);
            animateSpider();
            CurrentTimeEntryCard.Layer.BorderWidth = 1;
            CurrentTimeEntryCard.Layer.CornerRadius = 8;
            CurrentTimeEntryCard.Layer.BorderColor = Color.TimeEntriesLog.ButtonBorder.ToNativeColor().CGColor;
            CurrentTimeEntryElapsedTimeLabel.Font = CurrentTimeEntryElapsedTimeLabel.Font.GetMonospacedDigitFont();

            StartTimeEntryButton.Transform = CGAffineTransform.MakeScale(0.01f, 0.01f);
            reportsButton.SetImage(UIImage.FromBundle("icReports"), UIControlState.Normal);
            settingsButton.SetImage(UIImage.FromBundle("icSettings"), UIControlState.Normal);
           
            var visibilityConverter = new MvxVisibilityValueConverter();
            var timeSpanConverter = new TimeSpanToDurationValueConverter();
            var invertedVisibilityConverter = new MvxInvertedVisibilityValueConverter();

            var bindingSet = this.CreateBindingSet<MainViewController, MainViewModel>();

            //Commands
            bindingSet.Bind(settingsButton).To(vm => vm.OpenSettingsCommand);
            bindingSet.Bind(StopTimeEntryButton).To(vm => vm.StopTimeEntryCommand);
            bindingSet.Bind(StartTimeEntryButton).To(vm => vm.StartTimeEntryCommand);
            bindingSet.Bind(EditTimeEntryButton).To(vm => vm.EditTimeEntryCommand);
            bindingSet.Bind(CurrentTimeEntryCard)
                      .For(v => v.BindTap())
                      .To(vm => vm.EditTimeEntryCommand);

            //Visibility
            bindingSet.Bind(CurrentTimeEntryCard)
                      .For(v => v.BindVisibility())
                      .To(vm => vm.CurrentlyRunningTimeEntry)
                      .WithConversion(visibilityConverter);

            bindingSet.Bind(StartTimeEntryButton)
                      .For(v => v.BindVisibility())
                      .To(vm => vm.CurrentlyRunningTimeEntry)
                      .WithConversion(invertedVisibilityConverter);

            bindingSet.Bind(SpiderBroImageView)
                      .For(v => v.BindVisibility())
                      .To(vm => vm.SpiderIsVisible)
                      .WithConversion(visibilityConverter);

            //Text
            bindingSet.Bind(CurrentTimeEntryDescriptionLabel).To(vm => vm.CurrentlyRunningTimeEntry.Description);

            bindingSet.Bind(CurrentTimeEntryElapsedTimeLabel)
                      .To(vm => vm.CurrentTimeEntryElapsedTime)
                      .WithConversion(timeSpanConverter);

            bindingSet.Apply();
        }

        public override void ViewWillAppear(bool animated)
        {
            base.ViewWillAppear(animated);
    
            NavigationItem.TitleView = titleImage;
            NavigationItem.RightBarButtonItems = new[]
            {
                new UIBarButtonItem(UIBarButtonSystemItem.FixedSpace) { Width = -10 },
                new UIBarButtonItem(settingsButton),
                new UIBarButtonItem(reportsButton)
            };
        }

        internal UIView GetContainerFor(UIViewController viewController)
        {
            if (viewController is SuggestionsViewController)
                return SuggestionsContainer;
            
            if (viewController is TimeEntriesLogViewController)
                return TimeEntriesLogContainer;

            throw new ArgumentOutOfRangeException(nameof(viewController), "Received unexpected ViewController type");
        }

        internal void AnimatePlayButton()
        {
            AnimationExtensions.Animate(
                Animation.Timings.EnterTiming,
                Animation.Curves.EaseOut,
                () => StartTimeEntryButton.Transform = CGAffineTransform.MakeScale(1f, 1f)
            );
        }

        private void animateSpider()
        {
            SpiderBroImageView.Transform = CGAffineTransform.MakeRotation(-animationAngle);

            UIView.Animate(Animation.Timings.SpiderBro, 0, UIViewAnimationOptions.Autoreverse | UIViewAnimationOptions.Repeat, 
                () => SpiderBroImageView.Transform = CGAffineTransform.MakeRotation(animationAngle), animateSpider);
        }
    }
}

