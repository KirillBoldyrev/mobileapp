using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using MvvmCross.Commands;
using MvvmCross.Navigation;
using MvvmCross.ViewModels;
using Toggl.Foundation;
using Toggl.Foundation.Analytics;
using Toggl.Foundation.Autocomplete;
using Toggl.Foundation.Autocomplete.Span;
using Toggl.Foundation.Autocomplete.Suggestions;
using Toggl.Foundation.DataSources;
using Toggl.Foundation.Diagnostics;
using Toggl.Foundation.Interactors;
using Toggl.Foundation.Models;
using Toggl.Foundation.Models.Interfaces;
using Toggl.Foundation.MvvmCross.Collections;
using Toggl.Foundation.MvvmCross.Extensions;
using Toggl.Foundation.MvvmCross.Parameters;
using Toggl.Foundation.MvvmCross.Services;
using Toggl.Foundation.MvvmCross.ViewModels;
using Toggl.Foundation.Services;
using Toggl.Multivac;
using Toggl.Multivac.Extensions;
using Toggl.PrimeRadiant.Settings;
using static Toggl.Foundation.Helper.Constants;
using static Toggl.Multivac.Extensions.CommonFunctions;
using IStopwatch = Toggl.Foundation.Diagnostics.IStopwatch;
using IStopwatchProvider = Toggl.Foundation.Diagnostics.IStopwatchProvider;

[assembly: MvxNavigation(typeof(StartTimeEntryViewModel), ApplicationUrls.StartTimeEntry)]
namespace Toggl.Foundation.MvvmCross.ViewModels
{
    [Preserve(AllMembers = true)]
    public sealed class StartTimeEntryViewModel : MvxViewModel<StartTimeEntryParameters>, ITimeEntryPrototype
    {
        //Fields
        private readonly ITimeService timeService;
        private readonly ITogglDataSource dataSource;
        private readonly IDialogService dialogService;
        private readonly IUserPreferences userPreferences;
        private readonly IInteractorFactory interactorFactory;
        private readonly IMvxNavigationService navigationService;
        private readonly IAnalyticsService analyticsService;
        private readonly IAutocompleteProvider autocompleteProvider;
        private readonly ISchedulerProvider schedulerProvider;
        private readonly IIntentDonationService intentDonationService;
        private readonly IStopwatchProvider stopwatchProvider;

        private readonly CompositeDisposable disposeBag = new CompositeDisposable();
        private readonly ISubject<TextFieldInfo> uiSubject = new ReplaySubject<TextFieldInfo>();
        private readonly ISubject<TextFieldInfo> querySubject = new Subject<TextFieldInfo>();
        private readonly ISubject<AutocompleteSuggestionType> queryByTypeSubject = new Subject<AutocompleteSuggestionType>();

        private bool hasAnyTags;
        private bool hasAnyProjects;
        private bool canCreateProjectsInWorkspace;
        private IThreadSafeWorkspace defaultWorkspace;
        private StartTimeEntryParameters parameter;
        private TextFieldInfo textFieldInfo = TextFieldInfo.Empty(0);
        private StartTimeEntryParameters initialParameters;
        private IStopwatch startTimeEntryStopwatch;
        private Dictionary<string, IStopwatch> suggestionsLoadingStopwatches = new Dictionary<string, IStopwatch>();
        private IStopwatch suggestionsRenderingStopwatch;

        //Properties
        public IObservable<TextFieldInfo> TextFieldInfoObservable { get; }
        public BeginningOfWeek BeginningOfWeek { get; private set; }

        private bool isRunning => !Duration.HasValue;

