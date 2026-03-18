using PSConsole.Commands;

namespace PSConsole
{
    class Program
    {
        static void Main()
        {
            new CommandDispatcher().Run();
        }
    }
}