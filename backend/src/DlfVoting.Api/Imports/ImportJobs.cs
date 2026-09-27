using System.Collections.Concurrent;
using DlfVoting.Api.Common;

namespace DlfVoting.Api.Imports;

/// <summary>How far an import has got: passwords hashed out of the users being created.</summary>
public sealed class ImportProgress
{
    private int _total;
    private int _done;

    public int Total => Volatile.Read(ref _total);
    public int Done => Volatile.Read(ref _done);

    public void Start(int total) => Volatile.Write(ref _total, total);
    public void Advance() => Interlocked.Increment(ref _done);
}

public enum ImportJobStatus { Running, Succeeded, Failed }

public sealed class ImportJob
{
    public Guid Id { get; } = Guid.NewGuid();
    public required Guid StartedBy { get; init; }
    public ImportProgress Progress { get; } = new();

    // Written once by the background task, read by status requests.
    private volatile ImportJobStatus _status = ImportJobStatus.Running;
    public ImportJobStatus Status => _status;
    public object? Result { get; private set; }
    public string? FailureMessage { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }

    public void Succeed(object result, DateTimeOffset now)
    {
        Result = result;
        FinishedAt = now;
        _status = ImportJobStatus.Succeeded;
    }

    public void Fail(string message, DateTimeOffset now)
    {
        FailureMessage = message;
        FinishedAt = now;
        _status = ImportJobStatus.Failed;
    }
}

/// <summary>
/// Runs Excel imports in the background. Hashing hundreds of passwords takes minutes on a small App Service plan,
/// longer than Azure lets a single request stay open (230 s); a request that runs out loses the response, and with
/// it the only copy of the generated passwords. So the upload starts a job and the browser polls for its result.
///
/// Held in memory: a result contains plaintext passwords, so it is only shown to the administrator who started the
/// import and is dropped an hour after the import finishes (or when they delete it). One import runs at a time.
/// Jobs don't survive a restart; an interrupted import saves nothing, because each import saves all users at once.
/// </summary>
public sealed class ImportJobs
{
    public static readonly TimeSpan ResultLifetime = TimeSpan.FromHours(1);
    public const string AlreadyRunningMessage = "An import is already running. Please wait for it to finish.";
    private const string UnexpectedFailureMessage = "The import failed unexpectedly. No users were created; please try again.";

    private readonly ConcurrentDictionary<Guid, ImportJob> _jobs = new();
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _time;
    private readonly ILogger<ImportJobs> _logger;
    private int _running;

    // ReSharper disable once ConvertToPrimaryConstructor
    public ImportJobs(IServiceScopeFactory scopes, TimeProvider time, ILogger<ImportJobs> logger)
    {
        _scopes = scopes;
        _time = time;
        _logger = logger;
    }

    /// <summary>Starts <paramref name="run"/> in the background, or returns null while another import is running.</summary>
    public ImportJob? TryStart<TResult>(Guid administratorId, Func<IServiceProvider, ImportProgress, Task<OperationResult<TResult>>> run)
        where TResult : class
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return null;
        RemoveExpired();

        var job = new ImportJob { StartedBy = administratorId };
        _jobs[job.Id] = job;

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var result = await run(scope.ServiceProvider, job.Progress);
                if (result.Status == OperationStatus.Ok)
                    job.Succeed(result.Value!, _time.GetUtcNow());
                else
                    job.Fail(result.Message ?? UnexpectedFailureMessage, _time.GetUtcNow());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Import {JobId} failed", job.Id);
                job.Fail(UnexpectedFailureMessage, _time.GetUtcNow());
            }
            finally
            {
                Volatile.Write(ref _running, 0);
            }
        });

        return job;
    }

    /// <summary>The job, if it exists and was started by this administrator.</summary>
    public ImportJob? Find(Guid jobId, Guid administratorId)
    {
        RemoveExpired();
        return _jobs.TryGetValue(jobId, out var job) && job.StartedBy == administratorId ? job : null;
    }

    /// <summary>Forgets a finished job (and its passwords). Running jobs can't be removed.</summary>
    public bool Remove(Guid jobId, Guid administratorId)
    {
        var job = Find(jobId, administratorId);
        return job is { Status: not ImportJobStatus.Running } && _jobs.TryRemove(jobId, out _);
    }

    private void RemoveExpired()
    {
        var cutoff = _time.GetUtcNow() - ResultLifetime;
        foreach (var (id, job) in _jobs)
        {
            if (job.FinishedAt < cutoff) _jobs.TryRemove(id, out _);
        }
    }
}