        public bool SuggestCreation
        {
            get
            {
                if (IsSuggestingProjects && textFieldInfo.HasProject) return false;

                if (string.IsNullOrEmpty(CurrentQuery))
                    return false;

                if (IsSuggestingProjects)
                    return CurrentQuery.LengthInBytes() <= MaxProjectNameLengthInBytes
                           && canCreateProjectsInWorkspace;
/*
                if (IsSuggestingTags)
                    return Suggestions.None(c => c.Any(s =>
                               s is TagSuggestion tS
                               && tS.Name.IsSameCaseInsensitiveTrimedTextAs(CurrentQuery)))
                           && CurrentQuery.IsAllowedTagByteSize();
*/
                return false;
            }
        }

        public long[] TagIds => textFieldInfo.Spans.OfType<TagSpan>().Select(span => span.TagId).Distinct().ToArray();

        public long? ProjectId => textFieldInfo.Spans.OfType<ProjectSpan>().SingleOrDefault()?.ProjectId;

        public long? TaskId => textFieldInfo.Spans.OfType<ProjectSpan>().SingleOrDefault()?.TaskId;

        public string Description => textFieldInfo.Description;

        public long WorkspaceId => textFieldInfo.WorkspaceId;

        public bool IsDirty
            => !string.IsNullOrEmpty(textFieldInfo.Description)
                || textFieldInfo.Spans.Any(s => s is ProjectSpan || s is TagSpan)
                || IsBillable
                || StartTime != parameter.StartTime
                || Duration != parameter.Duration;

        public string CurrentQuery { get; private set; }

        public bool IsSuggestingTags { get; private set; }

        public bool IsSuggestingProjects { get; private set; }

        private TimeSpan displayedTime = TimeSpan.Zero;

        public TimeSpan DisplayedTime
        {
            get => displayedTime;
            set
            {
                if (isRunning)
                {
                    StartTime = timeService.CurrentDateTime - value;
                }
                else
                {
                    Duration = value;
                }

                displayedTime = value;

                RaisePropertyChanged();
            }
        }

        public DurationFormat DisplayedTimeFormat { get; } = DurationFormat.Improved;

        public bool IsBillable { get; private set; } = false;

        public bool IsBillableAvailable { get; private set; } = false;

        public bool IsEditingProjects { get; private set; } = false;

        public bool IsEditingTags { get; private set; } = false;

        public string PlaceholderText { get; private set; }

        public bool ShouldShowNoTagsInfoMessage
            => IsSuggestingTags && !hasAnyTags;

        public bool ShouldShowNoProjectsInfoMessage
            => IsSuggestingProjects && !hasAnyProjects;

        public DateTimeOffset StartTime { get; private set; }

        public TimeSpan? Duration { get; private set; }

        public IObservable<IEnumerable<CollectionSection<string, AutocompleteSuggestion>>>
            Suggestions { get; private set; }

        public ITogglDataSource DataSource => dataSource;


        public IMvxAsyncCommand SetStartDateCommand { get; }

        public IMvxAsyncCommand ChangeTimeCommand { get; }

        public IMvxCommand ToggleBillableCommand { get; }

        public IMvxCommand DurationTapped { get; }

        public IMvxCommand ToggleTagSuggestionsCommand { get; }

        public IMvxCommand ToggleProjectSuggestionsCommand { get; }


        public IMvxCommand<ProjectSuggestion> ToggleTaskSuggestionsCommand { get; }

        public IOnboardingStorage OnboardingStorage { get; }





        public UIAction Back { get; }
        public UIAction Done { get; }
        public InputAction<AutocompleteSuggestion> SelectSuggestion { get; }


