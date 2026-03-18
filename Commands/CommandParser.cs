using System.Text.RegularExpressions;

namespace PSConsole.Commands
{
    public static class CommandParser
    {
        /// Разбирает аргументы команды Create.
        public static bool TryParseCreate(string args,
            out string filename, out short maxLength, out string prsFilename)
        {
            filename = null;
            prsFilename = null;
            maxLength = 0;

            if (string.IsNullOrWhiteSpace(args)) return false;

            var match = Regex.Match(args.Trim(),
                @"^(\S+)\((\d+)(?:,\s*(\S+))?\)$");

            if (!match.Success) return false;

            filename = match.Groups[1].Value;
            if (!short.TryParse(match.Groups[2].Value, out maxLength)) return false;

            prsFilename = match.Groups[3].Success
                ? System.IO.Path.ChangeExtension(match.Groups[3].Value, ".prs")
                : System.IO.Path.ChangeExtension(filename, ".prs");

            return true;
        }

        /// Извлекает имя компонента и строку типа из аргументов команды Input (компонент, тип).
        public static (string name, string type) ParseInputComponent(string args)
        {
            string name = args.Substring(
                args.IndexOf("(") + 1,
                args.IndexOf(",") - args.IndexOf("(") - 1).Trim();

            string type = args.Substring(
                args.IndexOf(", ") + 2,
                args.IndexOf(")") - args.IndexOf(", ") - 2).Trim();

            return (name, type);
        }

        /// Извлекает пару (parent, child) из аргументов вида (parent/child).
        public static (string parent, string child) ParseSlashPair(string args)
        {
            string inside = args[(args.IndexOf("(") + 1)..args.IndexOf(")")];
            var split = inside.Split('/');
            return (split[0].Trim(), split[1].Trim());
        }

        /// Извлекает имя из скобок: (name).
        public static string ParseSingleName(string args)
            => args.Substring(
                args.IndexOf("(") + 1,
                args.IndexOf(")") - args.IndexOf("(") - 1).Trim();
    }
}