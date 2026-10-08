using Prime.Application.Common.Interfaces;

namespace Prime.Application.Common;

/// <summary>
/// The checker's side of maker-checker (CLAUDE.md §46): a decision is refused when the acting user is unknown, so an
/// unattributed request can never pass as "someone else" (docs/analysis/workflow-security.md §4.6, G7), and when the
/// acting user made the record. Background jobs act as the user who started them.
/// </summary>
internal static class MakerChecker
{
    public const string UnknownUserCode = "APPROVING_USER_UNKNOWN";
    public const string UnknownUserMessage = "The approving user is not known: an approval is made by a signed-in user (CLAUDE.md §46).";

    /// <summary>Null when the acting user may decide a record made by <paramref name="makerId"/>; else the code and message of the refusal.</summary>
    public static (string Code, string Message)? Refusal(ICurrentUserService currentUser, Guid? makerId, string ownCode, string ownMessage) =>
        currentUser.AppUserId is not { } me ? (UnknownUserCode, UnknownUserMessage)
        : me == makerId ? (ownCode, ownMessage)
        : null;
}