        public StartTimeEntryViewModel(
            ITimeService timeService,
            ITogglDataSource dataSource,
            IDialogService dialogService,
            IUserPreferences userPreferences,
            IOnboardingStorage onboardingStorage,
            IInteractorFactory interactorFactory,
            IMvxNavigationService navigationService,
            IAnalyticsService analyticsService,
            IAutocompleteProvider autocompleteProvider,
            ISchedulerProvider schedulerProvider,
            IIntentDonationService intentDonationService,
            IStopwatchProvider stopwatchProvider,
            IRxActionFactory rxActionFactory
        )
        {
            Ensure.Argument.IsNotNull(dataSource, nameof(dataSource));
            Ensure.Argument.IsNotNull(timeService, nameof(timeService));
            Ensure.Argument.IsNotNull(dialogService, nameof(dialogService));
            Ensure.Argument.IsNotNull(userPreferences, nameof(userPreferences));
            Ensure.Argument.IsNotNull(interactorFactory, nameof(interactorFactory));
            Ensure.Argument.IsNotNull(onboardingStorage, nameof(onboardingStorage));
            Ensure.Argument.IsNotNull(navigationService, nameof(navigationService));
            Ensure.Argument.IsNotNull(analyticsService, nameof(analyticsService));
            Ensure.Argument.IsNotNull(autocompleteProvider, nameof(autocompleteProvider));
            Ensure.Argument.IsNotNull(schedulerProvider, nameof(schedulerProvider));
            Ensure.Argument.IsNotNull(intentDonationService, nameof(intentDonationService));
            Ensure.Argument.IsNotNull(stopwatchProvider, nameof(stopwatchProvider));
            Ensure.Argument.IsNotNull(rxActionFactory, nameof(rxActionFactory));

            this.dataSource = dataSource;
            this.timeService = timeService;
            this.dialogService = dialogService;
            this.userPreferences = userPreferences;
            this.navigationService = navigationService;
            this.interactorFactory = interactorFactory;
            this.analyticsService = analyticsService;
            this.autocompleteProvider = autocompleteProvider;
            this.schedulerProvider = schedulerProvider;
            this.intentDonationService = intentDonationService;
            this.stopwatchProvider = stopwatchProvider;

            OnboardingStorage = onboardingStorage;

            TextFieldInfoObservable = uiSubject.AsDriver(this.schedulerProvider);

            Back = rxActionFactory.FromAsync(Close);
            Done = rxActionFactory.FromObservable(done);
            SelectSuggestion = rxActionFactory.FromAsync<AutocompleteSuggestion>(selectSuggestion);

            DurationTapped = new MvxCommand(durationTapped);
            ChangeTimeCommand = new MvxAsyncCommand(changeTime);
            ToggleBillableCommand = new MvxCommand(toggleBillable);
            SetStartDateCommand = new MvxAsyncCommand(setStartDate);
            ToggleTagSuggestionsCommand = new MvxCommand(toggleTagSuggestions);
            ToggleProjectSuggestionsCommand = new MvxCommand(toggleProjectSuggestions);

            ToggleTaskSuggestionsCommand = new MvxCommand<ProjectSuggestion>(toggleTaskSuggestions);

            var queryByType = queryByTypeSubject
                .AsObservable()
                .SelectMany(type => autocompleteProvider.Query(new QueryInfo("", type)));

            var queryByText = querySubject
                .AsObservable()
                .StartWith(textFieldInfo)
                .Select(QueryInfo.ParseFieldInfo)
                .Do(onParsedQuery)
                .ObserveOn(schedulerProvider.BackgroundScheduler)
                .SelectMany(autocompleteProvider.Query);

            Suggestions = Observable.Merge(queryByText, queryByType)
                .Select(items => items.ToList()) // This is line is needed for now to read objects from realm
                .Select(toSuggestions)
                .AsDriver(schedulerProvider);
        }

        public void Init()
        {
            var now = timeService.CurrentDateTime;
            var startTimeEntryParameters = userPreferences.IsManualModeEnabled
                ? StartTimeEntryParameters.ForManualMode(now)
                : StartTimeEntryParameters.ForTimerMode(now);
            Prepare(startTimeEntryParameters);
        }

        public override void Prepare()
        {

        }

