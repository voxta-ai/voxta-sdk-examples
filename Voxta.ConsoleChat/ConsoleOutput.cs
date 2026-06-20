namespace Voxta.ConsoleChat;

public interface IConsoleOutput
{
    void Write(string text, ConsoleColor color = ConsoleColor.White);
    void WriteLine(string text, ConsoleColor color = ConsoleColor.White);
    void WriteErrorLine(string text);
}

public class ConsoleOutput : IConsoleOutput
{
    public void Write(string text, ConsoleColor color = ConsoleColor.White)
    {
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ResetColor();
    }
    
    public void WriteLine(string text, ConsoleColor color = ConsoleColor.White)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }
    
    public void WriteErrorLine(string text)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(text);
        Console.ResetColor();
    }
}