using System;

namespace Inu.Userland.Commands;

public static class Echo
{
    public static void Main(string[] args)
    {
        for (int index = 0; index < args.Length; index++)
        {
            if (index > 0)
            {
                Console.Write(" ");
            }

            Console.Write(args[index]);
        }

        Console.WriteLine();
    }
}
