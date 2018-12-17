﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FsCheck;
using Microsoft.Reactive.Testing;
using NSubstitute;
using NUnit.Framework;
using Toggl.Foundation.DataSources;
using Toggl.Foundation.Exceptions;
using Toggl.Foundation.MvvmCross.Parameters;
using Toggl.Foundation.MvvmCross.ViewModels;
using Toggl.Foundation.Tests.Extensions;
using Toggl.Foundation.Tests.Generators;
using Toggl.Multivac;
using Toggl.Multivac.Extensions;
using Toggl.PrimeRadiant.Settings;
using Toggl.Ultrawave.Exceptions;
using Toggl.Ultrawave.Network;
using Xunit;
using Unit = System.Reactive.Unit;

namespace Toggl.Foundation.Tests.MvvmCross.ViewModels
{
    public sealed class LoginViewModelTests
    {
        public abstract class LoginViewModelTest : BaseViewModelTests<LoginViewModel>
        {
            protected Email ValidEmail { get; } = Email.From("person@company.com");
            protected Email InvalidEmail { get; } = Email.From("this is not an email");

            protected Password ValidPassword { get; } = Password.From("T0t4lly s4afe p4$$");
            protected Password InvalidPassword { get; } = Password.From("123");

            protected ILastTimeUsageStorage LastTimeUsageStorage { get; } = Substitute.For<ILastTimeUsageStorage>();

            protected override LoginViewModel CreateViewModel()
                => new LoginViewModel(
                    UserAccessManager,
                    OnboardingStorage,
                    NavigationService,
                    PasswordManagerService,
                    ErrorHandlingService,
                    LastTimeUsageStorage,
                    TimeService,
                    SchedulerProvider);
        }

        public sealed class TheConstructor : LoginViewModelTest
        {
            [Xunit.Theory, LogIfTooSlow]
            [ConstructorData]
            public void ThrowsIfAnyOfTheArgumentsIsNull(
                bool useUserAccessManager,
                bool useOnboardingStorage,
                bool userNavigationService,
                bool usePasswordManagerService,
                bool useApiErrorHandlingService,
                bool useLastTimeUsageStorage,
                bool useTimeService,
                bool useSchedulerProvider)
            {
                var userAccessManager = useUserAccessManager ? UserAccessManager : null;
                var onboardingStorage = useOnboardingStorage ? OnboardingStorage : null;
                var navigationService = userNavigationService ? NavigationService : null;
                var passwordManagerService = usePasswordManagerService ? PasswordManagerService : null;
                var apiErrorHandlingService = useApiErrorHandlingService ? ErrorHandlingService : null;
                var lastTimeUsageStorage = useLastTimeUsageStorage ? LastTimeUsageStorage : null;
                var timeService = useTimeService ? TimeService : null;
                var schedulerProvider = useSchedulerProvider ? SchedulerProvider : null;

                Action tryingToConstructWithEmptyParameters =
                    () => new LoginViewModel(userAccessManager,
                                             onboardingStorage,
                                             navigationService,
                                             passwordManagerService,
                                             apiErrorHandlingService,
                                             lastTimeUsageStorage,
                                             timeService,
                                             schedulerProvider);

                tryingToConstructWithEmptyParameters
                    .Should().Throw<ArgumentNullException>();
            }
        }

        public sealed class TheIsPasswordManagerAvailableProperty : LoginViewModelTest
        {
            [FsCheck.Xunit.Property]
            public void ReturnsWhetherThePasswordManagerIsAvailable(bool isAvailable)
            {
                PasswordManagerService.IsAvailable.Returns(isAvailable);

                var viewModel = CreateViewModel();

                viewModel.IsPasswordManagerAvailable.Should().Be(isAvailable);
            }
        }

