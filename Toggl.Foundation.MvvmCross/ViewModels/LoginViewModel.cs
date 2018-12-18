using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using MvvmCross.ViewModels;
using Toggl.Foundation.DataSources;
using Toggl.Foundation.Exceptions;
using Toggl.Foundation.Login;
using Toggl.Foundation.MvvmCross.Extensions;
using Toggl.Foundation.MvvmCross.Parameters;
using Toggl.Foundation.MvvmCross.Services;
using Toggl.Foundation.Services;
using Toggl.Multivac;
using Toggl.Multivac.Extensions;
using Toggl.Multivac.Extensions.Reactive;
using Toggl.PrimeRadiant.Settings;
using Toggl.Ultrawave.Exceptions;

namespace Toggl.Foundation.MvvmCross.ViewModels
{
    [Preserve(AllMembers = true)]
    public sealed class LoginViewModel : MvxViewModel<CredentialsParameter>
    {
        public enum ShakeTarget
        {
            None = 0,
            Email = 1,
            Password = 2
        }

        public enum State
        {
            Email,
            EmailAndPassword
        }

        private readonly IUserAccessManager userAccessManager;
        private readonly IOnboardingStorage onboardingStorage;
        private readonly IForkingNavigationService navigationService;
        private readonly IPasswordManagerService passwordManagerService;
        private readonly IErrorHandlingService errorHandlingService;
        private readonly ILastTimeUsageStorage lastTimeUsageStorage;
        private readonly ITimeService timeService;
        private readonly ISchedulerProvider schedulerProvider;

        private readonly Subject<ShakeTarget> shakeSubject = new Subject<ShakeTarget>();
        private readonly BehaviorSubject<bool> isPasswordMaskedSubject = new BehaviorSubject<bool>(true);
        private readonly int errorCountBeforeShowingContactSupportSuggestion = 2;
        private readonly Exception invalidEmailException = new Exception(Resources.EnterValidEmail);
        private readonly Exception incorrectPasswordException = new Exception(Resources.IncorrectEmailOrPassword);
        private readonly BehaviorSubject<State> state = new BehaviorSubject<State>(State.Email);

        public bool IsPasswordManagerAvailable { get; }
        public BehaviorRelay<string> EmailRelay { get; } = new BehaviorRelay<string>(string.Empty);
        public BehaviorRelay<string> PasswordRelay { get; } = new BehaviorRelay<string>(string.Empty);
        public IObservable<bool> IsLoggingIn { get; }
        public IObservable<ShakeTarget> Shake { get; }
        public IObservable<bool> IsPasswordMasked { get; }
        public IObservable<bool> IsShowPasswordButtonVisible { get; }
        public IObservable<bool> SuggestContactSupport { get; }
        public UIAction Login { get; }
        public IObservable<Unit> ClearPasswordScreenError { get; }
        public UIAction LoginWithGoogle { get; }
        public UIAction TogglePasswordVisibility { get; }
        public UIAction ForgotPassword { get; }
        public UIAction LoginWithEmail { get; }
        public UIAction Back { get; }
        public UIAction ContactUs { get; }
        public IObservable<Unit> ClearEmailScreenError { get; }
        public IObservable<bool> IsEmailFieldEdittable { get; }
        public IObservable<bool> IsInSecondScreen { get; }

        public LoginViewModel(
            IUserAccessManager userAccessManager,
            IOnboardingStorage onboardingStorage,
            IForkingNavigationService navigationService,
            IPasswordManagerService passwordManagerService,
            IErrorHandlingService errorHandlingService,
            ILastTimeUsageStorage lastTimeUsageStorage,
            ITimeService timeService,
            ISchedulerProvider schedulerProvider)
        {
            Ensure.Argument.IsNotNull(userAccessManager, nameof(userAccessManager));
            Ensure.Argument.IsNotNull(onboardingStorage, nameof(onboardingStorage));
            Ensure.Argument.IsNotNull(navigationService, nameof(navigationService));
            Ensure.Argument.IsNotNull(passwordManagerService, nameof(passwordManagerService));
            Ensure.Argument.IsNotNull(errorHandlingService, nameof(errorHandlingService));
            Ensure.Argument.IsNotNull(lastTimeUsageStorage, nameof(lastTimeUsageStorage));
            Ensure.Argument.IsNotNull(timeService, nameof(timeService));
            Ensure.Argument.IsNotNull(schedulerProvider, nameof(schedulerProvider));

            this.timeService = timeService;
            this.userAccessManager = userAccessManager;
            this.onboardingStorage = onboardingStorage;
            this.navigationService = navigationService;
            this.passwordManagerService = passwordManagerService;
            this.errorHandlingService = errorHandlingService;
            this.lastTimeUsageStorage = lastTimeUsageStorage;
            this.schedulerProvider = schedulerProvider;

            var isEmailValid = EmailRelay
                .Select(email => Email.From(email).IsValid);

            var isPasswordValid = PasswordRelay
                .Select(password => Password.From(password).IsValid);

            var isEmailState = state.Select(s => s == State.Email);
            var isReturningToEmail = isEmailState.Skip(1);

            Shake = shakeSubject
                .AsDriver(ShakeTarget.None, this.schedulerProvider);

            IsPasswordMasked = isPasswordMaskedSubject
                .DistinctUntilChanged()
                .AsDriver(schedulerProvider);

            IsShowPasswordButtonVisible = PasswordRelay
                .Select(password => password.Length > 1)
                .DistinctUntilChanged()
                .AsDriver(schedulerProvider);

            IsPasswordManagerAvailable = passwordManagerService.IsAvailable;

            Login = UIAction.FromObservable(login);

            ClearPasswordScreenError = Observable
                .CombineLatest(isEmailValid, isPasswordValid, CommonFunctions.And)
                .Merge(isReturningToEmail)
                .Where(CommonFunctions.Identity)
                .SelectUnit()
                .ObserveOn(schedulerProvider.MainScheduler);

            LoginWithGoogle = UIAction.FromObservable(loginWithGoogle);

            var isLoggingIn = Observable
                .CombineLatest(Login.Executing, LoginWithGoogle.Executing, CommonFunctions.Or);

            IsLoggingIn = isLoggingIn
                .AsDriver(schedulerProvider);

            ForgotPassword = UIAction.FromObservable(() => forgotPassword(EmailRelay.Value), isLoggingIn.Invert());

            TogglePasswordVisibility =
                UIAction.FromAction(() => isPasswordMaskedSubject.OnNext(!isPasswordMaskedSubject.Value));

            LoginWithEmail = UIAction.FromObservable(loginWithEmail);

            ClearEmailScreenError = isEmailValid
                .Where(CommonFunctions.Identity)
                .SelectUnit()
                .ObserveOn(schedulerProvider.MainScheduler);

            SuggestContactSupport = Observable.Merge(Login.Errors, LoginWithGoogle.Errors)
                .Skip(errorCountBeforeShowingContactSupportSuggestion)
                .SelectValue(true)
                .StartWith(false)
                .AsDriver(false, schedulerProvider);

            var emailRelatedExceptions = Login
                .Errors
                .Select(e => e == incorrectPasswordException || e == invalidEmailException);

            IsEmailFieldEdittable = Observable
                .CombineLatest(isEmailState, emailRelatedExceptions, CommonFunctions.Or)
                .StartWith(false)
                .AsDriver(schedulerProvider);

            Back = UIAction.FromAction(back, isLoggingIn.Invert());

            IsInSecondScreen = state
                .Select(s => s == State.EmailAndPassword)
                .AsObservable()
                .AsDriver(schedulerProvider);

            ContactUs = UIAction.FromAsync(contactUs);
        }

