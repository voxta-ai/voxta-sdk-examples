namespace Voxta.ConsoleChat.Session;

public enum ChatRole
{
    Character,
    User,
    System,
}

public readonly record struct ChatMessage(ChatRole Role, string Speaker, string Text);