        public sealed class ClearClearEmailScreenError : LoginViewModelTest
        {
            [Fact, LogIfTooSlow]
            public void DoesNotEmitWhenEmailIsValid()
            {
                ViewModel.EmailRelay.Accept(InvalidEmail.ToString());
                var observer = TestScheduler.CreateObserver<Unit>();
                ViewModel.ClearEmailScreenError.Subscribe(observer);

                TestScheduler.Start();

                observer.Messages.Should().BeEmpty();
            }

            [Fact, LogIfTooSlow]
            public void EmitsElementWhenEmailIsValid()
            {
                ViewModel.EmailRelay.Accept(ValidEmail.ToString());
                var observer = TestScheduler.CreateObserver<Unit>();
                ViewModel.ClearEmailScreenError.Subscribe(observer);

                TestScheduler.Start();

                observer.Messages.Should().HaveCount(1);
            }

            [Fact, LogIfTooSlow]
            public void EmitsElementWhenEmailTransitionFromInvalidToValid()
            {
                var observer = TestScheduler.CreateObserver<Unit>();
                ViewModel.ClearEmailScreenError.Subscribe(observer);
                ViewModel.EmailRelay.Accept(InvalidEmail.ToString());
                ViewModel.EmailRelay.Accept(ValidEmail.ToString());

                TestScheduler.Start();

                observer.Messages.Should().HaveCount(1);
            }
        }

        public sealed class ClearPasswordScreenError : LoginViewModelTest
        {
            [Xunit.Theory]
            [InlineData(false, false, false)]
            [InlineData(false, true, false)]
            [InlineData(true, false, false)]
            [InlineData(true, true, true)]
            public void EmitAppropriateValue(bool emailValid, bool passwordValid, bool shouldEmit)
            {
                ViewModel.EmailRelay.Accept(emailValid ? ValidEmail.ToString() : InvalidEmail.ToString());
                ViewModel.PasswordRelay.Accept(passwordValid ? ValidPassword.ToString() : InvalidPassword.ToString());
                var observer = TestScheduler.CreateObserver<Unit>();
                ViewModel.ClearPasswordScreenError.Subscribe(observer);

                TestScheduler.Start();

                observer.Messages.Should().HaveCount(shouldEmit ? 1 : 0);
            }
        }

        public sealed class TheLoginWithEmailAction : LoginViewModelTest
        {
            [Fact, LogIfTooSlow]
            public void EmitsErrorWhenEmailIsInvalid()
            {
                ViewModel.EmailRelay.Accept(InvalidEmail.ToString());
                var observer = TestScheduler.CreateObserver<Exception>();
                ViewModel.LoginWithEmail.Errors.Subscribe(observer);

                TestScheduler.Start();
                ViewModel.LoginWithEmail.Execute();

                observer.Messages.Should().HaveCount(1);
                observer.Messages.Last().Value.Value.Message.Should().Be(Resources.EnterValidEmail);
            }

            [Fact, LogIfTooSlow]
            public void EmitsElementWhenEmailIsValid()
            {
                ViewModel.EmailRelay.Accept(ValidEmail.ToString());
                var observer = TestScheduler.CreateObserver<Unit>();
                ViewModel.LoginWithEmail.Elements.Subscribe(observer);

                TestScheduler.Start();
                ViewModel.LoginWithEmail.Execute();

                observer.Messages.Should().HaveCount(1);
            }
        }

        public sealed class TheLoginAction : LoginViewModelTest
        {
            [Fact, LogIfTooSlow]
            public void CallsTheUserAccessManagerWhenTheEmailAndPasswordAreValid()
            {
                ViewModel.EmailRelay.Accept(ValidEmail.ToString());
                ViewModel.PasswordRelay.Accept(ValidPassword.ToString());

                ViewModel.Login.Execute();

                UserAccessManager.Received().Login(Arg.Is(ValidEmail), Arg.Is(ValidPassword));
            }

