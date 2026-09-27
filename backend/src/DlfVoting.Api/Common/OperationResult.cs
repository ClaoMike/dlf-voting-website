using Microsoft.AspNetCore.Mvc;

namespace DlfVoting.Api.Common;

public enum OperationStatus { Ok, Invalid, NotFound, Conflict, Forbidden }

/// <summary>
/// Outcome of a service call without a value. Failures carry the message shown to the admin/user;
/// they convert implicitly to <see cref="OperationResult{T}"/>, so services can return them from any method.
/// </summary>
public record OperationResult(OperationStatus Status, string? Message = null)
{
    public static readonly OperationResult Success = new(OperationStatus.Ok);
    public static OperationResult Invalid(string message) => new(OperationStatus.Invalid, message);
    public static OperationResult NotFound(string message) => new(OperationStatus.NotFound, message);
    public static OperationResult Conflict(string message) => new(OperationStatus.Conflict, message);
    public static OperationResult Forbidden(string message) => new(OperationStatus.Forbidden, message);
}

/// <summary>Outcome of a service call that produces a value on success.</summary>
public record OperationResult<T>(OperationStatus Status, T? Value = default, string? Message = null)
{
    public static OperationResult<T> Success(T value) => new(OperationStatus.Ok, value);

    public static implicit operator OperationResult<T>(OperationResult failure) => new(failure.Status, default, failure.Message);
}

public static class OperationResultExtensions
{
    /// <summary>Ok → 204 No Content; failures → their status code with <c>{ message }</c>.</summary>
    public static IActionResult ToActionResult(this ControllerBase controller, OperationResult result) =>
        result.Status == OperationStatus.Ok ? controller.NoContent() : Failure(controller, result.Status, result.Message);

    /// <summary>Ok → 200 with the (mapped) value; failures → their status code with <c>{ message }</c>.</summary>
    public static IActionResult ToActionResult<T>(this ControllerBase controller, OperationResult<T> result, Func<T, object>? map = null) =>
        result.Status == OperationStatus.Ok
            ? controller.Ok(map is null ? result.Value : map(result.Value!))
            : Failure(controller, result.Status, result.Message);

    private static IActionResult Failure(ControllerBase controller, OperationStatus status, string? message) => status switch
    {
        OperationStatus.Invalid => controller.BadRequest(new { message }),
        OperationStatus.NotFound => controller.NotFound(new { message }),
        OperationStatus.Conflict => controller.Conflict(new { message }),
        OperationStatus.Forbidden => controller.StatusCode(StatusCodes.Status403Forbidden, new { message }),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