        public override void Prepare(StartTimeEntryParameters parameter)
        {
            this.parameter = parameter;
            StartTime = parameter.StartTime;
            Duration = parameter.Duration;

            PlaceholderText = parameter.PlaceholderText;
            if (!string.IsNullOrEmpty(parameter.EntryDescription))
            {
                initialParameters = parameter;
            }

            timeService.CurrentDateTimeObservable
                .Where(_ => isRunning)
                .Subscribe(currentTime => DisplayedTime = currentTime - StartTime)
                .DisposedBy(disposeBag);
        }

        public override async Task Initialize()
        {
            await base.Initialize();
            startTimeEntryStopwatch = stopwatchProvider.Get(MeasuredOperation.OpenStartView);
            stopwatchProvider.Remove(MeasuredOperation.OpenStartView);

            defaultWorkspace = await interactorFactory.GetDefaultWorkspace()
                .TrackException<InvalidOperationException, IThreadSafeWorkspace>("StartTimeEntryViewModel.Initialize")
                .Execute();

            canCreateProjectsInWorkspace =
                await interactorFactory.GetAllWorkspaces().Execute().Select(allWorkspaces =>
                    allWorkspaces.Any(ws => ws.IsEligibleForProjectCreation()));

            textFieldInfo = TextFieldInfo.Empty(parameter?.WorkspaceId ?? defaultWorkspace.Id);

            if (initialParameters != null)
            {
                var spans = new List<ISpan>();
                spans.Add(new TextSpan(initialParameters.EntryDescription));
                if (initialParameters.ProjectId != null) {
                    try
                    {
                        var project = await interactorFactory.GetProjectById((long)initialParameters.ProjectId).Execute();
                        spans.Add(new ProjectSpan(project));
                    }
                    catch
                    {
                        // Intentionally left blank
                    }
                }
                if (initialParameters.TagIds != null) {
                    try
                    {
                        var tags = initialParameters.TagIds.ToObservable()
                            .SelectMany<long, IThreadSafeTag>(tagId => interactorFactory.GetTagById(tagId).Execute())
                            .ToEnumerable();
                        spans.AddRange(tags.Select(tag => new TagSpan(tag)));
                    }
                    catch
                    {
                        // Intentionally left blank
                    }
                }

                textFieldInfo = textFieldInfo.ReplaceSpans(spans.ToImmutableList());
            }

            await setBillableValues(textFieldInfo.ProjectId);
            uiSubject.OnNext(textFieldInfo);

            hasAnyTags = (await dataSource.Tags.GetAll()).Any();
            hasAnyProjects = (await dataSource.Projects.GetAll()).Any();

            dataSource.User.Current
                      .Subscribe(onUserChanged)
                      .DisposedBy(disposeBag);
        }

        public override void ViewAppeared()
        {
            base.ViewAppeared();
            startTimeEntryStopwatch?.Stop();
            startTimeEntryStopwatch = null;
        }

        public override void ViewDestroy(bool viewFinishing)
        {
            base.ViewDestroy(viewFinishing);
            disposeBag?.Dispose();
        }

        public void StopSuggestionsRenderingStopwatch()
        {
            suggestionsRenderingStopwatch?.Stop();
            suggestionsRenderingStopwatch = null;
        }

        public async Task OnTextFieldInfoFromView(IImmutableList<ISpan> spans)
        {
            queryWith(textFieldInfo.ReplaceSpans(spans));
            await setBillableValues(textFieldInfo.ProjectId);
        }

        public async Task<bool> Close()
        {
            if (IsDirty)
            {
                var shouldDiscard = await dialogService.ConfirmDestructiveAction(ActionType.DiscardNewTimeEntry);
                if (!shouldDiscard)
                    return false;
            }

            await navigationService.Close(this);
            return true;
        }

        private void onUserChanged(IThreadSafeUser user)
        {
            BeginningOfWeek = user.BeginningOfWeek;
        }

