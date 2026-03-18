using PSConsole.FileIO;
using PSConsole.Models;
using System;
using System.IO;

namespace PSConsole.Commands
{
    /// Разбирает введённую строку и вызывает нужный обработчик.
    public class CommandDispatcher
    {
        private readonly FileContext _ctx;
        private readonly ComponentRepository _repo;
        private readonly TruncateService _truncate;

        public CommandDispatcher()
        {
            _ctx = new FileContext();
            _repo = new ComponentRepository(_ctx);
            _truncate = new TruncateService(_ctx);
        }

        public void Run()
        {
            while (true)
            {
                try
                {
                    Console.Write("PS> ");
                    var cmd = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(cmd)) continue;

                    var parts = cmd.Split(' ', 2);
                    var command = parts[0];
                    var args = parts.Length > 1 ? parts[1] : "";

                    switch (command)
                    {
                        case "Create": HandleCreate(args); break;
                        case "Open": HandleOpen(args); break;
                        case "Input": HandleInput(args); break;
                        case "Delete": HandleDelete(args); break;
                        case "Restore": HandleRestore(args); break;
                        case "Truncate": HandleTruncate(); break;
                        case "Print": HandlePrint(args); break;
                        case "Help": HandleHelp(args); break;
                        case "Exit": HandleExit(); break;
                        default:
                            throw new Exception($"Неизвестная команда '{command}'.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка: {ex.Message}");
                    Console.WriteLine("Введите Help для списка команд.");
                }
            }
        }

        private void HandleCreate(string args)
        {
            if (!CommandParser.TryParseCreate(args,
                    out string filename, out short maxLen, out string prsFile))
                throw new Exception(
                    "Неверный формат. Используйте: Create <имя_файла>(<длина_записи>[, <имя_файла_спецификаций>])");

            filename = Path.ChangeExtension(filename, ".prd");
            Console.WriteLine($"Файл компонентов: {filename}");
            Console.WriteLine($"Файл спецификаций: {prsFile}");
            Console.WriteLine($"Длина записи: {maxLen}");

            if (_ctx.Create(filename, prsFile, maxLen))
                Console.WriteLine("Файл успешно создан.");
        }

        private void HandleOpen(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
                throw new Exception("Укажите имя файла.");

            string filename = Path.ChangeExtension(args.Trim(), ".prd");

            if (!_ctx.Open(filename))
                throw new Exception($"Не удалось открыть файл '{filename}'.");

            Console.WriteLine($"Файл компонентов: '{_ctx.CurrentFile}'");
            Console.WriteLine($"Файл спецификаций: '{_ctx.SpecFile}'");
            Console.WriteLine("Файлы открыты.");
        }

        private void HandleInput(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
                throw new Exception("Нет параметров.");

            if (!args.Contains("(") || !args.Contains(")"))
                throw new Exception(
                    "Неверный формат. Используйте: Input (компонент, тип) или Input (компонент/деталь)");

            if (args.Contains("/"))
                HandleInputSpec(args);
            else
                HandleInputComponent(args);
        }

        private void HandleInputSpec(string args)
        {
            var (parent, child) = CommandParser.ParseSlashPair(args);

            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(child))
                throw new Exception("Имена не могут быть пустыми.");

            Console.WriteLine($"Компонент: {parent}");
            Console.WriteLine($"Деталь: {child}");

            if (!_ctx.IsOpen) throw new Exception("Сначала откройте файл (Open).");

            _repo.AddSpec(parent, child);
        }

        private void HandleInputComponent(string args)
        {
            if (!args.Contains(","))
                throw new Exception("Нужна запятая. Используйте: Input (компонент, тип)");

            var (name, typeStr) = CommandParser.ParseInputComponent(args);

            if (string.IsNullOrWhiteSpace(name))
                throw new Exception("Имя компонента не может быть пустым.");

            Console.WriteLine($"Компонент: {name}");
            Console.WriteLine($"Тип: {typeStr}");

            if (!_ctx.IsOpen) throw new Exception("Сначала откройте файл (Open).");

            ComponentType type;
            if (int.TryParse(typeStr, out int typeNum))
            {
                if (!Enum.IsDefined(typeof(ComponentType), (byte)typeNum))
                    throw new Exception(
                        "Неверный тип компонента. Допустимые значения: 1 (Product), 2 (Unit), 3 (Detail)");
                type = (ComponentType)typeNum;
            }
            else if (!Enum.TryParse(typeStr, ignoreCase: true, out type))
            {
                throw new Exception("Неверный тип компонента. Используйте: Product, Unit или Detail");
            }

            _repo.AddComponent(name, type);
            Console.WriteLine("Компонент добавлен.");
        }

        private void HandleDelete(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
                throw new Exception("Нет параметров.");

            if (!args.Contains("(") || !args.Contains(")"))
                throw new Exception(
                    "Неверный формат. Используйте: Delete (компонент) или Delete (компонент/деталь)");

            if (!_ctx.IsOpen) throw new Exception("Сначала откройте файл (Open).");

            if (args.Contains("/"))
            {
                var (parent, child) = CommandParser.ParseSlashPair(args);
                Console.WriteLine($"Удалить компонент: {parent}");
                Console.WriteLine($"Удалить деталь: {child}");
                _repo.DeleteSpec(parent, child);
            }
            else
            {
                string name = CommandParser.ParseSingleName(args);
                Console.WriteLine($"Удалить компонент: {name}");
                _repo.DeleteComponent(name);
            }
        }

        private void HandleRestore(string args)
        {
            if (!_ctx.IsOpen) throw new Exception("Сначала откройте файл (Open).");

            if (args.Contains("*"))
            {
                Console.WriteLine("Восстановить всё");
                int count = _repo.RestoreAll();
                Console.WriteLine($"Восстановлено записей: {count}");
            }
            else
            {
                if (!args.Contains("(") || !args.Contains(")"))
                    throw new Exception("Используйте: Restore * или Restore (компонент)");

                string name = CommandParser.ParseSingleName(args);
                Console.WriteLine($"Восстановить: {name}");
                _repo.Restore(name);
            }
        }

        private void HandleTruncate()
        {
            if (!_ctx.IsOpen) throw new Exception("Сначала откройте файл (Open).");
            Console.WriteLine("Очистка...");
            _truncate.Truncate();
        }

        private void HandlePrint(string args)
        {
            if (!_ctx.IsOpen) throw new Exception("Сначала откройте файл (Open).");

            if (args.Contains("*"))
            {
                _repo.PrintAll();
            }
            else
            {
                if (!args.Contains("(") || !args.Contains(")"))
                    throw new Exception("Используйте: Print * или Print (компонент)");

                string name = CommandParser.ParseSingleName(args);
                _repo.Print(name);
            }
        }

        private void HandleHelp(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
                HelpCommand.Display();
            else
            {
                HelpCommand.SaveToFile(args);
                Console.WriteLine($"Справка сохранена в {args}");
            }
        }

        private void HandleExit()
        {
            _ctx.Close();
            Environment.Exit(0);
        }
    }
}