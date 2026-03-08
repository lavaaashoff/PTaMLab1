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
                        case "Create":
                            HandleCreate(args);
                            break;
                        case "Open":
                            HandleOpen(args);
                            break;
                        case "Input":
                            HandleInput(args);
                            break;
                        case "Delete":
                            HandleDelete(args);
                            break;
                        case "Restore":
                            HandleRestore(args);
                            break;
                        case "Truncate":
                            HandleTruncate();
                            break;
                        case "Print":
                            HandlePrint(args);
                            break;
                        case "Help":
                            HandleHelp(args);
                            break;
                        case "Exit":
                            HandleExit();
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

        static void HandleCreate(string args)
        {
            if (TryParseCreateCommand(args, out string filename, out short maxLength))
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
        }

        static bool TryParseCreateCommand(string args, out string filename, out short maxLength)
        {
            filename = null;
            maxLength = 0;

            var parts = args?.Split(' ');
            if (parts?.Length != 2)
                return false;

            filename = parts[0];
            return short.TryParse(parts[1], out maxLength);
        }

        static void HandleOpen(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
                throw new Exception("Укажите имя файла.");

            string filename = args;
            FileStream fs = Open(filename);

            if (fs == null)
                throw new Exception($"Не удалось открыть файл '{filename}'.");

            Console.WriteLine($"Файл '{filename}' открыт.");
        }

        static void HandleInput(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
                throw new Exception("Нет параметров.");

            if (!args.Contains("(") || !args.Contains(")"))
                throw new Exception("Неверный формат. Используйте: Input(компонент, тип) или Input(компонент/деталь)");

            if (args.Contains("/"))
            {
                HandleInputSpec(args);
            }
            else
            {
                HandleInputComponent(args);
            }
        }

        static void HandleInputSpec(string args)
        {
            string componentName = args.Substring(
                args.IndexOf("(") + 1,
                args.IndexOf("/") - args.IndexOf("(") - 1);

            string detailName = args.Substring(
                args.IndexOf("/") + 1,
                args.IndexOf(")") - args.IndexOf("/") - 1);

            if (string.IsNullOrWhiteSpace(componentName) || string.IsNullOrWhiteSpace(detailName))
                throw new Exception("Имена не могут быть пустыми.");

            Console.WriteLine($"Компонент: {componentName}");
            Console.WriteLine($"Деталь: {detailName}");

            if (compFs != null)
            {
                int parentOffset = FindComponent(componentName);
                if (parentOffset != -1)
                {
                    compFs.Seek(parentOffset, SeekOrigin.Begin);
                    compReader.ReadInt32(); // next
                    compReader.ReadInt32(); // specHead
                    compReader.ReadByte();  // deleted
                    byte parentType = compReader.ReadByte();

                    if ((ComponentType)parentType == ComponentType.Detail)
                    {
                        throw new Exception("Ошибка: Деталь не может содержать другие компоненты (тупиковый компонент)");
                    }
                }
            }

            InputSpec(args);
        }

        static void HandleInputComponent(string args)
        {
            if (!args.Contains(","))
                throw new Exception("Нужна запятая. Используйте: Input(компонент, тип)");

            string componentName = args.Substring(
                args.IndexOf("(") + 1,
                args.IndexOf(",") - args.IndexOf("(") - 1).Trim();

            string componentType = args.Substring(
                args.IndexOf(", ") + 2,
                args.IndexOf(")") - args.IndexOf(", ") - 2).Trim();

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
                throw new Exception($"Неверный тип компонента. Используйте: Product, Unit или Detail");
            }
        }

        static void HandleDelete(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
                throw new Exception("Нет параметров.");

            if (!args.Contains("(") || !args.Contains(")"))
                throw new Exception("Неверный формат. Используйте: Delete(компонент) или Delete(компонент/деталь)");

            if (args.Contains("/"))
            {
                string componentName = args.Substring(
                    args.IndexOf("(") + 1,
                    args.IndexOf("/") - args.IndexOf("(") - 1);

                string detailName = args.Substring(
                    args.IndexOf("/") + 1,
                    args.IndexOf(")") - args.IndexOf("/") - 1);

                Console.WriteLine($"Удалить компонент: {componentName}");
                Console.WriteLine($"Удалить деталь: {detailName}");

                DeleteSpec(args);
            }
            else
            {
                string componentName = args.Substring(
                    args.IndexOf("(") + 1,
                    args.IndexOf(")") - args.IndexOf("(") - 1).Trim();

                Console.WriteLine($"Удалить компонент: {componentName}");

                Delete(componentName);
            }
        }

        static void HandleRestore(string args)
        {
            if (args.Contains("*"))
            {
                Console.WriteLine("Восстановить всё");
                RestoreAll();
            }
            else
            {
                if (!args.Contains("(") || !args.Contains(")"))
                    throw new Exception("Используйте: Restore(*) или Restore(компонент)");

                string componentName = args.Substring(
                    args.IndexOf("(") + 1,
                    args.IndexOf(")") - args.IndexOf("(") - 1).Trim();

                Console.WriteLine($"Восстановить: {componentName}");

                Restore(componentName);
            }
        }

        static void HandleTruncate()
        {
            Console.WriteLine("Очистка...");
            Truncate();
        }

        static void HandlePrint(string args)
        {
            if (args.Contains("*"))
            {
                Console.WriteLine("Печать всего");
                PrintAll();
            }
            else
            {
                if (!args.Contains("(") || !args.Contains(")"))
                    throw new Exception("Используйте: Print(*) или Print(компонент)");

                string componentName = args.Substring(
                    args.IndexOf("(") + 1,
                    args.IndexOf(")") - args.IndexOf("(") - 1).Trim();

                Print(componentName);
            }
        }

        static void HandleHelp(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                DisplayHelpToConsole();
            }
            else
            {
                SaveHelpToFile(args);
                Console.WriteLine($"Справка сохранена в {args}");
            }
        }

        static void HandleExit()
        {
            Close();
            Environment.Exit(0);
        }

        static void DisplayHelpToConsole()
        {
            Console.WriteLine("Доступные команды:");
            Console.WriteLine("  Create <имя_файла> <длина_записи> - создать новый файл");
            Console.WriteLine("  Open <имя_файла> - открыть существующий файл");
            Console.WriteLine("  Input(компонент, тип) - добавить компонент");
            Console.WriteLine("  Input(компонент/деталь) - добавить спецификацию");
            Console.WriteLine("  Delete(компонент) - удалить компонент");
            Console.WriteLine("  Delete(компонент/деталь) - удалить спецификацию");
            Console.WriteLine("  Restore(*) - восстановить все удаленные записи");
            Console.WriteLine("  Restore(компонент) - восстановить конкретный компонент");
            Console.WriteLine("  Truncate - физически удалить помеченные записи");
            Console.WriteLine("  Print(*) - вывести все компоненты");
            Console.WriteLine("  Print(компонент) - вывести информацию о компоненте");
            Console.WriteLine("  Help [файл] - показать справку или сохранить в файл");
            Console.WriteLine("  Exit - выход из программы");
        }

        static void SaveHelpToFile(string filename)
        {
            using (var writer = new StreamWriter(filename))
            {
                writer.WriteLine("Справка по командам PSConsole:");
                writer.WriteLine("  Create <имя_файла> <длина_записи> - создать новый файл");
                writer.WriteLine("  Open <имя_файла> - открыть существующий файл");
                writer.WriteLine("  Input(компонент, тип) - добавить компонент");
                writer.WriteLine("  Input(компонент/деталь) - добавить спецификацию");
                writer.WriteLine("  Delete(компонент) - удалить компонент");
                writer.WriteLine("  Delete(компонент/деталь) - удалить спецификацию");
                writer.WriteLine("  Restore(*) - восстановить все удаленные записи");
                writer.WriteLine("  Restore(компонент) - восстановить конкретный компонент");
                writer.WriteLine("  Truncate - физически удалить помеченные записи");
                writer.WriteLine("  Print(*) - вывести все компоненты");
                writer.WriteLine("  Print(компонент) - вывести информацию о компоненте");
                writer.WriteLine("  Help [файл] - показать справку или сохранить в файл");
                writer.WriteLine("  Exit - выход из программы");
            }
        }

        static void PrintAll()
        {
            if (compFs == null)
            {
                Console.WriteLine("Сначала откройте файл (Open).");
                return;
            }

            compFs.Seek(2, SeekOrigin.Begin);
            short len = compReader.ReadInt16();
            int head = compReader.ReadInt32();

            Console.WriteLine("Список всех компонентов:");
            bool found = false;
            while (head != -1)
            {
                compFs.Seek(head, SeekOrigin.Begin);
                byte del = compReader.ReadByte();
                compReader.ReadInt32();
                int next = compReader.ReadInt32();
                byte type = compReader.ReadByte();
                string name = new string(compReader.ReadChars(len)).Trim('\0');

                if (del == 0)
                {
                    Console.WriteLine($"{name,-20} {(ComponentType)type}");
                    found = true;
                }

                head = next;
            }

            if (!found)
                Console.WriteLine("Компоненты не найдены.");
        }

        static void Create(string filename, short maxLength)
        {
            // файл спецификаций
            string prs = Path.ChangeExtension(filename, ".prs");

            using (var fs = new FileStream(prs, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(-1); // Указатель на логически первую запись
                bw.Write(8);  // Указатель на свободную область
            }

            // файл компонентов
            using (var fs = new FileStream(filename, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write((byte)'P');
                bw.Write((byte)'S');
                bw.Write(maxLength); // Длина записи данных
                bw.Write(-1); // Указатель на логически первую запись
                bw.Write(28); // Указатель на свободную область

                byte[] nameBytes = new byte[16];
                var src = System.Text.Encoding.ASCII.GetBytes(prs);
                Array.Copy(src, nameBytes, Math.Min(src.Length, 16));
                bw.Write(nameBytes);
            }
        }

        static FileStream Open(string name)
        {
            currentFile = name;
            specFile = Path.ChangeExtension(name, ".prs");

            compFs = new FileStream(name, FileMode.Open, FileAccess.ReadWrite);
            compReader = new BinaryReader(compFs);
            compWriter = new BinaryWriter(compFs);

            specFs = new FileStream(specFile, FileMode.Open, FileAccess.ReadWrite);
            specReader = new BinaryReader(specFs);
            specWriter = new BinaryWriter(specFs);

            if (compReader.ReadByte() != 'P' || compReader.ReadByte() != 'S')
            {
                Console.WriteLine("Ошибка сигнатуры");
                Close();
                return null;
            }

            return compFs;
        }

        static void Input(string name, ComponentType type)
        {
            compFs.Seek(2, SeekOrigin.Begin);
            short maxLen = compReader.ReadInt16();
            int head = compReader.ReadInt32();
            int free = compReader.ReadInt32();

            compFs.Seek(free, SeekOrigin.Begin);
            int offset = (int)compFs.Position;

            compWriter.Write((byte)0); // бит удаления
            compWriter.Write(-1); // Указатель на запись файла спецификаций
            compWriter.Write(head); // Указатель на следующую запись списка изделий
            compWriter.Write((byte)type); // бит типа (не из методички, своей головой)

            // Область данных
            byte[] data = new byte[maxLen];
            byte[] src = System.Text.Encoding.ASCII.GetBytes(name);

            // вот тут исключение можно предусмотреть
            int len = Math.Min(src.Length, maxLen);
            Array.Copy(src, data, len);

            for (int i = len; i < maxLen; i++)
                data[i] = (byte)' ';

            compWriter.Write(data);

            // новый head
            compFs.Seek(4, SeekOrigin.Begin);
            compWriter.Write(offset);

            // новый free
            compFs.Seek(8, SeekOrigin.Begin);
            compWriter.Write(offset + 1 + 4 + 4 + 1 + maxLen);
        }

        static void InputSpec(string args)
        {
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
            // Проверяем тип родительского компонента
            compFs.Seek(parentOffset, SeekOrigin.Begin);
            compReader.ReadInt32(); 
            compReader.ReadInt32(); 
            compReader.ReadByte();  
            byte parentType = compReader.ReadByte();

            // Проверяем тип дочернего компонента
            compFs.Seek(childOffset, SeekOrigin.Begin);
            compReader.ReadInt32(); // пропускаем next
            compReader.ReadInt32(); // пропускаем specHead
            compReader.ReadByte();  // пропускаем deleted
            byte childType = compReader.ReadByte();


            if ((ComponentType)parentType == ComponentType.Detail)
            {
                Console.WriteLine("Ошибка: Деталь не может иметь спецификаций");
                return;
            }

            if (parentOffset == childOffset)
            {
                Console.WriteLine("Ошибка: Компонент не может ссылаться сам на себя");
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
            int newPos = (int)specFs.Position;

            specWriter.Write((byte)0);
            specWriter.Write(childOffset);
            specWriter.Write((short)1);
            specWriter.Write(head);

            // новая запись становится головой
            specFs.Seek(0, SeekOrigin.Begin);
            specWriter.Write(newPos);
            specWriter.Write(8);

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

                byte del = compReader.ReadByte();
                compReader.ReadInt32();
                int next = compReader.ReadInt32();
                string cur = new string(compReader.ReadChars(len)).Trim('\0');

                if (cur == name && del == 0)
                    return head;

                head = next;
            }

            return -1;
        }

        static void Print(string name)
        {
            if (compFs == null)
            {
                Console.WriteLine("Сначала откройте файл (Open).");
                return;
            }

            compFs.Seek(2, SeekOrigin.Begin);
            short len = compReader.ReadInt16();
            int head = compReader.ReadInt32();

            while (head != -1)
            {
                compFs.Seek(head, SeekOrigin.Begin);
                byte del = compReader.ReadByte();
                int spec = compReader.ReadInt32();
                int next = compReader.ReadInt32();
                byte type = compReader.ReadByte();
                string curName = new string(compReader.ReadChars(len)).Trim('\0', ' ');

                if (curName == name && del == 0)
                {
                    Console.WriteLine($"Компонент: {curName}");
                    Console.WriteLine($"Тип: {(ComponentType)type}");

                    // Вывод спецификаций
                    if (spec != -1)
                    {
                        Console.WriteLine("Спецификации:");
                        PrintSpecifications(spec);
                    }
                    return;
                }

                head = next;
            }

            Console.WriteLine("Компонент не найден");
        }

        static void PrintSpecifications(int specHead)
        {
            int cur = specHead;

            while (cur != -1)
            {
                specFs.Seek(cur, SeekOrigin.Begin);

                byte del = specReader.ReadByte();
                int comp = specReader.ReadInt32();
                short count = specReader.ReadInt16();
                int next = specReader.ReadInt32();

                if (del == 0)
                {
                    compFs.Seek(comp, SeekOrigin.Begin);
                    int nextComp = compReader.ReadInt32();
                    int specComp = compReader.ReadInt32();
                    byte delComp = compReader.ReadByte();
                    byte typeComp = compReader.ReadByte();
                    short len = compReader.ReadInt16();
                    string name = new string(compReader.ReadChars(len)).Trim('\0');

                    Console.WriteLine($"  - {name} ({(ComponentType)typeComp}) x{count}");
                }

                cur = next;
            }
        }

        static void Delete(string name)
        {
            if (compFs == null)
            {
                Console.WriteLine("Сначала откройте файл (Open).");
                return;
            }

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

        static void DeleteSpec(string args)
        {
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

            // Ищем спецификацию для удаления
            specFs.Seek(0, SeekOrigin.Begin);
            int head = specReader.ReadInt32();
            int cur = head;
            int prev = -1;

            while (cur != -1)
            {
                specFs.Seek(cur, SeekOrigin.Begin);

                byte del = specReader.ReadByte();
                int comp = specReader.ReadInt32();
                short count = specReader.ReadInt16();
                int next = specReader.ReadInt32();

                if (comp == childOffset && del == 0)
                {
                    if (count > 1)
                    {
                        // Уменьшаем кратность
                        specFs.Seek(cur + 5, SeekOrigin.Begin);
                        specWriter.Write((short)(count - 1));
                        Console.WriteLine("Кратность уменьшена");
                    }
                    else
                    {
                        // Помечаем запись как удаленную
                        specFs.Seek(cur, SeekOrigin.Begin);
                        specWriter.Write((byte)1);
                        Console.WriteLine("Спецификация помечена как удалённая");
                    }
                    return;
                }

                prev = cur;
                cur = next;
            }

            Console.WriteLine("Спецификация не найдена");
        }

        static void RestoreAll()
        {
            if (compFs == null)
            {
                Console.WriteLine("Сначала откройте файл (Open).");
                return;
            }

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
            {
                Console.WriteLine("Сначала откройте файл (Open).");
                return;
            }

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
            if (compFs == null || string.IsNullOrEmpty(currentFile))
            {
                Console.WriteLine("Сначала откройте файл (Open).");
                return;
            }

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
                        newFs.Seek(0, SeekOrigin.End);
                    }

                    newTail = pos;
                }

                head = next;
            }

            newFs.Seek(4, SeekOrigin.Begin);
            newBw.Write(newHead);
            newBw.Write((int)newFs.Length);

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
        }
    }
}