        private async Task selectSuggestion(AutocompleteSuggestion suggestion)
        {
            switch (suggestion)
            {
                case QuerySymbolSuggestion querySymbolSuggestion:

                    if (querySymbolSuggestion.Symbol == QuerySymbols.ProjectsString)
                    {
                        analyticsService.StartViewTapped.Track(StartViewTapSource.PickEmptyStateProjectSuggestion);
                        analyticsService.StartEntrySelectProject.Track(ProjectTagSuggestionSource.TableCellButton);
                    }
                    else if (querySymbolSuggestion.Symbol == QuerySymbols.TagsString)
                    {
                        analyticsService.StartViewTapped.Track(StartViewTapSource.PickEmptyStateTagSuggestion);
                        analyticsService.StartEntrySelectTag.Track(ProjectTagSuggestionSource.TableCellButton);
                    }

                    queryAndUpdateUiWith(textFieldInfo.FromQuerySymbolSuggestion(querySymbolSuggestion));
                    break;

                case TimeEntrySuggestion timeEntrySuggestion:
                    analyticsService.StartViewTapped.Track(StartViewTapSource.PickTimeEntrySuggestion);
                    updateUiWith(textFieldInfo.FromTimeEntrySuggestion(timeEntrySuggestion));
                    await setBillableValues(timeEntrySuggestion.ProjectId);
                    break;

                case ProjectSuggestion projectSuggestion:
                    analyticsService.StartViewTapped.Track(StartViewTapSource.PickProjectSuggestion);

                    if (textFieldInfo.WorkspaceId != projectSuggestion.WorkspaceId
                        && await workspaceChangeDenied())
                        return;

                    IsSuggestingProjects = false;
                    updateUiWith(textFieldInfo.FromProjectSuggestion(projectSuggestion));
                    await setBillableValues(projectSuggestion.ProjectId);
                    queryByTypeSubject.OnNext(AutocompleteSuggestionType.None);

                    break;

                case TaskSuggestion taskSuggestion:
                    analyticsService.StartViewTapped.Track(StartViewTapSource.PickTaskSuggestion);

                    if (textFieldInfo.WorkspaceId != taskSuggestion.WorkspaceId
                        && await workspaceChangeDenied())
                        return;

                    IsSuggestingProjects = false;
                    updateUiWith(textFieldInfo.FromTaskSuggestion(taskSuggestion));
                    await setBillableValues(taskSuggestion.ProjectId);
                    queryByTypeSubject.OnNext(AutocompleteSuggestionType.None);
                    break;

                case TagSuggestion tagSuggestion:
                    analyticsService.StartViewTapped.Track(StartViewTapSource.PickTagSuggestion);
                    updateUiWith(textFieldInfo.FromTagSuggestion(tagSuggestion));
                    break;

                case CreateEntitySuggestion createEntitySuggestion:
                    if (IsSuggestingProjects)
                    {
                        createProject();
                    }
                    else
                    {
                        createTag();
                    }
                    break;

                default:
                    return;
            }

            IObservable<bool> workspaceChangeDenied()
                => dialogService.Confirm(
                    Resources.DifferentWorkspaceAlertTitle,
                    Resources.DifferentWorkspaceAlertMessage,
                    Resources.Ok,
                    Resources.Cancel
                ).Select(Invert);
        }

        private async Task createProject()
        {
            var createProjectStopwatch = stopwatchProvider.CreateAndStore(MeasuredOperation.OpenCreateProjectViewFromStartTimeEntryView);
            createProjectStopwatch.Start();

            var projectId = await navigationService.Navigate<EditProjectViewModel, string, long?>(CurrentQuery);
            if (projectId == null) return;

            var project = await interactorFactory.GetProjectById(projectId.Value).Execute();
            var projectSuggestion = new ProjectSuggestion(project);

            updateUiWith(textFieldInfo.FromProjectSuggestion(projectSuggestion));
            IsSuggestingProjects = false;
            queryByTypeSubject.OnNext(AutocompleteSuggestionType.None);
            hasAnyProjects = true;
        }