            [Fact, LogIfTooSlow]
            public void EmitErrorWhenEmailIsInvalid()
            {
                var observer = TestScheduler.CreateObserver<Exception>();
                ViewModel.Login.Errors.Subscribe(observer);
                ViewModel.EmailRelay.Accept(InvalidEmail.ToString());

                ViewModel.Login.Execute();
                TestScheduler.Start();

                observer.Messages.Last().Value.Value.Message.Should().Be(Resources.EnterValidEmail);
            }

            [Fact, LogIfTooSlow]
            public void EmitErrorWhenPasswordIsTooShort()
            {
                var observer = TestScheduler.CreateObserver<Exception>();
                ViewModel.Login.Errors.Subscribe(observer);
                ViewModel.EmailRelay.Accept(ValidEmail.ToString());
                ViewModel.PasswordRelay.Accept(InvalidPassword.ToString());

                ViewModel.Login.Execute();
                TestScheduler.Start();

                observer.Messages.Last().Value.Value.Message.Should().Be(Resources.PasswordTooShort);
            }

            public sealed class WhenLoginSucceeds : LoginViewModelTest
            {
                public WhenLoginSucceeds()
                {
                    ViewModel.EmailRelay.Accept(ValidEmail.ToString());
                    ViewModel.PasswordRelay.Accept(ValidPassword.ToString());
                    UserAccessManager.Login(Arg.Any<Email>(), Arg.Any<Password>())
                        .Returns(Observable.Return(DataSource));
                }

                [Fact, LogIfTooSlow]
                public async Task StartsSyncing()
                {
                    ViewModel.Login.Execute();

                    await DataSource.Received().StartSyncing();
                }

                [Fact, LogIfTooSlow]
                public void SetsIsNewUserToFalse()
                {
                    ViewModel.Login.Execute();

                    OnboardingStorage.Received().SetIsNewUser(false);
                }

                [Fact, LogIfTooSlow]
                public void NavigatesToTheTimeEntriesViewModel()
                {
                    ViewModel.Login.Execute();

                    NavigationService.Received().ForkNavigate<MainTabBarViewModel, MainViewModel>();
                }

                [FsCheck.Xunit.Property]
                public void SavesTheTimeOfLastLogin(DateTimeOffset now)
                {
                    TimeService.CurrentDateTime.Returns(now);
                    var viewModel = CreateViewModel();
                    viewModel.EmailRelay.Accept(ValidEmail.ToString());
                    viewModel.PasswordRelay.Accept(ValidPassword.ToString());

                    viewModel.Login.Execute();

                    LastTimeUsageStorage.Received().SetLogin(Arg.Is(now));
                }
            }

            public sealed class WhenLoginFails : LoginViewModelTest
            {
                public WhenLoginFails()
                {
                    ViewModel.EmailRelay.Accept(ValidEmail.ToString());
                    ViewModel.PasswordRelay.Accept(ValidPassword.ToString());
                }

                [Fact, LogIfTooSlow]
                public void DoesNotNavigate()
                {
                    UserAccessManager.Login(Arg.Any<Email>(), Arg.Any<Password>())
                        .Returns(Observable.Throw<ITogglDataSource>(new Exception()));

                    ViewModel.Login.Execute();

                    NavigationService.DidNotReceive().Navigate<MainViewModel>();
                }

                [Fact, LogIfTooSlow]
                public void SetsTheErrorMessageToIncorrectEmailOrPasswordWhenReceivedUnauthorizedException()
                {
                    var observer = TestScheduler.CreateObserver<Exception>();
                    ViewModel.Login.Errors.Subscribe(observer);
                    var exception = new UnauthorizedException(
                        Substitute.For<IRequest>(), Substitute.For<IResponse>());
                    UserAccessManager.Login(Arg.Any<Email>(), Arg.Any<Password>())
                        .Returns(Observable.Throw<ITogglDataSource>(exception));

                    ViewModel.Login.Execute();
                    TestScheduler.Start();

                    observer.Messages.Last().Value.Value.Message.Should().Be(Resources.IncorrectEmailOrPassword);
                }

