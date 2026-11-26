using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using UGTM.Modules.Notifications.Contracts.Email;
using UGTM.Modules.Notifications.Infrastructure.Email;

namespace UGTM.Architecture.Tests;

public sealed class SmtpEmailSenderBatchTests
{
    [Fact]
    public async Task SendBatchAsync_reuses_single_smtp_connection_for_entire_batch()
    {
        var transport = new FakeSmtpTransport();
        var createdTransports = 0;
        var sender = CreateSender(() =>
        {
            createdTransports++;
            return transport;
        });

        var items = new[]
        {
            new EmailBatchItem(Guid.NewGuid(), new EmailMessage("sv1@gm.uit.edu.vn", "Nhắc hạn 1", "<p>1</p>")),
            new EmailBatchItem(Guid.NewGuid(), new EmailMessage("sv2@gm.uit.edu.vn", "Nhắc hạn 2", "<p>2</p>")),
            new EmailBatchItem(Guid.NewGuid(), new EmailMessage("sv3@gm.uit.edu.vn", "Nhắc hạn 3", "<p>3</p>"))
        };

        var results = await sender.SendBatchAsync(items, CancellationToken.None);

        Assert.Equal(1, createdTransports);
        Assert.Equal(1, transport.ConnectCount);
        Assert.Equal(1, transport.AuthenticateCount);
        Assert.Equal(3, transport.SentRecipients.Count);
        Assert.Equal(1, transport.DisconnectCount);
        Assert.True(transport.Disposed);
        Assert.All(results, r => Assert.True(r.Succeeded));
        Assert.Equal(items.Select(i => i.Id), results.Select(r => r.Id));
    }

    [Fact]
    public async Task SendBatchAsync_isolates_invalid_email_and_550_recipient_rejection_without_stopping_remaining_emails()
    {
        var transport = new FakeSmtpTransport
        {
            OnSend = message =>
            {
                var to = message.To.Mailboxes.Single().Address;
                if (to == "missing@gm.uit.edu.vn")
                {
                    throw new SmtpCommandException(
                        SmtpErrorCode.RecipientNotAccepted,
                        SmtpStatusCode.MailboxUnavailable,
                        new MailboxAddress("", to),
                        "5.1.1 User unknown");
                }
            }
        };

        var sender = CreateSender(() => transport);
        var idInvalid = Guid.NewGuid();
        var idValid1 = Guid.NewGuid();
        var idRejected550 = Guid.NewGuid();
        var idValid2 = Guid.NewGuid();

        var results = await sender.SendBatchAsync(
        [
            new EmailBatchItem(idInvalid, new EmailMessage("not-an-email@@@", "Subj", "<p>x</p>")),
            new EmailBatchItem(idValid1, new EmailMessage("ok1@gm.uit.edu.vn", "Subj", "<p>1</p>")),
            new EmailBatchItem(idRejected550, new EmailMessage("missing@gm.uit.edu.vn", "Subj", "<p>2</p>")),
            new EmailBatchItem(idValid2, new EmailMessage("ok2@gm.uit.edu.vn", "Subj", "<p>3</p>"))
        ]);

        Assert.Equal(4, results.Count);
        Assert.False(results[0].Succeeded);
        Assert.True(results[0].IsPermanentFailure);
        Assert.Equal("InvalidEmailFormat", results[0].ErrorCode);

        Assert.True(results[1].Succeeded);

        Assert.False(results[2].Succeeded);
        Assert.True(results[2].IsPermanentFailure);
        Assert.Equal("Smtp550", results[2].ErrorCode);

        Assert.True(results[3].Succeeded);
        Assert.Equal(1, transport.ConnectCount);
    }

    [Fact]
    public async Task SendBatchAsync_reconnects_after_transient_transport_drop_and_marks_failed_item_retryable()
    {
        var transportCalls = 0;
        var sender = CreateSender(() =>
        {
            transportCalls++;
            var attempt = transportCalls;
            return new FakeSmtpTransport
            {
                OnSend = _ =>
                {
                    if (attempt == 1)
                    {
                        throw new IOException("Connection reset by peer");
                    }
                }
            };
        });

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var results = await sender.SendBatchAsync(
        [
            new EmailBatchItem(id1, new EmailMessage("sv1@gm.uit.edu.vn", "Subj 1", "<p>1</p>")),
            new EmailBatchItem(id2, new EmailMessage("sv2@gm.uit.edu.vn", "Subj 2", "<p>2</p>"))
        ]);

        Assert.Equal(2, transportCalls);
        Assert.False(results[0].Succeeded);
        Assert.False(results[0].IsPermanentFailure);
        Assert.Equal(nameof(IOException), results[0].ErrorCode);
        Assert.True(results[1].Succeeded);
    }

    [Fact]
    public async Task SendBatchAsync_marks_all_pending_items_retryable_when_smtp_connect_fails()
    {
        var sender = CreateSender(() => new FakeSmtpTransport
        {
            FailConnectWith = new TimeoutException("SMTP connect timed out")
        });

        var items = new[]
        {
            new EmailBatchItem(Guid.NewGuid(), new EmailMessage("sv1@gm.uit.edu.vn", "S1", "<p>1</p>")),
            new EmailBatchItem(Guid.NewGuid(), new EmailMessage("sv2@gm.uit.edu.vn", "S2", "<p>2</p>"))
        };

        var results = await sender.SendBatchAsync(items);

        Assert.All(results, r =>
        {
            Assert.False(r.Succeeded);
            Assert.False(r.IsPermanentFailure);
            Assert.Equal("SmtpTimeout", r.ErrorCode);
        });
    }

    private static SmtpEmailSender CreateSender(Func<ISmtpTransport> factory)
    {
        var options = Options.Create(new SmtpOptions
        {
            Host = "smtp.example.com",
            Port = 587,
            FromAddress = "no-reply@ugtm.uit.edu.vn",
            FromName = "UGTM",
            Username = "no-reply@ugtm.uit.edu.vn",
            Password = "secret",
            Security = SmtpSecurityMode.StartTls,
            TimeoutSeconds = 30
        });

        return new SmtpEmailSender(options, NullLogger<SmtpEmailSender>.Instance, factory);
    }

    private sealed class FakeSmtpTransport : ISmtpTransport
    {
        public bool IsConnected { get; private set; }
        public int ConnectCount { get; private set; }
        public int AuthenticateCount { get; private set; }
        public int DisconnectCount { get; private set; }
        public bool Disposed { get; private set; }
        public List<string> SentRecipients { get; } = [];
        public Exception? FailConnectWith { get; init; }
        public Action<MimeMessage>? OnSend { get; init; }

        public Task ConnectAsync(
            string host,
            int port,
            SecureSocketOptions security,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            ConnectCount++;
            if (FailConnectWith is not null)
            {
                throw FailConnectWith;
            }

            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task AuthenticateAsync(string username, string password, CancellationToken cancellationToken)
        {
            AuthenticateCount++;
            return Task.CompletedTask;
        }

        public Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
        {
            OnSend?.Invoke(message);
            SentRecipients.AddRange(message.To.Mailboxes.Select(m => m.Address));
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(bool quit, CancellationToken cancellationToken)
        {
            DisconnectCount++;
            IsConnected = false;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Disposed = true;
            IsConnected = false;
        }
    }
}
