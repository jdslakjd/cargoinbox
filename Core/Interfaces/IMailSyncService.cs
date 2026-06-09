namespace CargoInbox.Core.Interfaces;

public interface IMailSyncService
{
    Task SyncUserMailsAsync(string configId);
}