        public override void Prepare(CredentialsParameter parameter)
        {
            EmailRelay.Accept(parameter.Email.ToString());
            PasswordRelay.Accept(parameter.Password.ToString());
        }

        private IObservable<Unit> loginWithEmail()
        {
            if (!Email.From(EmailRelay.Value).IsValid)
            {
                return Observable.Return(Unit.Default).Do(_ =>
                {
                    shakeSubject.OnNext(ShakeTarget.Email);
                    throw invalidEmailException;
                });
            }

            return Observable.Return(Unit.Default).Do(_ => state.OnNext(State.EmailAndPassword));
        }

        private IObservable<Unit> loginWithGoogle()
            => userAccessManager
                .LoginWithGoogle()
                .SelectMany(onLoginSuccessfully)
                .Catch<Unit, Exception>(e => handleException(e))
                .ObserveOn(schedulerProvider.MainScheduler);

        private IObservable<Unit> login()
        {
            var password = Password.From(PasswordRelay.Value);
            var email = Email.From(EmailRelay.Value);

            if (!email.IsValid)
            {
                return Observable.Return(Unit.Default).Do(_ =>
                {
                    shakeSubject.OnNext(ShakeTarget.Email);
                    throw invalidEmailException;
                });
            }

            if (!password.IsValid)
            {
                return Observable.Return(Unit.Default).Do(_ =>
                {
                    shakeSubject.OnNext(ShakeTarget.Password);
                    throw new Exception(Resources.PasswordTooShort);
                });
            }

            return userAccessManager
                .Login(email, password)
                .SelectMany(onLoginSuccessfully)
                .Catch<Unit, Exception>(handleException)
                .ObserveOn(schedulerProvider.MainScheduler);
        }

        private IObservable<Unit> handleException(Exception e)
        {
            if (errorHandlingService.TryHandleDeprecationError(e))
            {
                return Observable.Return(Unit.Default);
            }

            switch (e)
            {
                case UnauthorizedException _:
                    return Observable.Throw<Unit>(incorrectPasswordException);
                case GoogleLoginException googleEx:
                    return Observable.Throw<Unit>(new Exception(googleEx.Message));
                default:
                    return Observable.Throw<Unit>(new Exception(Resources.GenericLoginError));
            }
        }

        private IObservable<Unit> onLoginSuccessfully(ITogglDataSource dataSource)
            => Observable.Defer(async () =>
                {
                    lastTimeUsageStorage.SetLogin(timeService.CurrentDateTime);
                    await dataSource.StartSyncing();
                    onboardingStorage.SetIsNewUser(false);
                    return navigationService.ForkNavigate<MainTabBarViewModel, MainViewModel>().ToObservable();
                });

        private IObservable<Unit> forgotPassword(string email)
        {
            var emailParam = EmailParameter.With(Email.From(email));
            return navigationService
                .Navigate<ForgotPasswordViewModel, EmailParameter, EmailParameter>(emailParam)
                .ToObservable()
                .Do(result => PasswordRelay.Accept(result.ToString()))
                .SelectUnit()
                .ObserveOn(schedulerProvider.MainScheduler);
        }

        private void back()
        {
            switch (state.Value)
            {
                case State.Email:
                    navigationService.Close(this);
                    break;
                case State.EmailAndPassword:
                    state.OnNext(State.Email);
                    PasswordRelay.Accept(string.Empty);
                    break;
            }
        }

        private Task contactUs() =>
            navigationService
                .Navigate<BrowserViewModel, BrowserParameters>(
                    BrowserParameters.WithUrlAndTitle(Resources.ContactUsUrl, Resources.ContactUs));
    }
}