                [Fact, LogIfTooSlow]
                public void SetsTheErrorMessageToGenericLoginErrorForAnyOtherException()
                {
                    var exception = new Exception();
                    var observer = TestScheduler.CreateObserver<Exception>();
                    ViewModel.Login.Errors.Subscribe(observer);
                    UserAccessManager.Login(Arg.Any<Email>(), Arg.Any<Password>())
                        .Returns(Observable.Throw<ITogglDataSource>(exception));

                    ViewModel.Login.Execute();

                    TestScheduler.Start();
                    observer.Messages.Last().Value.Value.Message.Should().Be(Resources.GenericLoginError);
                }

                [Fact, LogIfTooSlow]
                public void DoesNothingWhenErrorHandlingServiceHandlesTheException()
                {
                    var exception = new Exception();
                    var observer = TestScheduler.CreateObserver<Exception>();
                    ViewModel.Login.Errors.Subscribe(observer);
                    UserAccessManager.Login(Arg.Any<Email>(), Arg.Any<Password>())
                        .Returns(Observable.Throw<ITogglDataSource>(exception));
                    ErrorHandlingService.TryHandleDeprecationError(Arg.Any<Exception>())
                        .Returns(true);

                    ViewModel.Login.Execute();
                    TestScheduler.Start();

                    observer.Messages.Should().BeEmpty();
                }
            }
        }

        public sealed class TheLoginWithGoogleAction : LoginViewModelTest
        {
            [Fact, LogIfTooSlow]
            public void CallsTheUserAccessManager()
            {
                ViewModel.LoginWithGoogle.Execute();

                UserAccessManager.Received().LoginWithGoogle();
            }

            public sealed class WhenLoginSucceeds : LoginViewModelTest
            {
                public WhenLoginSucceeds()
                {
                    UserAccessManager.LoginWithGoogle()
                        .Returns(Observable.Return(DataSource));
                }

                [Fact, LogIfTooSlow]
                public void NavigatesToTheTimeEntriesViewModelWhenTheLoginSucceeds()
                {
                    UserAccessManager.LoginWithGoogle()
                        .Returns(Observable.Return(Substitute.For<ITogglDataSource>()));

                    ViewModel.LoginWithGoogle.Execute();

                    NavigationService.Received().ForkNavigate<MainTabBarViewModel, MainViewModel>();
                }

                [Fact, LogIfTooSlow]
                public async Task StartsSyncing()
                {
                    UserAccessManager.LoginWithGoogle()
                        .Returns(Observable.Return(DataSource));
                    ViewModel.LoginWithGoogle.Execute();

                    await DataSource.Received().StartSyncing();
                }

                [Fact, LogIfTooSlow]
                public void SetsIsNewUserToFalse()
                {
                    ViewModel.LoginWithGoogle.Execute();

                    OnboardingStorage.Received().SetIsNewUser(false);
                }

                [FsCheck.Xunit.Property]
                public void SavesTheTimeOfLastLogin(DateTimeOffset now)
                {
                    TimeService.CurrentDateTime.Returns(now);
                    var viewModel = CreateViewModel();
                    viewModel.EmailRelay.Accept(ValidEmail.ToString());
                    viewModel.PasswordRelay.Accept(ValidPassword.ToString());

                    viewModel.Login.Execute();

                    LastTimeUsageStorage.Received().SetLogin(Arg.Is(now));
                }
            }

            public sealed class WhenLoginFails : LoginViewModelTest
            {
                [Fact, LogIfTooSlow]
                public void DoesNotNavigateWhenTheLoginFails()
                {
                    UserAccessManager.LoginWithGoogle()
                        .Returns(Observable.Throw<ITogglDataSource>(new GoogleLoginException(false)));

                    ViewModel.LoginWithGoogle.Execute();

                    NavigationService.DidNotReceive().ForkNavigate<MainTabBarViewModel, MainViewModel>();
                }

