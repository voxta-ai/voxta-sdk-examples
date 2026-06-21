namespace Voxta.ConsoleChat.Session;

public interface IConnectionProgress
{
    void Status(string message);
    void DeviceCode(string userCode, string verificationUrl);
}

public readonly record struct LoadingUpdate(double Progress, string Text);
