namespace DlfVoting.Api.Common;

public static class Paging
{
    /// <summary>
    /// Clamps a requested page to 1..the last page whose offset still fits in an int, so a huge "?page=" can't
    /// overflow into a negative OFFSET (which Postgres rejects).
    /// </summary>
    public static int ClampPage(int page, int pageSize) => Math.Clamp(page, 1, int.MaxValue / pageSize);
}