                [Fact, LogIfTooSlow]
                public void ShouldForwardGoogleExceptionMessageWhenItIsNotCancelled()
                {
                    var exception = new GoogleLoginException(false, "Nahh");
                    UserAccessManager.LoginWithGoogle()
                        .Returns(Observable.Throw<ITogglDataSource>(exception));
                    var observer = TestScheduler.CreateObserver<Exception>();
                    ViewModel.LoginWithGoogle.Errors.Subscribe(observer);

                    ViewModel.LoginWithGoogle.Execute();
                    TestScheduler.Start();

                    observer.Messages.Last().Value.Value.Message.Should().Be(exception.Message);
                }
            }
        }

        public sealed class TheTogglePasswordVisibilityMethod : LoginViewModelTest
        {
            [Fact, LogIfTooSlow]
            public void SetsTheIsPasswordMaskedToFalseWhenItIsTrue()
            {
                var observer = TestScheduler.CreateObserver<bool>();

                ViewModel.IsPasswordMasked.Subscribe(observer);
                ViewModel.TogglePasswordVisibility.Execute();

                TestScheduler.Start();
                observer.Messages.AssertEqual(
                    ReactiveTest.OnNext(1, true),
                    ReactiveTest.OnNext(2, false)
                );
            }

            [Fact, LogIfTooSlow]
            public void SetsTheIsPasswordMaskedToTrueWhenItIsFalse()
            {
                var observer = TestScheduler.CreateObserver<bool>();

                ViewModel.IsPasswordMasked.Subscribe(observer);
                ViewModel.TogglePasswordVisibility.Execute();

                ViewModel.TogglePasswordVisibility.Execute();

                TestScheduler.Start();
                observer.Messages.AssertEqual(
                    ReactiveTest.OnNext(1, true),
                    ReactiveTest.OnNext(2, false),
                    ReactiveTest.OnNext(3, true)
                );
            }
        }

        public sealed class ThePrepareMethod : LoginViewModelTest
        {
            [FsCheck.Xunit.Property]
            public void SetsTheEmail(NonEmptyString emailString)
            {
                var viewModel = CreateViewModel();
                var email = Email.From(emailString.Get);
                var password = Password.Empty;
                var parameter = CredentialsParameter.With(email, password);
                var expectedValues = new[] { Email.Empty.ToString(), email.ToString() };
                var actualValues = new List<string>();
                viewModel.EmailRelay.Subscribe(actualValues.Add);

                viewModel.Prepare(parameter);

                TestScheduler.Start();
                CollectionAssert.AreEqual(expectedValues, actualValues);
            }

            [FsCheck.Xunit.Property]
            public void SetsThePassword(NonEmptyString passwordString)
            {
                var viewModel = CreateViewModel();
                var email = Email.Empty;
                var password = Password.From(passwordString.Get);
                var parameter = CredentialsParameter.With(email, password);
                var expectedValues = new[] { Password.Empty.ToString(),  password.ToString() };
                var actualValues = new List<string>();
                viewModel.PasswordRelay.Subscribe(actualValues.Add);

                viewModel.Prepare(parameter);

                TestScheduler.Start();
                CollectionAssert.AreEqual(expectedValues, actualValues);
            }
        }

        public sealed class TheEmailFieldEdittable : LoginViewModelTest
        {
            [Fact, LogIfTooSlow]
            public void ShouldStartWithFalse()
            {
                var observer = TestScheduler.CreateObserver<bool>();
                ViewModel.IsEmailFieldEdittable.Subscribe(observer);

                TestScheduler.Start();

                observer.LastValue().Should().BeFalse();
            }

