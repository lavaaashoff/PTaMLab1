using System.IO;
using Form;

namespace TaMP
{
    internal class Program
    {
        static public void Create(string fileName, short maxLength)
        {
            using (FileStream fs = new FileStream(fileName, FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(fs))
            {
                writer.Write((byte)'P');
                writer.Write((byte)'S');
                writer.Write(maxLength);
                writer.Write(-1);
                writer.Write(-1);
                int freeOffset = (int)fs.Position;
                writer.Seek(freeOffset - 4, SeekOrigin.Begin);
                writer.Write(freeOffset);
            }
        }

        static public FileStream Open(string fileName)
        {
            if (!File.Exists(fileName))
            {
                Console.WriteLine("Файла не существует");
                return null;
            }
            FileStream fs = new(fileName, FileMode.Open, FileAccess.ReadWrite);
            BinaryReader reader = new BinaryReader(fs);

            byte sign1 = reader.ReadByte();
            byte sign2 = reader.ReadByte();
            if (sign1 != (byte)'P' || sign2 != (byte)'S')
            {
                Console.WriteLine("Ошика: неправильная сигнатура файла");
                reader.Close();
                fs.Close();
                return null;
            }

            short maxLength = reader.ReadInt16();
            int head = reader.ReadInt32();
            int tail = reader.ReadInt32();

            Console.WriteLine("Файл успешно прочитан");
            Console.WriteLine($"Длина записи данных: {maxLength}");
            Console.WriteLine($"Первая логическая запись: {head}");
            Console.WriteLine($"Свободная область: {tail}");
            return fs;
        }

        static public void Input(FileStream fs, string name, Form.Type type)
        {
            fs.Seek(2, SeekOrigin.Begin);
            using BinaryReader reader = new(fs);
            using BinaryWriter writer = new(fs);



            short maxLength = reader.ReadInt16();
            int head = reader.ReadInt32();
            int tail = reader.ReadInt32();

            if (name.Length > maxLength)
            {
                Console.WriteLine("Имя превышает допустимую длину");
                return;
            }

            // Смещение новой записи
            fs.Seek(0, SeekOrigin.End);
            int newOffset = (int)fs.Position;

            // --- Запись новой записи ---
            writer.Write(-1);                 // Next
            writer.Write(-1);                 // Specs
            writer.Write((byte)0);            // ForDelete
            writer.Write((byte)type);         // Type

            // Запись строки фиксированной длины
            char[] buffer = new char[maxLength];
            name.CopyTo(0, buffer, 0, name.Length);
            writer.Write(buffer);

            // --- Обновление списка ---

            if (head == -1)
            {
                // список пуст
                head = newOffset;
            }
            else
            {
                // обновляем Next у старого tail
                fs.Seek(tail, SeekOrigin.Begin);
                writer.Write(newOffset);
            }

            tail = newOffset;

            // --- Перезапись head и tail в заголовке ---
            fs.Seek(4, SeekOrigin.Begin);
            writer.Write(head);
            writer.Write(tail);

            Console.WriteLine("Компонент добавлен");
        }
        static public bool TryParseCreateCommand(string commandParams, out string filename, out short maxLength)
        {
            filename = null;
            maxLength = 0;

            if (!commandParams.Contains("(") || !commandParams.Contains(")"))
            {
                Console.WriteLine("Ошибка: Неверный формат команды");
                return false;
            }

            filename = commandParams.Substring(0, commandParams.IndexOf("("));
            if (string.IsNullOrWhiteSpace(filename))
            {
                Console.WriteLine("Ошибка: Имя файла не может быть пустым");
                return false;
            }

            string lengthStr = commandParams.Substring(commandParams.IndexOf("(") + 1, commandParams.IndexOf(")") - commandParams.IndexOf("(") - 1);
            if (string.IsNullOrWhiteSpace(lengthStr))
            {
                Console.WriteLine("Ошибка: Длина записи не может быть пустой");
                return false;
            }
            if (!short.TryParse(lengthStr, out maxLength))
            {
                Console.WriteLine("Ошибка: Длина записи должна быть числом");
                return false;
            }
            if (maxLength <= 0)
            {
                Console.WriteLine("Ошибка: Длина записи должна быть положительным числом");
                return false;
            }

            return true;
        }

        static void Main(string[] args)
        {

            FileStream fs = null;
            while (true)
            {
                Console.Write("PS> ");
                string command = Console.ReadLine();
                string[] splittedCommand = command.Split(' ');
                string commandType = splittedCommand[0];
                string commandParams = string.Empty;
                for (int i = 1; i < command.Split(' ').Length; i++)
                {
                    commandParams += splittedCommand[i] + ' ';
                }
                switch (commandType)
                {
                    case "Create":
                        if (TryParseCreateCommand(commandParams, out string filename, out short maxLength))
                        {
                            Console.WriteLine($"Имя файла: {filename}");
                            Console.WriteLine($"Длина записи: {maxLength}");
                            try
                            {
                                Create(filename, maxLength);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Ошибка при создании файла: {ex.Message}");
                            }
                        }
                        break;

                    case "Open":
                        filename = commandParams;
                        fs = Open(filename);
                        break;

                    case "Input":
                        {
                            if (commandParams.Contains("/"))
                            {
                                string componentName = commandParams.Substring(
                                    commandParams.IndexOf("(") + 1,
                                    commandParams.IndexOf("/") - commandParams.IndexOf("(") - 1);

                                string detailName = commandParams.Substring(
                                    commandParams.IndexOf("/") + 1,
                                    commandParams.IndexOf(")") - commandParams.IndexOf("/") - 1);

                                Console.WriteLine(componentName);
                                Console.WriteLine(detailName);
                            }
                            else
                            {
                                string componentName = commandParams.Substring(
                                    commandParams.IndexOf("(") + 1,
                                    commandParams.IndexOf(",") - commandParams.IndexOf("(") - 1);

                                string componentType = commandParams.Substring(
                                    commandParams.IndexOf(", ") + 2,
                                    commandParams.IndexOf(")") - commandParams.IndexOf(", ") - 2);

                                Console.WriteLine(componentName);
                                Console.WriteLine(componentType);
                                Input(fs, componentName, Form.Type.Product);
                            }

                            break;
                        }

                    case "Delete":
                        if (commandParams.Contains("/"))
                        {
                            string componentName = commandParams.Substring(
                                commandParams.IndexOf("(") + 1,
                                commandParams.IndexOf("/") - commandParams.IndexOf("(") - 1);

                            string detailName = commandParams.Substring(
                                commandParams.IndexOf("/") + 1,
                                commandParams.IndexOf(")") - commandParams.IndexOf("/") - 1);

                            Console.WriteLine(componentName);
                            Console.WriteLine(detailName);
                        }
                        else
                        {
                            string componentName = commandParams.Substring(
                                commandParams.IndexOf("(") + 1,
                                commandParams.IndexOf(")") - commandParams.IndexOf("(") - 1);

                            Console.WriteLine(componentName);
                        }
                        break;

                    case "Restore":
                        if (commandParams.Contains("*"))
                        {
                            Console.WriteLine("Restore all");
                        }
                        else
                        {
                            string componentName = commandParams.Substring(
                                commandParams.IndexOf("(") + 1,
                                commandParams.IndexOf(")") - commandParams.IndexOf("(") - 1);

                            Console.WriteLine(componentName);
                        }
                        break;

                    case "Truncate":
                        Console.WriteLine("Truncate");
                        break;

                    case "Print":
                        if (commandParams.Contains("*"))
                        {
                            Console.WriteLine("Print all");
                        }
                        else
                        {
                            string componentName = commandParams.Substring(
                                commandParams.IndexOf("(") + 1,
                                commandParams.IndexOf(")") - commandParams.IndexOf("(") - 1);

                            Console.WriteLine(componentName);
                        }
                        break;

                    case "Help":
                        if (string.IsNullOrWhiteSpace(commandParams))
                        {
                            Console.WriteLine("Help to console");
                        }
                        else
                        {
                            string helpFile = commandParams;
                            Console.WriteLine(helpFile);
                        }
                        break;

                    case "Exit":
                        Console.WriteLine("Exit program");
                        break;
                    default:
                        Console.WriteLine("Enter Help");
                        break;
                }
            }
        }
    }
}