        private async Task createTag()
        {
            var createdTag = await interactorFactory.CreateTag(CurrentQuery, textFieldInfo.WorkspaceId).Execute();
            var tagSuggestion = new TagSuggestion(createdTag);
            await SelectSuggestion.Execute(tagSuggestion);
            hasAnyTags = true;
            toggleTagSuggestions();
        }

        private void OnDurationChanged()
        {
            if (Duration == null)
                return;

            DisplayedTime = Duration.Value;
        }

        private void durationTapped()
        {
            analyticsService.StartViewTapped.Track(StartViewTapSource.Duration);
        }

        private void toggleTagSuggestions()
        {
            if (IsSuggestingTags)
            {
                updateUiWith(textFieldInfo.RemoveTagQueryIfNeeded());
                IsSuggestingTags = false;
                return;
            }

            analyticsService.StartViewTapped.Track(StartViewTapSource.Tags);
            analyticsService.StartEntrySelectTag.Track(ProjectTagSuggestionSource.ButtonOverKeyboard);
            OnboardingStorage.ProjectOrTagWasAdded();

            queryAndUpdateUiWith(textFieldInfo.AddQuerySymbol(QuerySymbols.TagsString));
        }

        private void toggleProjectSuggestions()
        {
            if (IsSuggestingProjects)
            {
                IsSuggestingProjects = false;
                updateUiWith(textFieldInfo.RemoveProjectQueryIfNeeded());
                queryByTypeSubject.OnNext(AutocompleteSuggestionType.None);
                return;
            }

            analyticsService.StartViewTapped.Track(StartViewTapSource.Project);
            analyticsService.StartEntrySelectProject.Track(ProjectTagSuggestionSource.ButtonOverKeyboard);
            OnboardingStorage.ProjectOrTagWasAdded();

            if (textFieldInfo.HasProject)
            {
                IsSuggestingProjects = true;
                queryByTypeSubject.OnNext(AutocompleteSuggestionType.Projects);
                return;
            }

            queryAndUpdateUiWith(
                textFieldInfo.AddQuerySymbol(QuerySymbols.ProjectsString)
            );
        }

        private void toggleTaskSuggestions(ProjectSuggestion projectSuggestion)
        {
            /*
            var grouping = Suggestions.FirstOrDefault(s => s.WorkspaceId == projectSuggestion.WorkspaceId);
            if (grouping == null) return;

            var suggestionIndex = grouping.IndexOf(projectSuggestion);
            if (suggestionIndex < 0) return;

            projectSuggestion.TasksVisible = !projectSuggestion.TasksVisible;

            var groupingIndex = Suggestions.IndexOf(grouping);
            Suggestions.Remove(grouping);
            Suggestions.Insert(groupingIndex,
                new WorkspaceGroupedCollection<AutocompleteSuggestion>(
                    grouping.WorkspaceName, grouping.WorkspaceId, getSuggestionsWithTasks(grouping)
                )
            );
            */
        }

        private void toggleBillable()
        {
            analyticsService.StartViewTapped.Track(StartViewTapSource.Billable);
            IsBillable = !IsBillable;
        }

        private async Task changeTime()
        {
            analyticsService.StartViewTapped.Track(StartViewTapSource.StartTime);

            var currentDuration = DurationParameter.WithStartAndDuration(StartTime, Duration);

            var selectedDuration = await navigationService
                .Navigate<EditDurationViewModel, EditDurationParameters, DurationParameter>(new EditDurationParameters(currentDuration, isStartingNewEntry: true))
                .ConfigureAwait(false);

            StartTime = selectedDuration.Start;
            Duration = selectedDuration.Duration ?? Duration;
        }

        private async Task setStartDate()
        {
            analyticsService.StartViewTapped.Track(StartViewTapSource.StartDate);

            var parameters = isRunning
                ? DateTimePickerParameters.ForStartDateOfRunningTimeEntry(StartTime, timeService.CurrentDateTime)
                : DateTimePickerParameters.ForStartDateOfStoppedTimeEntry(StartTime);

            var duration = Duration;

            StartTime = await navigationService
                .Navigate<SelectDateTimeViewModel, DateTimePickerParameters, DateTimeOffset>(parameters)
                .ConfigureAwait(false);

            if (isRunning == false)
            {
                Duration = duration;
            }
        }

