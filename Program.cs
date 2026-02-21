using System.IO;
using Form;

namespace TaMP
{
    internal class Program
    {
        static public void Create(string fileName, short maxLength)
        {
            byte[] dataToWrite = { Convert.ToByte('P'), Convert.ToByte('S'), Convert.ToByte(maxLength), Convert.ToByte(new Element()), Convert.ToByte(new Element()) };
            File.WriteAllBytes(fileName, dataToWrite);

        
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
            using BinaryReader reader = new(fs);
            using BinaryWriter writer = new(fs);

            // Проверка сигнатуры
            byte sign1 = reader.ReadByte();
            byte sign2 = reader.ReadByte();
            if (sign1 != (byte)'P' || sign2 != (byte)'S')
            {
                Console.WriteLine("Ошибка сигнатуры");
                return;
            }

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



        static void Main(string[] args)
        {
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

                string filename = string.Empty;
                short maxLength;

                switch (commandType)
                {
                    case "Create":
                        filename = commandParams.Substring(0, commandParams.IndexOf("("));
                        maxLength = Convert.ToInt16(commandParams.Substring(commandParams.IndexOf("(") + 1, commandParams.IndexOf(")") - commandParams.IndexOf("(") - 1));
                        Console.WriteLine(filename);
                        Console.WriteLine(maxLength);
                        //Create(filename, maxLength);
                        break;

                    case "Open":
                        filename = commandParams;
                        Console.WriteLine(filename);
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
