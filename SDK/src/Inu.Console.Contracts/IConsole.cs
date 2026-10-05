namespace Inu.Console.Contracts;

public interface IConsole
{
    bool Write(ReadOnlySpan<char> text);
    bool WriteLine(ReadOnlySpan<char> text);
    bool WriteLine();
}