        private IObservable<Unit> done()
        {
            return interactorFactory.CreateTimeEntry(this).Execute()
                .Do(_ => navigationService.Close(this))
                .SelectUnit();
        }

        private void onParsedQuery(QueryInfo parsedQuery)
        {
            var newQuery = parsedQuery.Text?.Trim() ?? "";
            if (CurrentQuery != newQuery)
            {
                CurrentQuery = newQuery;
                suggestionsLoadingStopwatches[CurrentQuery] = stopwatchProvider.Create(MeasuredOperation.StartTimeEntrySuggestionsLoadingTime);
                suggestionsLoadingStopwatches[CurrentQuery].Start();
            }
            bool suggestsTags = parsedQuery.SuggestionType == AutocompleteSuggestionType.Tags;
            bool suggestsProjects = parsedQuery.SuggestionType == AutocompleteSuggestionType.Projects;

            if (!IsSuggestingTags && suggestsTags)
            {
                analyticsService.StartEntrySelectTag.Track(ProjectTagSuggestionSource.TextField);
            }

            if (!IsSuggestingProjects && suggestsProjects)
            {
                analyticsService.StartEntrySelectProject.Track(ProjectTagSuggestionSource.TextField);
            }

            IsSuggestingTags = suggestsTags;
            IsSuggestingProjects = suggestsProjects;
        }

        private IEnumerable<CollectionSection<string, AutocompleteSuggestion>> toSuggestions(IEnumerable<AutocompleteSuggestion> suggestions)
        {
            var filteredSuggestions = filterSuggestions(suggestions);
            var groupedSuggestions = groupSuggestions(filteredSuggestions);

            if (suggestionsLoadingStopwatches.ContainsKey(CurrentQuery))
            {
                suggestionsLoadingStopwatches[CurrentQuery]?.Stop();
                suggestionsLoadingStopwatches = new Dictionary<string, IStopwatch>();
            }

            return groupedSuggestions;
        }

        private IEnumerable<AutocompleteSuggestion> filterSuggestions(IEnumerable<AutocompleteSuggestion> suggestions)
        {
            suggestionsRenderingStopwatch = stopwatchProvider.Create(MeasuredOperation.StartTimeEntrySuggestionsRenderingTime);
            suggestionsRenderingStopwatch.Start();

            if (textFieldInfo.HasProject && !IsSuggestingProjects && !IsSuggestingTags)
            {
                var projectId = textFieldInfo.Spans.OfType<ProjectSpan>().Single().ProjectId;

                return suggestions.OfType<TimeEntrySuggestion>()
                    .Where(suggestion => suggestion.ProjectId == projectId);
            }

            return suggestions;
        }