            [Fact, LogIfTooSlow]
            public void EmitsTrueWhenReceiveUnauthorizedException()
            {
                ViewModel.EmailRelay.Accept(ValidEmail.ToString());
                ViewModel.PasswordRelay.Accept(ValidPassword.ToString());
                var exception = new UnauthorizedException(
                    Substitute.For<IRequest>(), Substitute.For<IResponse>());
                UserAccessManager.Login(Arg.Any<Email>(), Arg.Any<Password>())
                    .Returns(Observable.Throw<ITogglDataSource>(exception));
                var observer = TestScheduler.CreateObserver<bool>();
                ViewModel.IsEmailFieldEdittable.Subscribe(observer);

                ViewModel.Login.Execute();
                TestScheduler.Start();

                observer.LastValue().Should().BeTrue();
            }
        }

        public sealed class TheShakeTargetsProperty : LoginViewModelTest
        {
            [Fact, LogIfTooSlow]
            public void ShouldEmitEmailWhenEmailIsInvalidWhenContinueToPasswordScreen()
            {
                ViewModel.EmailRelay.Accept(InvalidEmail.ToString());
                ViewModel.PasswordRelay.Accept(ValidPassword.ToString());
                var observer = TestScheduler.CreateObserver<LoginViewModel.ShakeTarget>();
                ViewModel.Shake.Subscribe(observer);

                ViewModel.LoginWithEmail.Execute();
                TestScheduler.Start();

                observer.LastValue().Should().Be(LoginViewModel.ShakeTarget.Email);
            }

            [Fact, LogIfTooSlow]
            public void ShouldEmitEmailWhenEmailIsInvalidWhenLoginWithEmail()
            {
                ViewModel.EmailRelay.Accept(InvalidEmail.ToString());
                ViewModel.PasswordRelay.Accept(ValidPassword.ToString());
                var observer = TestScheduler.CreateObserver<LoginViewModel.ShakeTarget>();
                ViewModel.Shake.Subscribe(observer);

                ViewModel.Login.Execute();
                TestScheduler.Start();

                observer.LastValue().Should().Be(LoginViewModel.ShakeTarget.Email);
            }

            [Fact, LogIfTooSlow]
            public void ShouldEmitPasswordWhenPasswordIsInvalid()
            {
                ViewModel.EmailRelay.Accept(ValidEmail.ToString());
                ViewModel.PasswordRelay.Accept(InvalidPassword.ToString());
                var observer = TestScheduler.CreateObserver<LoginViewModel.ShakeTarget>();
                ViewModel.Shake.Subscribe(observer);

                ViewModel.Login.Execute();
                TestScheduler.Start();

                observer.LastValue().Should().Be(LoginViewModel.ShakeTarget.Password);
            }

            [Fact, LogIfTooSlow]
            public void ShouldNotEmitWhenEmailAndPasswordAreValid()
            {
                ViewModel.EmailRelay.Accept(ValidEmail.ToString());
                ViewModel.PasswordRelay.Accept(ValidPassword.ToString());
                var observer = TestScheduler.CreateObserver<LoginViewModel.ShakeTarget>();
                ViewModel.Shake.Subscribe(observer);

                ViewModel.Login.Execute();
                TestScheduler.Start();

                observer.Messages.Should().BeEmpty();
            }
        }

        public sealed class TheBackAction : LoginViewModelTest
        {
            [Fact, LogIfTooSlow]
            public void ShouldCallNavigationServiceCloseWhenInTheFirstScreen()
            {
                ViewModel.Back.Execute();
                TestScheduler.Start();

                NavigationService.Received().Close(ViewModel);
            }

            [Fact, LogIfTooSlow]
            public void ShouldBeDisabledIfLoggingInWithEmail()
            {
                ViewModel.EmailRelay.Accept(ValidEmail.ToString());
                ViewModel.PasswordRelay.Accept(ValidPassword.ToString());
                UserAccessManager.Login(Arg.Any<Email>(), Arg.Any<Password>())
                    .Returns(Observable.Never<ITogglDataSource>());
                var observer = TestScheduler.CreateObserver<bool>();
                ViewModel.Back.Enabled.Subscribe(observer);

                ViewModel.Login.Execute();
                TestScheduler.Start();

                observer.LastValue().Should().BeFalse();
            }

