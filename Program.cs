using System;
using System.IO;

namespace PSConsole
{
    class Program
    {
        static FileStream compFs;
        static BinaryReader compReader;
        static BinaryWriter compWriter;
        static string currentFile;
        static FileStream specFs;
        static BinaryReader specReader;
        static BinaryWriter specWriter;
        static string specFile;

        public enum ComponentType : byte
        {
            Product = 1,
            Unit = 2,
            Detail = 3
        }

        static void Main()
        {
            while (true)
            {
                Console.Write("PS> ");
                var input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input)) continue;

                var parts = input.Split(' ', 2);
                var command = parts[0];
                var commandParams = parts.Length > 1 ? parts[1] : "";

                try
                {
                    switch (command)
                    {
                        case "Create":
                            if (TryParseCreateCommand(commandParams, out string filename, out short maxLength))
                            {
                                Console.WriteLine($"Имя файла: {filename}");
                                Console.WriteLine($"Длина записи: {maxLength}");
                                Create(filename, maxLength);
                                Console.WriteLine("Файл успешно создан.");
                            }
                            else
                            {
                                throw new Exception("Неверный формат. Используйте: Create <имя_файла> <длина_записи>");
                            }
                            break;

                        case "Open":
                            if (string.IsNullOrWhiteSpace(commandParams))
                                throw new Exception("Укажите имя файла.");

                            filename = commandParams;
                            Open(filename);
                            break;

                        case "Input":
                            if (string.IsNullOrWhiteSpace(commandParams))
                                throw new Exception("Нет параметров.");

                            if (!commandParams.Contains("(") || !commandParams.Contains(")"))
                                throw new Exception("Неверный формат. Используйте: Input(компонент, тип) или Input(компонент/деталь)");

                            if (commandParams.Contains("/"))
                            {
                                string componentName = commandParams.Substring(
                                    commandParams.IndexOf("(") + 1,
                                    commandParams.IndexOf("/") - commandParams.IndexOf("(") - 1);

                                string detailName = commandParams.Substring(
                                    commandParams.IndexOf("/") + 1,
                                    commandParams.IndexOf(")") - commandParams.IndexOf("/") - 1);

                                if (string.IsNullOrWhiteSpace(componentName) || string.IsNullOrWhiteSpace(detailName))
                                    throw new Exception("Имена не могут быть пустыми.");

                                Console.WriteLine($"Компонент: {componentName}");
                                Console.WriteLine($"Деталь: {detailName}");

                                // Вызов метода для добавления спецификации
                                InputSpec($"{componentName}/{detailName}");
                            }
                            else
                            {
                                if (!commandParams.Contains(","))
                                    throw new Exception("Нужна запятая. Используйте: Input(компонент, тип)");

                                string componentName = commandParams.Substring(
                                    commandParams.IndexOf("(") + 1,
                                    commandParams.IndexOf(",") - commandParams.IndexOf("(") - 1).Trim();

                                string componentType = commandParams.Substring(
                                    commandParams.IndexOf(",") + 1,
                                    commandParams.IndexOf(")") - commandParams.IndexOf(",") - 1).Trim();

                                if (string.IsNullOrWhiteSpace(componentName))
                                    throw new Exception("Имя компонента не может быть пустым.");

                                Console.WriteLine($"Компонент: {componentName}");
                                Console.WriteLine($"Тип: {componentType}");

                                if (compFs == null)
                                    throw new Exception("Сначала откройте файл (Open).");

                                if (Enum.TryParse<ComponentType>(componentType, true, out ComponentType type))
                                {
                                    Input(componentName, type);
                                    Console.WriteLine("Компонент добавлен.");
                                }
                                else
                                {
                                    throw new Exception($"Неверный тип компонента. Допустимые значения: Product, Unit, Detail");
                                }
                            }
                            break;

                        case "Delete":
                            if (string.IsNullOrWhiteSpace(commandParams))
                                throw new Exception("Нет параметров.");

                            if (!commandParams.Contains("(") || !commandParams.Contains(")"))
                                throw new Exception("Неверный формат. Используйте: Delete(компонент) или Delete(компонент/деталь)");

                            if (commandParams.Contains("/"))
                            {
                                string componentName = commandParams.Substring(
                                    commandParams.IndexOf("(") + 1,
                                    commandParams.IndexOf("/") - commandParams.IndexOf("(") - 1);

                                string detailName = commandParams.Substring(
                                    commandParams.IndexOf("/") + 1,
                                    commandParams.IndexOf(")") - commandParams.IndexOf("/") - 1);

                                Console.WriteLine($"Удалить компонент: {componentName}");
                                Console.WriteLine($"Удалить деталь: {detailName}");
                                // Здесь можно добавить логику удаления спецификации
                            }
                            else
                            {
                                string componentName = commandParams.Substring(
                                    commandParams.IndexOf("(") + 1,
                                    commandParams.IndexOf(")") - commandParams.IndexOf("(") - 1).Trim();

                                Console.WriteLine($"Удалить компонент: {componentName}");
                                Delete(componentName);
                            }
                            break;

                        case "Restore":
                            if (commandParams.Contains("*"))
                            {
                                Console.WriteLine("Восстановить всё");
                                RestoreAll();
                            }
                            else
                            {
                                if (!commandParams.Contains("(") || !commandParams.Contains(")"))
                                    throw new Exception("Используйте: Restore(*) или Restore(компонент)");

                                string componentName = commandParams.Substring(
                                    commandParams.IndexOf("(") + 1,
                                    commandParams.IndexOf(")") - commandParams.IndexOf("(") - 1).Trim();

                                Console.WriteLine($"Восстановить: {componentName}");
                                Restore(componentName);
                            }
                            break;

                        case "Truncate":
                            Console.WriteLine("Очистка...");
                            Truncate();
                            break;

                        case "Print":
                            if (commandParams.Contains("*"))
                            {
                                Console.WriteLine("Печать всего");
                                Print("*");
                            }
                            else
                            {
                                if (!commandParams.Contains("(") || !commandParams.Contains(")"))
                                    throw new Exception("Используйте: Print(*) или Print(компонент)");

                                string componentName = commandParams.Substring(
                                    commandParams.IndexOf("(") + 1,
                                    commandParams.IndexOf(")") - commandParams.IndexOf("(") - 1).Trim();

                                Console.WriteLine($"Печать: {componentName}");
                                Print(componentName);
                            }
                            break;

                        case "Help":
                            if (string.IsNullOrWhiteSpace(commandParams))
                            {
                                DisplayHelpToConsole();
                            }
                            else
                            {
                                SaveHelpToFile(commandParams);
                                Console.WriteLine($"Справка сохранена в {commandParams}");
                            }
                            break;

                        case "Exit":
                            Close();
                            Environment.Exit(0);
                            break;

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

        static bool TryParseCreateCommand(string input, out string filename, out short maxLength)
        {
            filename = null;
            maxLength = 0;

            var parts = input.Split(' ');
            if (parts.Length != 2)
                return false;

            filename = parts[0];
            return short.TryParse(parts[1], out maxLength);
        }

        static void DisplayHelpToConsole()
        {
            Console.WriteLine("Доступные команды:");
            Console.WriteLine("  Create <имя_файла> <длина_записи> - создать новый файл");
            Console.WriteLine("  Open <имя_файла> - открыть существующий файл");
            Console.WriteLine("  Input(компонент, тип) - добавить компонент (тип: Product/Unit/Detail)");
            Console.WriteLine("  Input(компонент/деталь) - добавить спецификацию");
            Console.WriteLine("  Delete(компонент) - удалить компонент");
            Console.WriteLine("  Delete(компонент/деталь) - удалить спецификацию");
            Console.WriteLine("  Restore(*) - восстановить все удаленные записи");
            Console.WriteLine("  Restore(компонент) - восстановить компонент");
            Console.WriteLine("  Truncate - физически удалить помеченные записи");
            Console.WriteLine("  Print(*) - вывести все компоненты");
            Console.WriteLine("  Print(компонент) - вывести спецификацию компонента");
            Console.WriteLine("  Help [файл] - показать справку или сохранить в файл");
            Console.WriteLine("  Exit - выход");
        }

        static void SaveHelpToFile(string filename)
        {
            using (var writer = new StreamWriter(filename))
            {
                writer.WriteLine("Справка по командам PSConsole:");
                writer.WriteLine("  Create <имя_файла> <длина_записи> - создать новый файл");
                writer.WriteLine("  Open <имя_файла> - открыть существующий файл");
                writer.WriteLine("  Input(компонент, тип) - добавить компонент (тип: Product/Unit/Detail)");
                writer.WriteLine("  Input(компонент/деталь) - добавить спецификацию");
                writer.WriteLine("  Delete(компонент) - удалить компонент");
                writer.WriteLine("  Delete(компонент/деталь) - удалить спецификацию");
                writer.WriteLine("  Restore(*) - восстановить все удаленные записи");
                writer.WriteLine("  Restore(компонент) - восстановить компонент");
                writer.WriteLine("  Truncate - физически удалить помеченные записи");
                writer.WriteLine("  Print(*) - вывести все компоненты");
                writer.WriteLine("  Print(компонент) - вывести спецификацию компонента");
                writer.WriteLine("  Help [файл] - показать справку или сохранить в файл");
                writer.WriteLine("  Exit - выход");
            }
        }

        // Методы работы с файлами из второго фрагмента
        static void Create(string name, short maxLength)
        {
            // файл компонентов
            using (var fs = new FileStream(name, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write((byte)'P');
                bw.Write((byte)'S');
                bw.Write(maxLength);
                bw.Write(-1);
                bw.Write((int)fs.Position + 4);
            }

            // файл спецификаций
            string prs = Path.ChangeExtension(name, ".prs");

            using (var fs = new FileStream(prs, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(-1); // first
                bw.Write(8);  // free
            }

            Console.WriteLine("Файлы PS и PRS созданы");
        }

        static void Open(string name)
        {
            if (compFs != null)
                Close();

            currentFile = name;
            specFile = Path.ChangeExtension(name, ".prs");

            compFs = new FileStream(name, FileMode.Open, FileAccess.ReadWrite);
            compReader = new BinaryReader(compFs);
            compWriter = new BinaryWriter(compFs);

            specFs = new FileStream(specFile, FileMode.Open, FileAccess.ReadWrite);
            specReader = new BinaryReader(specFs);
            specWriter = new BinaryWriter(specFs);

            compFs.Seek(0, SeekOrigin.Begin);
            if (compReader.ReadByte() != 'P' || compReader.ReadByte() != 'S')
            {
                Console.WriteLine("Ошибка сигнатуры");
                Close();
                return;
            }

            Console.WriteLine($"Файл '{name}' открыт.");
        }

        static void Input(string name, ComponentType type)
        {
            if (compFs == null)
                throw new Exception("Сначала откройте файл (Open).");

            compFs.Seek(2, SeekOrigin.Begin);
            short maxLen = compReader.ReadInt16();
            int head = compReader.ReadInt32();

            compFs.Seek(0, SeekOrigin.End);
            int offset = (int)compFs.Position;

            compWriter.Write(head);       // Next
            compWriter.Write(-1);         // SpecHead
            compWriter.Write((byte)0);    // Deleted
            compWriter.Write((byte)type); // Type

            char[] buf = new char[maxLen];
            name.CopyTo(0, buf, 0, Math.Min(name.Length, maxLen));
            compWriter.Write(buf);

            compFs.Seek(4, SeekOrigin.Begin);
            compWriter.Write(offset);
        }

        static void InputSpec(string args)
        {
            if (compFs == null || specFs == null)
                throw new Exception("Сначала откройте файл (Open).");

            var inside = args[(args.IndexOf("(") + 1)..args.IndexOf(")")];
            var split = inside.Split('/');

            string parent = split[0].Trim();
            string child = split[1].Trim();

            int parentOffset = FindComponent(parent);
            int childOffset = FindComponent(child);

            if (parentOffset == -1 || childOffset == -1)
            {
                Console.WriteLine("Компонент не найден");
                return;
            }

            // читаем голову списка
            specFs.Seek(0, SeekOrigin.Begin);
            int head = specReader.ReadInt32();

            int cur = head;

            while (cur != -1)
            {
                specFs.Seek(cur, SeekOrigin.Begin);

                byte del = specReader.ReadByte();
                int comp = specReader.ReadInt32();
                short count = specReader.ReadInt16();
                int next = specReader.ReadInt32();

                // если уже есть такой child → увеличиваем count
                if (comp == childOffset && del == 0)
                {
                    specFs.Seek(cur + 5, SeekOrigin.Begin);
                    specWriter.Write((short)(count + 1));

                    Console.WriteLine("Кратность увеличена");
                    return;
                }

                cur = next;
            }

            // если записи нет → создаём новую
            specFs.Seek(0, SeekOrigin.End);
            int newOffset = (int)specFs.Position;

            specWriter.Write((byte)0);      // не удалено
            specWriter.Write(childOffset);  // компонент
            specWriter.Write((short)1);     // количество
            specWriter.Write(head);          // next (старая голова)

            // новая запись становится головой
            specFs.Seek(0, SeekOrigin.Begin);
            specWriter.Write(newOffset);
            specWriter.Write(8);  // free (не используется)

            Console.WriteLine("Спецификация добавлена");
        }

        static int FindComponent(string name)
        {
            compFs.Seek(2, SeekOrigin.Begin);
            short len = compReader.ReadInt16();
            int head = compReader.ReadInt32();

            while (head != -1)
            {
                compFs.Seek(head, SeekOrigin.Begin);

                int next = compReader.ReadInt32();
                compReader.ReadInt32(); // SpecHead
                byte del = compReader.ReadByte();
                compReader.ReadByte(); // Type
                string cur = new string(compReader.ReadChars(len)).Trim('\0');

                if (cur == name && del == 0)
                    return head;

                head = next;
            }

            return -1;
        }

        static void Print(string param)
        {
            if (compFs == null)
                throw new Exception("Сначала откройте файл (Open).");

            if (param == "*")
            {
                compFs.Seek(2, SeekOrigin.Begin);
                short len = compReader.ReadInt16();
                int head = compReader.ReadInt32();

                Console.WriteLine("Список компонентов:");
                Console.WriteLine("-------------------");
                while (head != -1)
                {
                    compFs.Seek(head, SeekOrigin.Begin);
                    int next = compReader.ReadInt32();
                    int spec = compReader.ReadInt32();
                    byte del = compReader.ReadByte();
                    byte type = compReader.ReadByte();
                    string name = new string(compReader.ReadChars(len)).Trim('\0');

                    if (del == 0)
                        Console.WriteLine($"{name,-20} {(ComponentType)type}");

                    head = next;
                }
            }
            else
            {
                // Здесь можно реализовать вывод спецификации компонента
                Console.WriteLine($"Вывод спецификации для {param} (в разработке)");
            }
        }

        static void Delete(string name)
        {
            if (compFs == null)
                throw new Exception("Сначала откройте файл (Open).");

            compFs.Seek(2, SeekOrigin.Begin);
            short len = compReader.ReadInt16();
            int head = compReader.ReadInt32();

            while (head != -1)
            {
                compFs.Seek(head, SeekOrigin.Begin);

                int next = compReader.ReadInt32();
                compReader.ReadInt32(); // SpecHead
                long delPos = compFs.Position;
                byte deleted = compReader.ReadByte();
                compReader.ReadByte(); // Type
                string curName = new string(compReader.ReadChars(len)).Trim('\0');

                if (curName == name && deleted == 0)
                {
                    compFs.Seek(delPos, SeekOrigin.Begin);
                    compWriter.Write((byte)1);
                    Console.WriteLine("Запись помечена как удалённая");
                    return;
                }

                head = next;
            }

            Console.WriteLine("Компонент не найден");
        }

        static void RestoreAll()
        {
            if (compFs == null)
                throw new Exception("Сначала откройте файл (Open).");

            compFs.Seek(2, SeekOrigin.Begin);
            short len = compReader.ReadInt16();
            int head = compReader.ReadInt32();

            int count = 0;

            while (head != -1)
            {
                compFs.Seek(head, SeekOrigin.Begin);

                int next = compReader.ReadInt32();
                compReader.ReadInt32();
                long delPos = compFs.Position;
                byte deleted = compReader.ReadByte();

                if (deleted == 1)
                {
                    compFs.Seek(delPos, SeekOrigin.Begin);
                    compWriter.Write((byte)0);
                    count++;
                }

                head = next;
            }

            Console.WriteLine($"Восстановлено записей: {count}");
        }

        static void Restore(string name)
        {
            if (compFs == null)
                throw new Exception("Сначала откройте файл (Open).");

            compFs.Seek(2, SeekOrigin.Begin);
            short len = compReader.ReadInt16();
            int head = compReader.ReadInt32();

            while (head != -1)
            {
                compFs.Seek(head, SeekOrigin.Begin);

                int next = compReader.ReadInt32();
                compReader.ReadInt32();
                long delPos = compFs.Position;
                byte deleted = compReader.ReadByte();
                compReader.ReadByte();
                string curName = new string(compReader.ReadChars(len)).Trim('\0');

                if (curName == name && deleted == 1)
                {
                    compFs.Seek(delPos, SeekOrigin.Begin);
                    compWriter.Write((byte)0);
                    Console.WriteLine("Запись восстановлена");
                    return;
                }

                head = next;
            }

            Console.WriteLine("Удалённая запись не найдена");
        }

        static void Truncate()
        {
            if (compFs == null)
                throw new Exception("Сначала откройте файл (Open).");

            string tempFile = currentFile + ".tmp";

            compFs.Seek(2, SeekOrigin.Begin);
            short len = compReader.ReadInt16();
            int head = compReader.ReadInt32();

            using var newFs = new FileStream(tempFile, FileMode.Create);
            using var newBw = new BinaryWriter(newFs);

            newBw.Write((byte)'P');
            newBw.Write((byte)'S');
            newBw.Write(len);
            newBw.Write(-1); // new head
            newBw.Write(-1); // free

            int newHead = -1;
            int newTail = -1;

            while (head != -1)
            {
                compFs.Seek(head, SeekOrigin.Begin);

                int next = compReader.ReadInt32();
                int spec = compReader.ReadInt32();
                byte deleted = compReader.ReadByte();
                byte type = compReader.ReadByte();
                char[] name = compReader.ReadChars(len);

                if (deleted == 0)
                {
                    int pos = (int)newFs.Position;

                    newBw.Write(-1);
                    newBw.Write(spec);
                    newBw.Write((byte)0);
                    newBw.Write(type);
                    newBw.Write(name);

                    if (newHead == -1)
                        newHead = pos;
                    else
                    {
                        newFs.Seek(newTail, SeekOrigin.Begin);
                        newBw.Write(pos);
                    }

                    newTail = pos;
                    newFs.Seek(0, SeekOrigin.End);
                }

                head = next;
            }

            newFs.Seek(4, SeekOrigin.Begin);
            newBw.Write(newHead);
            newBw.Write((int)newFs.Position);

            Close();
            newFs?.Close();
            newBw?.Close();

            File.Delete(currentFile);
            File.Move(tempFile, currentFile);

            Open(currentFile);

            Console.WriteLine("Физическое удаление завершено");
        }

        static void Close()
        {
            compReader?.Close();
            compWriter?.Close();
            compFs?.Close();

            specReader?.Close();
            specWriter?.Close();
            specFs?.Close();

            compReader = null;
            compWriter = null;
            compFs = null;
            specReader = null;
            specWriter = null;
            specFs = null;
        }
    }
}