        private IEnumerable<CollectionSection<string, AutocompleteSuggestion>> groupSuggestions(
            IEnumerable<AutocompleteSuggestion> suggestions)
        {
            IEnumerable<CollectionSection<string, AutocompleteSuggestion>> sections;

            var firstSuggestion = suggestions.FirstOrDefault();
            if (firstSuggestion is ProjectSuggestion)
            {
                sections = suggestions
                    .Cast<ProjectSuggestion>()
                    .OrderBy(ps => ps.ProjectName)
                    .Where(suggestion => !string.IsNullOrEmpty(suggestion.WorkspaceName))
                    .GroupBy(suggestion => suggestion.WorkspaceId)
                    .OrderByDescending(group => group.First().WorkspaceId == (defaultWorkspace?.Id ?? 0))
                    .ThenBy(group => group.First().WorkspaceName)
                    .Select(group =>
                        new CollectionSection<string, AutocompleteSuggestion>(@group.First().WorkspaceName, group)
                    );
            }

            if (IsSuggestingTags)
                suggestions = suggestions.Where(suggestion => suggestion.WorkspaceId == textFieldInfo.WorkspaceId);

            sections = suggestions
                .GroupBy(suggestion => suggestion.WorkspaceId)
                .Select(group =>
                    new CollectionSection<string, AutocompleteSuggestion>(
                        group.First().WorkspaceName,
                        group.Distinct(AutocompleteSuggestionComparer.Instance)
                    )
                );

            if (canCreateProjectsInWorkspace && IsSuggestingProjects && !textFieldInfo.HasProject &&
                CurrentQuery.LengthInBytes() <= MaxProjectNameLengthInBytes &&
                !string.IsNullOrEmpty(CurrentQuery) &&
                sections.None(section => section.Items.Any(item => item is ProjectSuggestion projectSuggestion && projectSuggestion.ProjectName.IsSameCaseInsensitiveTrimedTextAs(CurrentQuery)))
                )
            {
                return sections
                    .ToList()
                    .Prepend(
                        new CollectionSection<string, AutocompleteSuggestion>(
                            "",
                            new[] { new CreateEntitySuggestion(Resources.CreateProject, textFieldInfo.Description) }
                        )
                    );
            }

            if (IsSuggestingTags &&
                !string.IsNullOrEmpty(CurrentQuery) &&
                CurrentQuery.IsAllowedTagByteSize() &&
                sections.None(section => section.Items.Any(item =>
                    item is TagSuggestion tagSuggestion &&
                    tagSuggestion.Name.IsSameCaseInsensitiveTrimedTextAs(CurrentQuery)))
            )
            {
                return sections
                    .ToList()
                    .Prepend(
                        new CollectionSection<string, AutocompleteSuggestion>(
                            "",
                            new[] { new CreateEntitySuggestion(Resources.CreateTag, textFieldInfo.Description) }
                        )
                    );
            }

            return sections;
        }

        private async Task setBillableValues(long? currentProjectId)
        {
            var hasProject = currentProjectId.HasValue && currentProjectId.Value != ProjectSuggestion.NoProjectId;
            if (hasProject)
            {
                var projectId = currentProjectId.Value;
                IsBillableAvailable =
                    await interactorFactory.IsBillableAvailableForProject(projectId).Execute();

                IsBillable = IsBillableAvailable && await interactorFactory.ProjectDefaultsToBillable(projectId).Execute();
            }
            else
            {
                IsBillable = false;
                IsBillableAvailable = await interactorFactory.IsBillableAvailableForWorkspace(WorkspaceId).Execute();
            }
        }

        private IEnumerable<AutocompleteSuggestion> getSuggestionsWithTasks(
            IEnumerable<AutocompleteSuggestion> suggestions)
        {
            foreach (var suggestion in suggestions)
            {
                if (suggestion is TaskSuggestion) continue;

                yield return suggestion;

                if (suggestion is ProjectSuggestion projectSuggestion && projectSuggestion.TasksVisible)
                {
                    var orderedTasks = projectSuggestion.Tasks
                        .OrderBy(t => t.Name);

                    foreach (var taskSuggestion in orderedTasks)
                        yield return taskSuggestion;
                }
            }
        }

        private void queryWith(TextFieldInfo newTextFieldinfo)
        {
            textFieldInfo = newTextFieldinfo;
            querySubject.OnNext(textFieldInfo);
        }

        private void updateUiWith(TextFieldInfo newTextFieldinfo)
        {
            textFieldInfo = newTextFieldinfo;
            uiSubject.OnNext(textFieldInfo);
        }

        private void queryAndUpdateUiWith(TextFieldInfo newTextFieldinfo)
        {
            textFieldInfo = newTextFieldinfo;
            uiSubject.OnNext(textFieldInfo);
            querySubject.OnNext(textFieldInfo);
        }
    }
}
