using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeditationApp.Models;
using MeditationApp.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.Text.Json;
using Microsoft.Maui.Networking;

namespace MeditationApp.ViewModels
{
    public partial class BreathingExerciseViewModel : ObservableObject
    {
        private readonly BreathingDatabaseService? _databaseService;
        private readonly DatabaseSyncService? _syncService;
        private Timer? _breathingTimer;
        private DateTime _phaseStartTime;
        private BreathingSession? _currentSession;
        private CancellationTokenSource? _cancellationTokenSource;

        [ObservableProperty]
        private ObservableCollection<BreathingTechnique> _techniques = new();

        [ObservableProperty]
        private BreathingTechnique? _selectedTechnique;

        [ObservableProperty]
        private BreathingPhase _currentPhase = BreathingPhase.Ready;

        [ObservableProperty]
        private BreathingState _breathingState = BreathingState.Stopped;

        [ObservableProperty]
        private int _currentCycle = 0;

        [ObservableProperty]
        private int _totalCycles = 10;

        [ObservableProperty]
        private int _remainingTime = 0;

        [ObservableProperty]
        private int _phaseProgress = 0;

        [ObservableProperty]
        private string _phaseText = "Ready to begin?";

        [ObservableProperty]
        private string _instructionText = "Select a technique to get started";

        [ObservableProperty]
        private double _circleScale = 1.0;

        [ObservableProperty]
        private Color _circleColor = Colors.LightGreen;

        [ObservableProperty]
        private string _buttonText = "Start";

        [ObservableProperty]
        private bool _isSessionActive = false;

        [ObservableProperty]
        private BreathingStats _stats = new();

        // Public method to load stats for BreathingStatsPage
        public void LoadBreathingStats()
        {
            LoadStats();
        }

        [ObservableProperty]
        private bool _showTechniqueSelector = true;

        [ObservableProperty]
        private TimeSpan _sessionDuration = TimeSpan.Zero;

        [ObservableProperty]
        private bool _isLoading = true;

        [ObservableProperty]
        private bool _hasPremiumSubscription = false;

        public BreathingExerciseViewModel(BreathingDatabaseService? databaseService = null, DatabaseSyncService? syncService = null)
        {
            _databaseService = databaseService;
            _syncService = syncService;
            // Explicitly initialize the current session to null to prevent accidental reuse
            _currentSession = null;
            LoadTechniques();
            LoadStats();
            LoadSubscriptionStatus();
            IsLoading = false;
        }

        private void LoadTechniques()
        {
            var predefinedTechniques = BreathingTechnique.GetPredefinedTechniques();
            Techniques.Clear();
            foreach (var technique in predefinedTechniques)
            {
                Techniques.Add(technique);
            }
            
            // Load custom techniques from storage
            LoadCustomTechniques();
            
            // Set default technique but don't auto-select it
            SelectedTechnique = Techniques.FirstOrDefault();
            if (SelectedTechnique != null)
            {
                TotalCycles = SelectedTechnique.Cycles;
                InstructionText = SelectedTechnique.Instructions;
                // Don't auto-select the first technique - let the user choose it
                // await SelectTechnique(SelectedTechnique);
            }
        }