            [Fact, LogIfTooSlow]
            public void ShouldBeDisabledIfLoggingInWithGoogle()
            {
                UserAccessManager.LoginWithGoogle()
                    .Returns(Observable.Never<ITogglDataSource>());
                var observer = TestScheduler.CreateObserver<bool>();
                ViewModel.Back.Enabled.Subscribe(observer);

                ViewModel.LoginWithGoogle.Execute();
                TestScheduler.Start();

                observer.LastValue().Should().BeFalse();
            }

            [Fact, LogIfTooSlow]
            public void ShouldBeEnabledByDefault()
            {
                var observer = TestScheduler.CreateObserver<bool>();
                ViewModel.Back.Enabled.Subscribe(observer);

                TestScheduler.Start();

                observer.LastValue().Should().BeTrue();
            }

            [Fact, LogIfTooSlow]
            public void ShouldClearThePassword()
            {
                ViewModel.EmailRelay.Accept(ValidEmail.ToString());
                var observer = TestScheduler.CreateObserver<string>();
                ViewModel.PasswordRelay.Accept("somePassword");
                ViewModel.PasswordRelay.Subscribe(observer);

                ViewModel.LoginWithEmail.Execute();
                ViewModel.Back.Execute();
                TestScheduler.Start();

                observer.Messages.AssertEqual(
                   ReactiveTest.OnNext(0, "somePassword"),
                   ReactiveTest.OnNext(0, string.Empty)
                );
            }
        }

        public sealed class TheForgotPasswordAction : LoginViewModelTest
        {
            [Fact, LogIfTooSlow]
            public void ShouldBeDisabledWhenIsLoggingIn()
            {
                ViewModel.EmailRelay.Accept(ValidEmail.ToString());
                ViewModel.PasswordRelay.Accept(ValidPassword.ToString());
                UserAccessManager.Login(Arg.Any<Email>(), Arg.Any<Password>())
                    .Returns(Observable.Never<ITogglDataSource>());
                var observer = TestScheduler.CreateObserver<bool>();
                ViewModel.ForgotPassword.Enabled.Subscribe(observer);

                ViewModel.Login.Execute();
                TestScheduler.Start();

                observer.LastValue().Should().BeFalse();
            }

            [Fact, LogIfTooSlow]
            public void ShouldBeEnabledByDefault()
            {
                var observer = TestScheduler.CreateObserver<bool>();
                ViewModel.ForgotPassword.Enabled.Subscribe(observer);

                TestScheduler.Start();

                observer.LastValue().Should().BeTrue();
            }

            [Fact, LogIfTooSlow]
            public void ShouldCallsNavigationService()
            {
                ViewModel.EmailRelay.Accept(ValidEmail.ToString());
                ViewModel.PasswordRelay.Accept(ValidPassword.ToString());

                ViewModel.ForgotPassword.Execute();

                NavigationService.Received()
                    .Navigate<ForgotPasswordViewModel, EmailParameter, EmailParameter>(
                        Arg.Any<EmailParameter>());
            }
        }

        public sealed class TheContactUsAction : LoginViewModelTest
        {
            [Fact, LogIfTooSlow]
            public async Task OpensTheBrowserWithTheAppropriateTitle()
            {
                await ViewModel.ContactUs.Execute();

                await NavigationService.Received().Navigate<BrowserViewModel, BrowserParameters>(
                    Arg.Is<BrowserParameters>(parameter => parameter.Title == Resources.ContactUs)
                );
            }

            [Fact, LogIfTooSlow]
            public async Task OpensTheBrowserWithTheCorrectURL()
            {
                await ViewModel.ContactUs.Execute();

                await NavigationService.Received().Navigate<BrowserViewModel, BrowserParameters>(
                    Arg.Is<BrowserParameters>(parameter => parameter.Url == Resources.ContactUsUrl)
                );
            }
        }

    }
}
