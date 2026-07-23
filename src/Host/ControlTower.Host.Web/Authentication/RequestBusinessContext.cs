namespace ControlTower.Host.Web.Authentication;

public static class RequestBusinessContext
{
    public const string PurposeHeader = "X-Purpose";
    public const string ApprovalReferenceHeader = "X-Approval-Reference";

    public static bool TryGetPurpose(HttpContext context, out string purpose) =>
        TryGetSingleBoundedHeader(context, PurposeHeader, 512, out purpose);

    public static bool TryGetApprovalReference(
        HttpContext context,
        out string approvalReference) =>
        TryGetSingleBoundedHeader(
            context,
            ApprovalReferenceHeader,
            256,
            out approvalReference);

    private static bool TryGetSingleBoundedHeader(
        HttpContext context,
        string name,
        int maximumLength,
        out string value)
    {
        if (context.Request.Headers.TryGetValue(name, out var values)
            && values.Count == 1)
        {
            var candidate = values[0]?.Trim() ?? string.Empty;
            if (ControlTowerAuthenticationExtensions.IsBoundedText(
                    candidate,
                    maximumLength))
            {
                value = candidate;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }
}