        private async void LoadCustomTechniques()
        {
            try
            {
                var customTechniquesJson = await SecureStorage.GetAsync("custom_breathing_techniques");
                if (!string.IsNullOrEmpty(customTechniquesJson))
                {
                    var customTechniques = JsonSerializer.Deserialize<List<BreathingTechnique>>(customTechniquesJson);
                    if (customTechniques != null)
                    {
                        foreach (var technique in customTechniques)
                        {
                            Techniques.Add(technique);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading custom techniques: {ex.Message}");
            }
        }

        private async void LoadStats()
        {
            try
            {
                if (_databaseService != null)
                {
                    Stats = await _databaseService.GetStatsAsync();
                }
                else
                {
                    // Fallback to local storage
                    var statsJson = await SecureStorage.GetAsync("breathing_stats");
                    if (!string.IsNullOrEmpty(statsJson))
                    {
                        var stats = JsonSerializer.Deserialize<BreathingStats>(statsJson);
                        if (stats != null)
                        {
                            Stats = stats;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading stats: {ex.Message}");
            }
        }

        private async void LoadSubscriptionStatus()
        {
            try
            {
                var paywallService = new PaywallService();
                HasPremiumSubscription = await paywallService.CheckSubscriptionStatusAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading subscription status: {ex.Message}");
                HasPremiumSubscription = false;
            }
        }

        [RelayCommand]
        private async Task SelectTechnique(BreathingTechnique technique)
        {
            if (IsSessionActive) return;
            
            // Check if this technique requires premium access
            // Free techniques: 4-7-8 Technique (Id=1) and Box Breathing (Id=2)
            // Premium techniques: Equal Breathing (Id=3), Relaxing Breath (Id=4), Energizing Breath (Id=5)
            if (technique.Id > 2)
            {
                var hasPremium = await MeditationApp.Utils.PremiumFeatureHelper.CheckPremiumAccessAsync($"{technique.Name} Breathing Technique");
                if (!hasPremium)
                {
                    // User doesn't have premium or cancelled upgrade, keep technique selector open
                    return;
                }
                else
                {
                    // User has premium access, update subscription status
                    HasPremiumSubscription = true;
                }
            }
            
            SelectedTechnique = technique;
            TotalCycles = technique.Cycles;
            InstructionText = technique.Instructions;
            PhaseText = "Ready to begin?";
            CurrentPhase = BreathingPhase.Ready;
            CircleScale = 1.0;
            CircleColor = Color.FromArgb(technique.IconColor);
            ShowTechniqueSelector = false;
        }

        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task StartStopSession()
        {
            Debug.WriteLine($"StartStopSession called. BreathingState: {BreathingState}");
            Debug.WriteLine($"SelectedTechnique: {SelectedTechnique?.Name}");
            
            switch (BreathingState)
            {
                case BreathingState.Stopped:
                    Debug.WriteLine("Starting session...");
                    await StartSessionAsync();
                    break;
                case BreathingState.Playing:
                    Debug.WriteLine("Pausing session...");
                    PauseSession();
                    break;
                case BreathingState.Paused:
                    Debug.WriteLine("Resuming session...");
                    ResumeSession();
                    break;
                case BreathingState.Completed:
                    Debug.WriteLine("Resetting session...");
                    ResetSession();
                    break;
            }
        }

        private async Task StartSessionAsync()
        {
            Debug.WriteLine("StartSessionAsync called");
            if (SelectedTechnique == null) 
            {
                Debug.WriteLine("No technique selected!");
                return;
            }

            Debug.WriteLine("Starting breathing session directly");
            await BeginBreathingSessionAsync();
        }

        [RelayCommand]
        private async Task BeginBreathingSessionAsync()
        {
            Debug.WriteLine("BeginBreathingSessionAsync called");
            
            // Safety check to prevent accidental session creation
            if (ShowTechniqueSelector)
            {
                Debug.WriteLine("Cannot start session while technique selector is visible");
                return;
            }
            
            if (SelectedTechnique == null) 
            {
                Debug.WriteLine("No technique selected in BeginBreathingSessionAsync!");
                return;
            }

            Debug.WriteLine($"Creating session for technique: {SelectedTechnique.Name}");
            
            _currentSession = new BreathingSession
            {
                StartTime = DateTime.Now,
                TechniqueId = SelectedTechnique.Id,
                TechniqueName = SelectedTechnique.Name,
                TotalCycles = TotalCycles
            };

            // Clean up any existing resources
            _breathingTimer?.Dispose();
            _breathingTimer = null;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            
            CurrentCycle = 0;
            BreathingState = BreathingState.Playing;
            IsSessionActive = true;
            
            // Make sure button text updates on the main thread
            MainThread.BeginInvokeOnMainThread(() => {
                ButtonText = "Pause";
            });

            Debug.WriteLine("Starting breathing cycle...");
            
            // Create cancellation token for this session
            _cancellationTokenSource = new CancellationTokenSource();
            
            try {
                await StartBreathingCycleAsync(_cancellationTokenSource.Token);
            }
            catch (OperationCanceledException) {
                Debug.WriteLine("Breathing cycle was cancelled during start");
            }
        }

        private async Task StartBreathingCycleAsync(CancellationToken cancellationToken = default)
        {
            Debug.WriteLine("StartBreathingCycleAsync called");
            if (SelectedTechnique == null || !IsSessionActive) 
            {
                Debug.WriteLine($"Cannot start breathing cycle. SelectedTechnique: {SelectedTechnique?.Name}, IsSessionActive: {IsSessionActive}");
                return;
            }

            CurrentCycle++;
            PhaseText = $"Cycle {CurrentCycle} of {TotalCycles}";
            Debug.WriteLine($"Starting cycle {CurrentCycle} of {TotalCycles}");
            
            // Inhale phase
            Debug.WriteLine("Starting inhale phase");
            await ExecutePhaseAsync(BreathingPhase.Inhale, SelectedTechnique.InhaleDuration, "Breathe In", 1.4, cancellationToken);
            
            if (!IsSessionActive || cancellationToken.IsCancellationRequested) return;
            
            // Inhale hold phase
            if (SelectedTechnique.InhaleHoldDuration > 0)
            {
                Debug.WriteLine("Starting inhale hold phase");
                await ExecutePhaseAsync(BreathingPhase.InhaleHold, SelectedTechnique.InhaleHoldDuration, "Hold", 1.4, cancellationToken);
            }
            
            if (!IsSessionActive || cancellationToken.IsCancellationRequested) return;
            
            // Exhale phase
            Debug.WriteLine("Starting exhale phase");
            await ExecutePhaseAsync(BreathingPhase.Exhale, SelectedTechnique.ExhaleDuration, "Breathe Out", 1.0, cancellationToken);
            
            if (!IsSessionActive || cancellationToken.IsCancellationRequested) return;
            
            // Exhale hold phase
            if (SelectedTechnique.ExhaleHoldDuration > 0)
            {
                Debug.WriteLine("Starting exhale hold phase");
                await ExecutePhaseAsync(BreathingPhase.ExhaleHold, SelectedTechnique.ExhaleHoldDuration, "Hold", 1.0, cancellationToken);
            }

            if (!IsSessionActive || cancellationToken.IsCancellationRequested) return;

            if (CurrentCycle < TotalCycles)
            {
                // Continue to next cycle
                await Task.Delay(500, cancellationToken); // Brief pause between cycles
                if (IsSessionActive && !cancellationToken.IsCancellationRequested)
                {
                    await StartBreathingCycleAsync(cancellationToken);
                }
            }
            else
            {
                // Session completed
                Debug.WriteLine("Session completed");
                await CompleteSessionAsync();
            }
        }

        private async Task ExecutePhaseAsync(BreathingPhase phase, int duration, string text, double targetScale, CancellationToken cancellationToken = default)
        {
            CurrentPhase = phase;
            PhaseText = text;
            _phaseStartTime = DateTime.Now;
            
            // Haptic feedback for breathing phases
            try
            {
                Debug.WriteLine($"[ExecutePhase] Triggering haptic feedback for {phase}");
                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
            }
            catch (Exception hapticEx)
            {
                Debug.WriteLine($"[ExecutePhase] Haptic feedback failed: {hapticEx.Message}");
            }

            // Start circle scale animation
            var animationTask = AnimateCircleScale(targetScale, duration * 1000, cancellationToken);
            
            // Use a timer for countdown instead of Task.Delay to ensure it can be properly cancelled
            var countdownComplete = new TaskCompletionSource<bool>();
            
            if (_breathingTimer != null)
            {
                _breathingTimer.Dispose();
            }
            
            int remainingSeconds = duration;
            
            _breathingTimer = new Timer(_ => 
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _breathingTimer?.Dispose();
                    _breathingTimer = null;
                    countdownComplete.TrySetCanceled();
                    return;
                }
                
                if (remainingSeconds <= 0)
                {
                    _breathingTimer?.Dispose();
                    _breathingTimer = null;
                    countdownComplete.TrySetResult(true);
                    return;
                }
                
                MainThread.BeginInvokeOnMainThread(() => 
                {
                    RemainingTime = remainingSeconds;
                    PhaseProgress = (int)((duration - remainingSeconds + 1) / (double)duration * 100);
                    remainingSeconds--;
                });
            }, null, 0, 1000);
            
            try
            {
                // Wait for either the countdown to complete or cancellation
                await countdownComplete.Task;
            }
            catch (TaskCanceledException)
            {
                // Task was cancelled, handle gracefully
                Debug.WriteLine("ExecutePhaseAsync cancelled");
                return;
            }
            finally
            {
                // Clean up timer if still running
                _breathingTimer?.Dispose();
                _breathingTimer = null;
            }
            
            // Make sure animation completes
            try
            {
                await animationTask;
            }
            catch (OperationCanceledException)
            {
                // Animation was cancelled, that's ok
            }
        }

        private async Task AnimateCircleScale(double targetScale, int durationMs, CancellationToken cancellationToken = default)
        {
            const int steps = 60; // 60 FPS
            var stepDelay = durationMs / steps;
            
            var startScale = CircleScale;
            var scaleStep = (targetScale - startScale) / steps;
            
            var startTime = DateTime.Now;
            var animationEndTime = startTime.AddMilliseconds(durationMs);
            
            for (int i = 0; i < steps && IsSessionActive && !cancellationToken.IsCancellationRequested; i++)
            {
                // Calculate progress based on elapsed time to ensure smooth animation
                var progress = Math.Min(1.0, (DateTime.Now - startTime).TotalMilliseconds / durationMs);
                
                MainThread.BeginInvokeOnMainThread(() => {
                    // Linear interpolation between start and target scales
                    CircleScale = startScale + (targetScale - startScale) * progress;
                });
                
                try
                {
                    await Task.Delay(stepDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("Animation cancelled");
                    return; // Exit gracefully when cancelled
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Animation error: {ex.Message}");
                    return;
                }
            }
            
            if (IsSessionActive && !cancellationToken.IsCancellationRequested)
            {
                MainThread.BeginInvokeOnMainThread(() => {
                    CircleScale = targetScale;
                });
            }
        }

        private void PauseSession()
        {
            Debug.WriteLine("PauseSession called");
            BreathingState = BreathingState.Paused;
            ButtonText = "Resume";
            PhaseText = "Paused";
            
            // Save the current phase and time for resuming
            _phaseStartTime = DateTime.Now;
            
            // Cancel the current breathing cycle
            _cancellationTokenSource?.Cancel();
            _breathingTimer?.Dispose();
            _breathingTimer = null;
            
            Debug.WriteLine("Cancellation token triggered for pause");
        }

        private void ResumeSession()
        {
            Debug.WriteLine("ResumeSession called");
            BreathingState = BreathingState.Playing;
            ButtonText = "Pause";
            
            // Create new cancellation token
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Resume from where we left off
            MainThread.BeginInvokeOnMainThread(async () => 
            {
                try
                {
                    // Ensure we're properly handling the UI thread for animations
                    await StartBreathingCycleAsync(_cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("Breathing cycle was cancelled during resume");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error during breathing cycle resume: {ex.Message}");
                }
            });
        }

        private async Task CompleteSessionAsync()
        {
            if (_currentSession == null) return;

            _currentSession.EndTime = DateTime.Now;
            _currentSession.CompletedCycles = CurrentCycle;
            _currentSession.IsCompleted = true;
            _currentSession.Duration = _currentSession.ActualDuration;

            BreathingState = BreathingState.Completed;
            IsSessionActive = false;
            ButtonText = "Start New Session";
            PhaseText = "Session Complete!";
            SessionDuration = _currentSession.Duration;

            // Update stats
            await UpdateStatsAsync();

            // Trigger sync if connected to internet
            if (_syncService != null && Connectivity.NetworkAccess == NetworkAccess.Internet)
            {
                Debug.WriteLine("[BreathingExercise] Triggering immediate breathing sync after session completion");
                _ = Task.Run(async () => 
                {
                    try
                    {
                        var result = await _syncService.SyncBreathingSessionsImmediatelyAsync();
                        Debug.WriteLine($"[BreathingExercise] Immediate breathing sync completed: {result.IsSuccess} - {result.Message}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[BreathingExercise] Immediate breathing sync failed: {ex.Message}");
                    }
                });
            }
            
            // Important: Clear the current session to prevent accidental reuse or duplication
            _currentSession = null;
        }

        private void ResetSession()
        {
            BreathingState = BreathingState.Stopped;
            IsSessionActive = false;
            CurrentPhase = BreathingPhase.Ready;
            CurrentCycle = 0;
            ButtonText = "Start";
            PhaseText = "Ready to begin?";
            CircleScale = 1.0;
            RemainingTime = 0;
            PhaseProgress = 0;
            ShowTechniqueSelector = true;
            // Make sure to explicitly set the current session to null to prevent accidental reuse
            _currentSession = null;
        }

        [RelayCommand]
        private void ShowTechniques()
        {
            if (!IsSessionActive)
            {
                ShowTechniqueSelector = true;
            }
        }

        private async Task UpdateStatsAsync()
        {
            if (_currentSession == null) return;

            // Save session to database with sync tracking
            if (_databaseService != null)
            {
                Debug.WriteLine($"[BreathingExercise] Saving session {_currentSession.Id} - IsCompleted: {_currentSession.IsCompleted}");
                await _databaseService.SaveSessionWithSyncTrackingAsync(_currentSession);
                Debug.WriteLine($"[BreathingExercise] Session saved with IsSynced: {_currentSession.IsSynced}");
                
                // Reload stats from database
                Stats = await _databaseService.GetStatsAsync();
            }
            else
            {
                // Fallback to updating local stats
                Stats.TotalSessions++;
                Stats.TotalDuration = Stats.TotalDuration.Add(_currentSession.Duration);
                Stats.TotalCyclesCompleted += _currentSession.CompletedCycles;
                Stats.LastSessionDate = DateTime.Today;

                // Update streak
                if (Stats.HasSessionToday)
                {
                    Stats.CurrentStreak++;
                    if (Stats.CurrentStreak > Stats.LongestStreak)
                    {
                        Stats.LongestStreak = Stats.CurrentStreak;
                    }
                }
                else
                {
                    Stats.CurrentStreak = 1;
                }

                await SaveStatsAsync();
            }
        }

        private async Task SaveStatsAsync()
        {
            try
            {
                var statsJson = JsonSerializer.Serialize(Stats);
                await SecureStorage.SetAsync("breathing_stats", statsJson);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving stats: {ex.Message}");
            }
        }

        public void Cleanup()
        {
            _breathingTimer?.Dispose();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            IsSessionActive = false;
            // Explicitly clear the current session to prevent accidental reuse
            _currentSession = null;
        }

        [RelayCommand]
        private async Task ShowStats()
        {
            Debug.WriteLine("[ShowStats] Navigating to BreathingStatsPage");
            await Shell.Current.GoToAsync("//BreathingStatsPage");
        }

        [RelayCommand]
        private async Task TriggerSync()
        {
            if (_syncService == null)
            {
                Debug.WriteLine("[TriggerSync] Sync service not available");
                return;
            }

            if (Connectivity.NetworkAccess != NetworkAccess.Internet)
            {
                Debug.WriteLine("[TriggerSync] No internet connection");
                // You could show a toast or alert here
                return;
            }

            Debug.WriteLine("[TriggerSync] Starting manual breathing sync...");
            try
            {
                var result = await _syncService.SyncBreathingSessionsImmediatelyAsync();
                Debug.WriteLine($"[TriggerSync] Breathing sync result: {result.IsSuccess} - {result.Message}");
                
                // Reload stats after successful sync
                if (result.IsSuccess)
                {
                    LoadStats();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TriggerSync] Breathing sync failed: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task TriggerFullSync()
        {
            if (_syncService == null)
            {
                Debug.WriteLine("[TriggerFullSync] Sync service not available");
                return;
            }

            if (Connectivity.NetworkAccess != NetworkAccess.Internet)
            {
                Debug.WriteLine("[TriggerFullSync] No internet connection");
                return;
            }

            Debug.WriteLine("[TriggerFullSync] Starting full data sync...");
            try
            {
                var result = await _syncService.SyncAllDataAsync(forceSync: true);
                Debug.WriteLine($"[TriggerFullSync] Full sync result: {result.IsSuccess} - {result.Message}");
                
                // Reload stats after successful sync
                if (result.IsSuccess)
                {
                    LoadStats();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TriggerFullSync] Full sync failed: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task CheckUnsyncedSessions()
        {
            if (_databaseService == null)
            {
                Debug.WriteLine("[CheckUnsynced] Database service not available");
                return;
            }

            try
            {
                var unsyncedSessions = await _databaseService.GetUnsyncedSessionsAsync();
                Debug.WriteLine($"[CheckUnsynced] Found {unsyncedSessions.Count} unsynced sessions");
                
                foreach (var session in unsyncedSessions)
                {
                    Debug.WriteLine($"[CheckUnsynced] Session {session.Id}: {session.TechniqueName}, Completed: {session.IsCompleted}, Synced: {session.IsSynced}");
                }

                var allSessions = await _databaseService.GetSessionsAsync();
                Debug.WriteLine($"[CheckUnsynced] Total sessions in database: {allSessions.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CheckUnsynced] Error: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task CheckLocalSessions()
        {
            if (_databaseService == null)
            {
                Debug.WriteLine("[CheckLocal] Database service not available");
                return;
            }

            try
            {
                var allSessions = await _databaseService.GetSessionsAsync();
                Debug.WriteLine($"[CheckLocal] Total sessions in local database: {allSessions.Count}");
                
                foreach (var session in allSessions)
                {
                    Debug.WriteLine($"[CheckLocal] Session {session.Id}: {session.TechniqueName}, " +
                                  $"Completed: {session.IsCompleted}, " +
                                  $"Synced: {session.IsSynced}, " +
                                  $"BackendId: {session.BackendId ?? "null"}, " +
                                  $"StartTime: {session.StartTime:yyyy-MM-dd HH:mm:ss}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CheckLocal] Error: {ex.Message}");
            }
        }

        // Calculated properties for UI
        public double WeeklyProgress => Math.Min(Stats.SessionsThisWeek / 7.0, 1.0);
        public bool WeekStreakAchievement => Stats.LongestStreak >= 7;
        public bool CenturionAchievement => Stats.TotalSessions >= 100;
        public bool BreathMillennialAchievement => Stats.TotalBreaths >= 1000;
        public bool TimeMasterAchievement => Stats.TotalDuration.TotalHours >= 10;

        [RelayCommand]
        private void CancelSession()
        {
            if (IsSessionActive)
            {
                Debug.WriteLine("Cancelling session...");
                _cancellationTokenSource?.Cancel();
                ResetSession();
            }
        }
    }
}
