namespace UGTM.Modules.Notifications.Contracts.Email;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);

    async Task<IReadOnlyList<EmailBatchItemResult>> SendBatchAsync(
        IReadOnlyList<EmailBatchItem> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            return [];
        }

        var results = new List<EmailBatchItemResult>(items.Count);
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await SendAsync(item.Message, cancellationToken);
                results.Add(new EmailBatchItemResult(item.Id, Succeeded: true));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                results.Add(new EmailBatchItemResult(
                    item.Id,
                    Succeeded: false,
                    IsPermanentFailure: ex is FormatException,
                    ErrorCode: ex.GetType().Name,
                    ErrorMessage: ex.Message));
            }
        }

        return results;
    }
}
